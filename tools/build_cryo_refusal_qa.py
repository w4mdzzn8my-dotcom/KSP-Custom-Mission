#!/usr/bin/env python3
"""Build the one-shot original-provider cryo refusal probe into its dedicated QA copy."""
import argparse
import hashlib
import os
from pathlib import Path
import shutil
import subprocess
import tempfile


ROOT_NAME = "KSP-Hermes-QA"
SAVE_REL = Path("saves/MegaCrew Background QA/persistent.sfs")
MEGACREW_REL = Path("GameData/MegaCrew/Plugins/MegaCrew.dll")
PROVIDER_REL = Path("GameData/REPOSoftTech/BackgroundResources/BackgroundResources.dll")
OUTPUT_REL = Path("GameData/HermesCryoRefusalQA/HermesCryoRefusalQA.dll")
SOURCE_REL = Path("tests/ksp/CryoRefusalProbe.cs")
SCOPE_TEXT = "MEGACREW_DISPOSABLE_CRYO_REFUSAL_QA_V1\n"
ENABLE_TEXT = "AUTHORIZE ONE ORIGINAL-PROVIDER CRYO REFUSAL QA FOR SAVE: MegaCrew Background QA\n"
EXPECTED = {
    SAVE_REL: "6fd1a826062dafcdf02fa485c89ca818819ea5352bcfc5eca5f1bfde2c1bb4b9",
    MEGACREW_REL: "2d6fd46d353b1cac3b971deff623a2f7f24d2d52cb44cc605e01e8548c5ca986",
    PROVIDER_REL: "ed956b9ec2cc5f414586a52a9086423e4c9125fa72097bce2721ea41eb075bc9",
}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def exact_file(path: Path, text: str) -> bool:
    return path.is_file() and not path.is_symlink() and path.read_bytes() == text.encode("utf-8")


def scan_game_data(game_data: Path) -> list[Path]:
    if game_data.is_symlink() or not game_data.is_dir():
        raise ValueError("GameData must be a real directory inside the disposable root")
    found: list[Path] = []
    for directory, subdirs, filenames in os.walk(game_data, followlinks=False):
        parent = Path(directory)
        for name in list(subdirs):
            child = parent / name
            if child.is_symlink() or not child.resolve().is_relative_to(game_data.resolve()):
                raise ValueError(f"GameData has a symlinked or escaping directory: {child}")
        for name in filenames:
            path = parent / name
            if path.suffix.lower() == ".dll" and "qa" in path.name.lower():
                if path.is_symlink() or not path.resolve().is_relative_to(game_data.resolve()):
                    raise ValueError(f"GameData has a symlinked or escaping QA DLL: {path}")
                found.append(path)
    return found


def pinned_inputs(root: Path) -> dict[Path, Path]:
    inputs = {rel: root / rel for rel in EXPECTED}
    for rel, path in inputs.items():
        if not path.is_file() or path.is_symlink():
            raise ValueError(f"Pinned input is missing or is a symlink: {rel}")
        actual = sha256(path)
        if actual != EXPECTED[rel]:
            raise ValueError(f"Pinned input hash mismatch for {rel}: {actual}")
    return inputs


def prepare(root: Path, repo: Path, csc: Path, dotnet: str) -> Path:
    if not root.is_dir() or root.name != ROOT_NAME:
        raise ValueError(f"QA root must be an existing directory named exactly {ROOT_NAME}")
    if not exact_file(root / "CryoRefusalQA.scope", SCOPE_TEXT):
        raise ValueError("CryoRefusalQA.scope must contain only its exact dedicated-root sentinel")
    if not exact_file(root / "CryoRefusalQA.enable", ENABLE_TEXT):
        raise ValueError("CryoRefusalQA.enable must contain the exact one-shot original-provider authorization")
    for marker in ("CryoRefusalQA.running", "CryoRefusalQA.done", "CryoRefusalQA-result.cfg"):
        if (root / marker).exists():
            raise ValueError("Cryo refusal probe is already claimed or has a report; do not rearm it")

    scan = scan_game_data(root / "GameData")
    if scan:
        names = ", ".join(str(path.relative_to(root)) for path in scan[:8])
        raise ValueError(f"Refusing to install beside another QA DLL in GameData: {names}")
    inputs = pinned_inputs(root)

    managed = root / "KSP.app/Contents/Resources/Data/Managed"
    csc = csc.resolve(strict=True)
    refs = sorted(managed.glob("*.dll"))
    if not managed.is_dir() or not (managed / "Assembly-CSharp.dll").is_file() or not refs:
        raise ValueError("KSP 1.12.5 Data/Managed is missing from the disposable root")
    if not csc.is_file():
        raise ValueError("Roslyn csc.dll is missing")
    source = repo / SOURCE_REL
    if not source.is_file():
        raise ValueError(f"Probe source is missing: {source}")

    output = root / OUTPUT_REL
    if output.exists():
        raise ValueError(f"Refusing to overwrite QA output: {output}")
    if not output.parent.resolve().is_relative_to(root):
        raise ValueError("QA output path escapes the disposable root")
    for parent in (output.parents[2], output.parent):
        if parent.exists() and parent.is_symlink():
            raise ValueError(f"QA output traverses a symlink: {parent}")

    with tempfile.TemporaryDirectory(prefix="megacrew-cryo-refusal-") as temp_name:
        temp = Path(temp_name)
        staged = temp / output.name
        response = temp / "compile.rsp"
        compile_refs = refs + [inputs[MEGACREW_REL]]
        args = ["-nologo", "-target:library", "-nostdlib+", "-langversion:7.3", f'-out:"{staged}"']
        args.extend(f'-reference:"{reference}"' for reference in compile_refs)
        args.append(f'"{source}"')
        response.write_text("\n".join(args) + "\n", encoding="utf-8")
        subprocess.run([dotnet, str(csc), "@" + str(response)], check=True)
        if not staged.is_file():
            raise RuntimeError("Compiler returned without producing the refusal QA addon")

        # Recheck every pinned input after compilation and before writing the one addon.
        for rel, path in inputs.items():
            if sha256(path) != EXPECTED[rel]:
                raise RuntimeError(f"Pinned input changed during build: {rel}")
        if scan_game_data(root / "GameData"):
            raise RuntimeError("A QA DLL appeared in GameData during compilation; refusing install")
        output.parent.mkdir(parents=True, exist_ok=True)
        if not output.parent.resolve().is_relative_to(root) or any(
                parent.is_symlink() for parent in (output.parents[2], output.parent)):
            raise RuntimeError("QA output directory escaped through a symlink")
        with staged.open("rb") as src, output.open("xb") as dst:
            shutil.copyfileobj(src, dst)
    return output


def main() -> int:
    repo = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--qa-root", type=Path, required=True, help="Existing dedicated KSP-Hermes-QA root")
    parser.add_argument("--csc", type=Path, required=True, help="Roslyn bincore/csc.dll")
    parser.add_argument("--dotnet", default="dotnet", help="dotnet executable")
    args = parser.parse_args()
    try:
        output = prepare(args.qa_root.expanduser().resolve(strict=True), repo, args.csc.expanduser(), args.dotnet)
    except (OSError, ValueError, RuntimeError, subprocess.CalledProcessError) as exc:
        parser.error(str(exc))
    print(f"Built one scoped refusal QA addon: {output}")
    print("The build wrote only this QA addon; the runtime probe performs its single native lifecycle save in the disposable QA copy.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
