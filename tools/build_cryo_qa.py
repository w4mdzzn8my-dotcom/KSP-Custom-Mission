#!/usr/bin/env python3
"""Compile the one-shot probe into one freshly authorized disposable KSP copy."""

import argparse
import os
from pathlib import Path
import shutil
import subprocess
import tempfile


ROOT_NAME = "KSP-Hermes-QA"
SCOPE_NAME = "HermesCryoQA.scope"
SCOPE_TEXT = b"MEGACREW_DISPOSABLE_CRYO_QA_V1\n"
ENABLE_NAME = "HermesCryoQA.enable"
ENABLE_TEXT = b"one-shot\n"
RUN_MARKERS = (
    "HermesCryoQA.running",
    "HermesCryoQA.done",
    "HermesCryoQA.failed",
    "HermesCryoQA.consumed",
    "HermesCryoQA-result.cfg",
)
OUTPUT_REL = Path("GameData/HermesQA/HermesCryoQA.dll")
SOURCE_REL = Path("tests/ksp/HermesCryoProbe.cs")
REQUIRED_GAME_REFS = (
    Path("GameData/MegaCrew/Plugins/MegaCrew.dll"),
    Path("GameData/REPOSoftTech/DeepFreeze/Plugins/DeepFreeze.dll"),
    Path("GameData/REPOSoftTech/BackgroundResources/BackgroundResources.dll"),
)


def exact_file(path: Path, contents: bytes) -> bool:
    return path.is_file() and not path.is_symlink() and path.read_bytes() == contents


def check_no_symlink_components(path: Path, stop: Path) -> None:
    """Reject symlinks from stop through path, including the final component."""
    path = Path(os.path.abspath(path))
    stop = Path(os.path.abspath(stop))
    try:
        relative = path.relative_to(stop)
    except ValueError as exc:
        raise ValueError(f"Path escapes its expected root: {path}") from exc
    current = stop
    if current.is_symlink():
        raise ValueError(f"Symlink path component is not allowed: {current}")
    for part in relative.parts:
        current = current / part
        if current.is_symlink():
            raise ValueError(f"Symlink path component is not allowed: {current}")


def scan_qa_dlls(game_data: Path) -> list[Path]:
    """Find QA-named DLLs anywhere in live GameData; backups belong outside it."""
    if game_data.is_symlink() or not game_data.is_dir():
        raise ValueError("GameData must be a real directory inside the disposable root")
    real_game_data = game_data.resolve(strict=True)
    found: list[Path] = []
    for directory, subdirs, filenames in os.walk(game_data, followlinks=False):
        parent = Path(directory)
        for name in subdirs:
            child = parent / name
            if child.is_symlink() or not child.resolve().is_relative_to(real_game_data):
                raise ValueError(f"GameData has a symlinked or escaping directory: {child}")
        for name in filenames:
            path = parent / name
            if path.suffix.lower() == ".dll" and "qa" in path.name.casefold():
                if path.is_symlink() or not path.resolve().is_relative_to(real_game_data):
                    raise ValueError(f"GameData has a symlinked or escaping QA DLL: {path}")
                found.append(path)
    return found


