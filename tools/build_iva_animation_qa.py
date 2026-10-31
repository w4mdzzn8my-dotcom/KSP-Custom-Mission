#!/usr/bin/env python3
"""Build the isolated, read-only IVA animation catalog addon into a disposable KSP copy."""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import tempfile


SCOPE_CONTENTS = "MEGACREW_DISPOSABLE_IVA_ANIMATION_QA_V1\n"
ENABLE_CONTENTS = "AUTHORIZE ONE READ-ONLY IVA ANIMATION CATALOG FOR SAVE: MegaCrew Background QA\n"
EXPECTED_ROOT_NAME = "KSP-Hermes-QA"
OUTPUT_RELATIVE = Path("GameData/MegaCrewIvaAnimationQA/Plugins/MegaCrewIvaAnimationQA.dll")
SOURCE_RELATIVE = Path("tests/ksp/IvaAnimationCatalogProbe.cs")


def parser_for(repo: Path) -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--qa-root", type=Path, required=True,
                        help="Existing disposable KSP root named exactly KSP-Hermes-QA")
    parser.add_argument("--managed", type=Path, required=True,
                        help="KSP 1.12.5 Data/Managed directory")
    parser.add_argument("--csc", type=Path, required=True,
                        help="Roslyn bincore/csc.dll")
    parser.add_argument("--dotnet", default="dotnet", help="dotnet executable")
    parser.set_defaults(repo=repo)
    return parser


def resolve_inputs(args: argparse.Namespace) -> tuple[Path, Path, list[Path], Path]:
    root = args.qa_root.expanduser().resolve(strict=True)
    if not root.is_dir() or root.name != EXPECTED_ROOT_NAME:
        raise ValueError(f"--qa-root must be an existing directory named exactly {EXPECTED_ROOT_NAME}")

    if not exact_file(root / "IvaAnimationQA.scope", SCOPE_CONTENTS):
        raise ValueError("IvaAnimationQA.scope must contain only the exact disposable-root sentinel line")
    if not exact_file(root / "IvaAnimationQA.enable", ENABLE_CONTENTS):
        raise ValueError("IvaAnimationQA.enable must contain the exact one-shot save authorization line")

    markers = ("IvaAnimationQA.running", "IvaAnimationQA.done", "IvaAnimationQA.failed",
               "IvaAnimationQA.report.txt")
    if any((root / marker).exists() for marker in markers):
        raise ValueError("This disposable root already has an IVA QA run marker or report; it cannot be re-armed")

    game_data = root / "GameData"
    if not game_data.is_dir() or game_data.is_symlink() or not game_data.resolve().is_relative_to(root):
        raise ValueError("GameData must be a real directory inside the disposable root")
    existing_qa_dlls = scan_qa_dlls(game_data)
    if existing_qa_dlls:
        paths = ", ".join(str(path.relative_to(root)) for path in existing_qa_dlls[:5])
        raise ValueError(f"Disposable root already contains a QA DLL; refusing overwrite or multiple QA addons: {paths}")

    managed = args.managed.expanduser().resolve(strict=True)
    csc = args.csc.expanduser().resolve(strict=True)
    refs = sorted(managed.glob("*.dll"))
    if not managed.is_dir() or not (managed / "Assembly-CSharp.dll").is_file() or not refs:
        raise ValueError("--managed must be the KSP Data/Managed directory containing Assembly-CSharp.dll")
    if not csc.is_file():
        raise ValueError("--csc must identify an existing Roslyn csc.dll")
    source = args.repo / SOURCE_RELATIVE
    if not source.is_file():
        raise ValueError(f"Missing probe source: {source}")

    output = root / OUTPUT_RELATIVE
    if output.exists():
        raise ValueError(f"Output already exists; refusing overwrite: {output}")
    if not output.parent.resolve().is_relative_to(root):
        raise ValueError("Resolved plugin output escapes the disposable QA root")
    for parent in (output.parents[2], output.parents[1], output.parent):
        if parent.exists() and parent.is_symlink():
            raise ValueError(f"Plugin output path may not pass through symlinked directory: {parent}")
    return root, source, refs, output


def exact_file(path: Path, contents: str) -> bool:
    return path.is_file() and not path.is_symlink() and path.read_bytes() == contents.encode("utf-8")


def scan_qa_dlls(game_data: Path) -> list[Path]:
    found: list[Path] = []
    for directory, subdirs, filenames in os.walk(game_data, followlinks=False):
        base = Path(directory)
        for name in list(subdirs):
            child = base / name
            if child.is_symlink():
                raise ValueError(f"GameData contains a symlinked directory; cannot verify loaded QA DLLs: {child}")
        for name in filenames:
            path = base / name
            if path.suffix.lower() == ".dll" and "qa" in path.name.lower():
                if path.is_symlink():
                    raise ValueError(f"GameData contains a symlinked QA DLL: {path}")
                found.append(path)
    return found


def compile_probe(args: argparse.Namespace, root: Path, source: Path, refs: list[Path], output: Path) -> None:
    with tempfile.TemporaryDirectory(prefix="megacrew-iva-qa-") as temp_name:
        temp = Path(temp_name)
        staged = temp / "MegaCrewIvaAnimationQA.dll"
        response = temp / "compile.rsp"
        values = ["-nologo", "-target:library", "-nostdlib+", "-langversion:7.3",
                  f'-out:"{staged}"']
        values.extend(f'-reference:"{ref}"' for ref in refs)
        values.append(f'"{source}"')
        response.write_text("\n".join(values) + "\n", encoding="utf-8")
        subprocess.run([args.dotnet, str(args.csc), "@" + str(response)], check=True)
        if not staged.is_file():
            raise RuntimeError("Roslyn returned successfully without producing the QA addon")

        # The build emits one addon at one dedicated path, using exclusive creation.
        output.parent.mkdir(parents=True, exist_ok=True)
        if not output.parent.resolve().is_relative_to(root):
            raise RuntimeError("Plugin directory resolved outside the disposable QA root")
        if any(parent.is_symlink() for parent in (output.parents[2], output.parents[1], output.parent)):
            raise RuntimeError("Plugin output path passes through a symlinked directory")
        with staged.open("rb") as src, output.open("xb") as dst:
            shutil.copyfileobj(src, dst)


def main() -> int:
    repo = Path(__file__).resolve().parents[1]
    parser = parser_for(repo)
    args = parser.parse_args()
    try:
        root, source, refs, output = resolve_inputs(args)
        compile_probe(args, root, source, refs, output)
    except (OSError, ValueError, RuntimeError, subprocess.CalledProcessError) as exc:
        parser.error(str(exc))
    print(f"Built isolated read-only IVA catalog addon: {output}")
    print("No normal MegaCrew plugin was built or written.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