def validate_inputs(ksp_root: Path, csc: Path, repo_root: Path) -> dict[str, object]:
    """Validate the complete scope and return resolved paths without modifying the copy."""
    candidate_root = Path(os.path.abspath(ksp_root.expanduser()))
    if candidate_root.is_symlink():
        raise ValueError("KSP root may not be a symlink")
    root = candidate_root.resolve(strict=True)
    if not root.is_dir() or root.name != ROOT_NAME:
        raise ValueError(f"KSP root must be an existing directory named exactly {ROOT_NAME}")

    if not exact_file(root / SCOPE_NAME, SCOPE_TEXT):
        raise ValueError(f"{SCOPE_NAME} must contain only the exact disposable-root sentinel")
    if not exact_file(root / ENABLE_NAME, ENABLE_TEXT):
        raise ValueError(f"{ENABLE_NAME} must contain exactly the fresh one-shot token: one-shot\\n")
    for marker in RUN_MARKERS:
        path = root / marker
        if path.exists() or path.is_symlink():
            raise ValueError(f"This QA root already has a run marker or result: {marker}")

    game_data = root / "GameData"
    check_no_symlink_components(game_data, root)
    existing = scan_qa_dlls(game_data)
    if existing:
        names = ", ".join(str(path.relative_to(root)) for path in existing[:8])
        raise ValueError(f"Refusing to build beside another QA DLL in GameData: {names}")

    managed = root / "KSP.app/Contents/Resources/Data/Managed"
    check_no_symlink_components(managed, root)
    if not managed.is_dir():
        raise ValueError("KSP 1.12.5 Data/Managed is missing from the disposable root")
    managed_refs = sorted(managed.glob("*.dll"))
    if not managed_refs or not any(p.name == "Assembly-CSharp.dll" for p in managed_refs):
        raise ValueError("KSP 1.12.5 Data/Managed is missing required assemblies")
    for reference in managed_refs:
        check_no_symlink_components(reference, root)
        if not reference.is_file():
            raise ValueError(f"Managed assembly is not a regular file: {reference}")

    game_refs = [root / relative for relative in REQUIRED_GAME_REFS]
    for reference in game_refs:
        check_no_symlink_components(reference, root)
        if not reference.is_file():
            raise ValueError(f"Required QA reference is missing: {reference.relative_to(root)}")

    compiler = csc.expanduser().resolve(strict=True)
    if not compiler.is_file():
        raise ValueError("--csc must identify an existing Roslyn csc.dll")
    repo = repo_root.resolve(strict=True)
    source = repo / SOURCE_REL
    if not source.is_file():
        raise ValueError(f"Probe source is missing: {source}")

    output = root / OUTPUT_REL
    check_no_symlink_components(output.parent, root)
    if output.exists() or output.is_symlink():
        raise ValueError(f"Refusing to overwrite QA output: {output}")
    return {
        "root": root,
        "managed_refs": managed_refs,
        "game_refs": game_refs,
        "csc": compiler,
        "source": source,
        "output": output,
        "game_data": game_data,
    }


def build(ksp_root: Path, csc: Path, repo_root: Path, dotnet: str = "dotnet") -> Path:
    inputs = validate_inputs(ksp_root, csc, repo_root)
    root = inputs["root"]
    output = inputs["output"]
    references = inputs["managed_refs"] + inputs["game_refs"]

    with tempfile.TemporaryDirectory(prefix="megacrew-cryo-qa-") as temp_name:
        temp = Path(temp_name)
        staged = temp / OUTPUT_REL.name
        response = temp / "compile.rsp"
        args = ["-nologo", "-target:library", "-nostdlib+", "-langversion:7.3",
                f'-out:"{staged}"']
        args.extend(f'-reference:"{reference}"' for reference in references)
        args.append(f'"{inputs["source"]}"')
        response.write_text("\n".join(args) + "\n", encoding="utf-8")
        subprocess.run([dotnet, str(inputs["csc"]), "@" + str(response)], check=True)
        if not staged.is_file() or staged.stat().st_size == 0:
            raise RuntimeError("Compiler returned without producing a nonempty QA addon")

        # Recheck the one-shot authorization and all live installation boundaries.
        validate_inputs(ksp_root, csc, repo_root)
        output.parent.mkdir(parents=True, exist_ok=True)
        check_no_symlink_components(output.parent, root)
        if not output.parent.resolve().is_relative_to(root.resolve()):
            raise RuntimeError("QA output directory escaped the disposable root")

        # Stage on the destination filesystem, then link exclusively so a racing
        # or pre-existing output can never be overwritten or partially replaced.
        fd, local_stage_name = tempfile.mkstemp(prefix=".HermesCryoQA-", suffix=".tmp",
                                                dir=output.parent)
        local_stage = Path(local_stage_name)
        try:
            with os.fdopen(fd, "wb") as target, staged.open("rb") as source:
                shutil.copyfileobj(source, target)
                target.flush()
                os.fsync(target.fileno())
            os.link(local_stage, output)
        finally:
            try:
                local_stage.unlink()
            except FileNotFoundError:
                pass
    return output


def main() -> int:
    repo = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ksp-root", required=True, type=Path,
                        help="Fresh disposable KSP-Hermes-QA root")
    parser.add_argument("--csc", required=True, type=Path,
                        help="Roslyn bincore/csc.dll")
    parser.add_argument("--dotnet", default="dotnet", help="dotnet executable")
    args = parser.parse_args()
    try:
        output = build(args.ksp_root, args.csc, repo, args.dotnet)
    except (OSError, ValueError, RuntimeError, subprocess.CalledProcessError) as exc:
        parser.error(str(exc))
    print(f"Built one authorized QA addon: {output}")
    print("Only the isolated HermesCryoQA DLL was installed; the normal MegaCrew plugin was untouched.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
