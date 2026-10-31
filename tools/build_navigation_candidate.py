#!/usr/bin/env python3
"""Reconcile the published 0.3.0 package, cumulative campaign patches and nav sources safely.

This tool reads Git objects only. It never installs into KSP, reads/writes a
game save outside the published ZIP, accesses server data, downloads inputs, or
modifies the checkout. Output must be a new directory.
"""
import argparse
import hashlib
import io
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import stat
import subprocess
import sys
import tempfile
import zipfile


EXPECTED_ZIP_SHA256 = "0071eacf3a1b38a63c611fe2a65877ea2f62f6076b7e97d22e9cdf58b412cf40"
ZIP_PATH = "Hermes-Practice-v0.3.0.zip"
NAV_ROOT = "Source/Navigation"
NAVIGATION_TEST_PATHS = (
    "tests/navigation/BurnDurationTests.cs",
    "tests/navigation/LambertRandomizedTests.cs",
    "tests/navigation/MissionControlMathTests.cs",
    "tests/navigation/WatchCoverageTests.cs",
    "tests/navigation/run.py",
)
OPERATIONS_SOURCE_PATHS = (
    "Source/OnboardEngineering.cs",
    "Source/OnboardOperations.cs",
    "Source/OperationsModel.cs",
    "Source/OnboardRendezvous.cs",
    "Source/RendezvousModel.cs",
    "Source/OnboardScienceObservation.cs",
    "Source/ScienceObservationModel.cs",
    "Source/DutyReadinessBridge.cs",
    "Source/OnboardPowerSurvey.cs",
    "Source/PowerSurveyModel.cs",
)
OPERATIONS_TEST_PATHS = (
    "tests/OperationsModelTests.cs",
    "tests/run_operations_tests.py",
    "tests/RendezvousModelTests.cs",
    "tests/run_rendezvous_model_tests.py",
    "tests/ScienceObservationModelTests.cs",
    "tests/run_science_observation_model_tests.py",
    "tests/DutyReadinessBridgeTests.cs",
    "tests/run_duty_readiness_bridge_tests.py",
    "tests/PowerSurveyModelTests.cs",
    "tests/run_power_survey_model_tests.py",
)
DOCKING_GROUP_PATHS = (
    "Source/DockingGeometryModel.cs",
    "Source/OnboardDockingInstruments.cs",
    "tests/DockingGeometryModelTests.cs",
    "tests/run_docking_geometry_model_tests.py",
    "tests/DockingInstrumentControllerTests.cs",
    "tests/DockingInstrumentControllerFakes.cs",
    "tests/run_docking_instrument_controller_tests.py",
)
PATCH_SEQUENCE = (
    {
        "ref": "f76e437",
        "path": "Hermes-Campaign-v0.3.7-source.patch",
        "expected_sha256": "5f59faa0a8220abf9a5119b76fb98caac8e4e73c189cc7e4d2f05948b80b8770",
    },
)
MAX_ENTRIES = 1000
MAX_MEMBER_BYTES = 128 * 1024 * 1024
MAX_TOTAL_BYTES = 512 * 1024 * 1024
INTEGRATION_SNAPSHOT_DIRS = (
    "Source", "docs", "tools", "tests", "Ships", "Missions", "GameData/MegaCrew",
)
INTEGRATION_SNAPSHOT_FILES = (
    "README.md", "dependencies.json", "visual-profile.json",
)
INTEGRATION_EXCLUDED_SUFFIXES = (".dll",)


class BuildError(RuntimeError):
    pass


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def verify_sha256(data, expected, label):
    actual = sha256(data)
    if actual.lower() != expected.lower():
        raise BuildError("{} SHA-256 mismatch: expected {}, got {}".format(label, expected, actual))
    return actual


def validate_archive_member(info):
    raw = info.filename
    if not raw or "\\" in raw or raw.startswith("/") or re.match(r"^[A-Za-z]:", raw):
        raise BuildError("Unsafe ZIP member path: {!r}".format(raw))
    member_text = raw[:-1] if raw.endswith("/") else raw
    raw_parts = member_text.split("/")
    path = PurePosixPath(raw)
    if (path.is_absolute() or not member_text or
            any(part in ("", ".", "..") for part in raw_parts) or
            any(part in ("", ".", "..") for part in path.parts)):
        raise BuildError("Unsafe ZIP member path: {!r}".format(raw))
    if any("\x00" in part for part in path.parts):
        raise BuildError("NUL in ZIP member path")
    mode = (info.external_attr >> 16) & 0xFFFF
    kind = stat.S_IFMT(mode)
    if kind == stat.S_IFLNK:
        raise BuildError("ZIP symlinks are not permitted: {!r}".format(raw))
    if kind not in (0, stat.S_IFREG, stat.S_IFDIR):
        raise BuildError("Special ZIP member is not permitted: {!r}".format(raw))
    if info.flag_bits & 0x1:
        raise BuildError("Encrypted ZIP members are not permitted")
    if info.file_size < 0 or info.file_size > MAX_MEMBER_BYTES:
        raise BuildError("ZIP member exceeds size limit: {!r}".format(raw))
    return path


def safe_extract(zip_bytes, destination):
    """Extract archive paths directly beneath destination; never overwrite."""
    destination = Path(destination)
    if not destination.is_dir():
        raise BuildError("Extraction root must already exist: {}".format(destination))
    names = set()
    total = 0
    with zipfile.ZipFile(io.BytesIO(zip_bytes), "r") as archive:
        infos = archive.infolist()
        if len(infos) > MAX_ENTRIES:
            raise BuildError("ZIP contains too many entries")
        checked = []
        for info in infos:
            member = validate_archive_member(info)
            normalized = member.as_posix().rstrip("/")
            collision_key = normalized.casefold()
            if collision_key in names:
                raise BuildError("Duplicate/case-colliding ZIP path: {!r}".format(info.filename))
            names.add(collision_key)
            total += info.file_size
            if total > MAX_TOTAL_BYTES:
                raise BuildError("ZIP exceeds total uncompressed size limit")
            checked.append((info, member))

        for info, member in checked:
            target = destination.joinpath(*member.parts)
            # Resolve before mkdir as well as after it. In particular, never
            # create missing directories through an existing symlink parent
            # before discovering that it escapes the extraction root.
            parent = target.parent
            root_resolved = destination.resolve()
            parent_resolved = parent.resolve(strict=False)
            if parent_resolved != root_resolved and root_resolved not in parent_resolved.parents:
                raise BuildError("ZIP extraction escaped the output root")
            parent.mkdir(parents=True, exist_ok=True)
            parent_resolved = parent.resolve()
            if parent_resolved != root_resolved and root_resolved not in parent_resolved.parents:
                raise BuildError("ZIP extraction escaped the output root")
            mode = (info.external_attr >> 16) & 0xFFFF
            if info.is_dir() or stat.S_IFMT(mode) == stat.S_IFDIR:
                if target.exists() and not target.is_dir():
                    raise BuildError("ZIP directory would overwrite a file: {}".format(member))
                target.mkdir(parents=True, exist_ok=True)
                continue
            if target.exists() or target.is_symlink():
                raise BuildError("ZIP member would overwrite existing output: {}".format(member))
            # Create exclusively; never follow or replace an existing path.
            data = archive.read(info)
            flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
            if hasattr(os, "O_NOFOLLOW"):
                flags |= os.O_NOFOLLOW
            fd = os.open(str(target), flags, 0o644)
            with os.fdopen(fd, "wb") as stream:
                stream.write(data)
    return len(checked)


def _git(repo, *args, input_bytes=None):
    return subprocess.run(["git", "-C", str(repo)] + list(args), input=input_bytes,
                          stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True).stdout


def resolve_commit(repo, ref):
    if not ref or ref.startswith("-") or "\x00" in ref or "\n" in ref:
        raise BuildError("Invalid Git ref")
    try:
        return _git(repo, "rev-parse", "--verify", "{}^{{commit}}".format(ref)).decode().strip()
    except subprocess.CalledProcessError as exc:
        raise BuildError("Could not resolve Git ref {!r}: {}".format(ref, exc.stderr.decode(errors="replace")))


def git_blob(repo, commit, path):
    try:
        return _git(repo, "show", "{}:{}".format(commit, path))
    except subprocess.CalledProcessError as exc:
        raise BuildError("Missing Git object {}:{} ({})".format(commit, path, exc.stderr.decode(errors="replace").strip()))


def git_file_mode(repo, commit, path):
    """Return mode for an exact tracked path, or None when absent."""
    listing = _git(repo, "ls-tree", commit, "--", path).decode("utf-8").splitlines()
    for line in listing:
        metadata, separator, listed_path = line.partition("\t")
        if not separator or listed_path != path:
            continue
        fields = metadata.split()
        if len(fields) >= 2:
            return fields[0], fields[1]
    return None


def require_regular_git_file(repo, commit, path, label):
    entry = git_file_mode(repo, commit, path)
    if entry is None:
        raise BuildError("Missing Git object {}:{} (path not found)".format(commit, path))
    mode, object_type = entry
    if object_type != "blob" or mode not in ("100644", "100755"):
        raise BuildError("{} is not a regular tracked file: {}:{}".format(label, commit, path))
    return git_blob(repo, commit, path)


def navigation_sources(repo, commit):
    listing = _git(repo, "ls-tree", "-r", "--name-only", commit, "--", NAV_ROOT).decode().splitlines()
    paths = [p for p in listing if p.startswith(NAV_ROOT + "/") and p.endswith(".cs")]
    if not paths:
        raise BuildError("Navigation ref contains no tracked C# source files")
    result = {}
    for path in paths:
        rel = PurePosixPath(path)
        if rel.is_absolute() or ".." in rel.parts or "\\" in path:
            raise BuildError("Unsafe Git navigation path: {!r}".format(path))
        result[path] = git_blob(repo, commit, path)
    return result


def fixed_git_files(repo, commit, paths, label):
    """Read an exact tracked-file allowlist from a pinned commit."""
    result = {}
    for path in paths:
        rel = PurePosixPath(path)
        if rel.is_absolute() or ".." in rel.parts or "\\" in path:
            raise BuildError("Unsafe {} path: {!r}".format(label, path))
        result[path] = require_regular_git_file(repo, commit, path, label)
    return result


def navigation_tests(repo, commit):
    return fixed_git_files(repo, commit, NAVIGATION_TEST_PATHS, "navigation test")


def operations_files(repo, commit):
    base = {
        "sources": fixed_git_files(repo, commit, OPERATIONS_SOURCE_PATHS, "operations source"),
        "tests": fixed_git_files(repo, commit, OPERATIONS_TEST_PATHS, "operations test"),
    }
    present = []
    for path in DOCKING_GROUP_PATHS:
        entry = git_file_mode(repo, commit, path)
        if entry is not None:
            mode, object_type = entry
            if object_type != "blob" or mode not in ("100644", "100755"):
                raise BuildError("Docking group path is not a regular tracked file: {}:{}".format(
                    commit, path))
            present.append(path)
    if present and len(present) != len(DOCKING_GROUP_PATHS):
        missing = sorted(set(DOCKING_GROUP_PATHS) - set(present))
        raise BuildError("Incomplete optional docking group in operations ref; missing: {}".format(
            ", ".join(missing)))
    base["docking"] = (fixed_git_files(repo, commit, DOCKING_GROUP_PATHS, "docking group")
                       if present else None)
    return base


def integration_snapshot_files(repo, commit):
    """Read a complete package snapshot from one immutable Git commit."""
    raw = _git(repo, "ls-tree", "-r", "-z", "--full-tree", commit)
    files = {}
    excluded_dlls = {}
    excluded_historical_metadata = {}
    folded_prefixes_seen = {}
    directories = set(INTEGRATION_SNAPSHOT_DIRS)
    prefixes = tuple(directory + "/" for directory in INTEGRATION_SNAPSHOT_DIRS)
    folded_prefixes = tuple(prefix.casefold() for prefix in prefixes)
    folded_directories = {directory.casefold() for directory in INTEGRATION_SNAPSHOT_DIRS}
    folded_root_files = {path.casefold() for path in INTEGRATION_SNAPSHOT_FILES}

    for record in raw.split(b"\0"):
        if not record:
            continue
        metadata, separator, raw_path = record.partition(b"\t")
        if not separator:
            raise BuildError("Malformed Git tree record in integration snapshot")
        try:
            path = raw_path.decode("utf-8", errors="strict")
        except UnicodeDecodeError as exc:
            raise BuildError("Integration snapshot contains a non-UTF8 path") from exc

        historical_metadata = path == "integration-manifest.json"
        selected = (path in INTEGRATION_SNAPSHOT_FILES or path in directories or
                    path.startswith(prefixes))
        casefold_selected = (path.casefold() in folded_root_files or
                             path.casefold() in folded_directories or
                             path.casefold().startswith(folded_prefixes) or
                             path.casefold() == "integration-manifest.json")
        if not selected and not casefold_selected and not historical_metadata:
            continue

        rel = PurePosixPath(path)
        if (not path or rel.is_absolute() or "\\" in path or
                any(part in ("", ".", "..") for part in path.split("/")) or
                re.match(r"^[A-Za-z]:", path)):
            raise BuildError("Unsafe path in integration snapshot: {!r}".format(path))
        if not selected:
            if not historical_metadata:
                raise BuildError("Integration snapshot path uses noncanonical package-root casing: {}".format(path))

        if path in directories:
            raise BuildError("Integration snapshot package root is not a directory: {}".format(path))

        fields = metadata.decode("ascii", errors="strict").split()
        if len(fields) != 3:
            raise BuildError("Malformed Git tree metadata for {}".format(path))
        mode, object_type, object_id = fields
        if object_type != "blob" or mode not in ("100644", "100755"):
            raise BuildError("Integration snapshot contains a symlink or non-regular file: {}".format(path))

        # Check every ancestor too: two differently-cased directories can
        # alias on a case-insensitive volume even when their leaves differ.
        for part_count in range(1, len(rel.parts) + 1):
            prefix = PurePosixPath(*rel.parts[:part_count]).as_posix()
            collision_key = prefix.casefold()
            prior = folded_prefixes_seen.get(collision_key)
            if prior is not None and prior != prefix:
                raise BuildError("Case-colliding integration snapshot paths: {} and {}".format(
                    prior, prefix))
            folded_prefixes_seen[collision_key] = prefix

        is_dll = PurePosixPath(path).suffix.casefold() in INTEGRATION_EXCLUDED_SUFFIXES
        blob = git_blob(repo, commit, path)
        if historical_metadata:
            excluded_historical_metadata[path] = sha256(blob)
            continue
        if is_dll:
            excluded_dlls[path] = sha256(blob)
        else:
            files[path] = blob

    for required in INTEGRATION_SNAPSHOT_FILES:
        if required not in files:
            raise BuildError("Integration snapshot is missing required package file: {}".format(required))
    if not any(path.startswith("Source/") and path.endswith(".cs") for path in files):
        raise BuildError("Integration snapshot has no tracked C# source under Source/")
    if "Source/MegaCrew.cs" not in files:
        raise BuildError("Integration snapshot is missing Source/MegaCrew.cs")
    return {"files": files, "excluded_dlls": excluded_dlls,
            "excluded_historical_metadata": excluded_historical_metadata}


def replace_snapshot_trees(root, snapshot_files):
    """Replace complete package roots from a verified snapshot, removing stale base files."""
    for relative in INTEGRATION_SNAPSHOT_DIRS:
        target = root / relative
        if target.is_symlink():
            target.unlink()
        elif target.is_dir():
            shutil.rmtree(target)
        elif target.exists():
            target.unlink()
    for relative in INTEGRATION_SNAPSHOT_FILES:
        target = root / relative
        if target.is_symlink() or target.exists():
            target.unlink()
    overlay_files(root, [("full Git integration snapshot", snapshot_files)])


def source_tree_hashes(root):
    result = {}
    source_root = root / "Source"
    if not source_root.is_dir():
        return result
    for path in sorted(source_root.rglob("*")):
        if path.is_symlink():
            raise BuildError("Non-regular source file in canonical base: {}".format(path))
        if path.is_file():
            result[path.relative_to(root).as_posix()] = sha256(path.read_bytes())
    return result


def apply_patch_once(root, patch_bytes, patch_label, sequence_number):
    patch_file = root / (".integration-patch-{}.patch".format(sequence_number))
    patch_file.write_bytes(patch_bytes)
    check = subprocess.run(["git", "apply", "--check", str(patch_file)], cwd=str(root),
                           stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if check.returncode != 0:
        raise BuildError("Patch {} dry-run failed: {}".format(
            patch_label, check.stderr.decode(errors="replace").strip()))
    apply = subprocess.run(["git", "apply", str(patch_file)], cwd=str(root),
                           stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if apply.returncode != 0:
        raise BuildError("Patch {} apply failed: {}".format(
            patch_label, apply.stderr.decode(errors="replace").strip()))
    patch_file.unlink()


def overlay_files(root, groups):
    """Overlay several pinned file maps after preflighting all paths and collisions."""
    combined = {}
    owners = {}
    for label, files in groups:
        for path, content in sorted(files.items()):
            rel = PurePosixPath(path)
            if rel.is_absolute() or ".." in rel.parts or "\\" in path:
                raise BuildError("Unsafe {} overlay path: {!r}".format(label, path))
            if path in combined:
                raise BuildError("Overlay path collision between {} and {}: {}".format(
                    owners[path], label, path))
            combined[path] = content
            owners[path] = label
    for path in sorted(combined):
        target = root.joinpath(*PurePosixPath(path).parts)
        if target.exists() or target.is_symlink():
            raise BuildError("{} overlay would overwrite an existing path: {}".format(
                owners[path], path))
    for path, content in sorted(combined.items()):
        target = root.joinpath(*PurePosixPath(path).parts)
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(content)


def overlay_navigation(root, sources):
    overlay_files(root, [("Navigation", sources)])


def hash_tree(root):
    files = {}
    root_manifest = root / "manifest.json"
    for path in sorted(p for p in root.rglob("*") if p.is_file() and p != root_manifest):
        relative = path.relative_to(root).as_posix()
        files[relative] = sha256(path.read_bytes())
    digest = hashlib.sha256()
    for relative, value in sorted(files.items()):
        digest.update(relative.encode("utf-8")); digest.update(b"\0")
        digest.update(value.encode("ascii")); digest.update(b"\n")
    return files, digest.hexdigest()


def compile_sources(root, managed, csc):
    managed = Path(managed).resolve()
    csc = Path(csc).resolve()
    if not managed.is_dir() or not (managed / "Assembly-CSharp.dll").is_file():
        raise BuildError("--managed must be KSP Data/Managed and contain Assembly-CSharp.dll")
    if not csc.is_file():
        raise BuildError("--csc must point to Roslyn csc.dll")
    refs = sorted(managed.glob("*.dll"))
    sources = sorted((root / "Source").rglob("*.cs"))
    if not refs or not sources:
        raise BuildError("No managed references or C# sources found")
    output = root / "GameData/MegaCrew/Plugins/MegaCrew.dll"
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="megacrew-csc-") as temp:
        response = Path(temp) / "compile.rsp"
        args = ["-nologo", "-target:library", "-nostdlib+", "-langversion:7.3",
                '-out:"{}"'.format(output)]
        args.extend('-reference:"{}"'.format(reference) for reference in refs)
        args.extend('"{}"'.format(source) for source in sources)
        response.write_text("\n".join(args) + "\n", encoding="utf-8")
        try:
            subprocess.run(["dotnet", str(csc), "@" + str(response)], cwd=str(root), check=True)
        except subprocess.CalledProcessError as exc:
            raise BuildError("C# 7.3 compilation failed with exit code {}".format(exc.returncode))
    inputs = {
        "compiler_path": str(csc),
        "compiler_sha256": sha256(csc.read_bytes()),
        "managed_path": str(managed),
        "managed_assemblies": {p.name: sha256(p.read_bytes()) for p in refs},
    }
    return output, inputs


def ensure_new_destination(path, repo=None):
    path = Path(path)
    if os.path.lexists(str(path)):
        raise BuildError("Output already exists; refusing to overwrite: {}".format(path))
    path = path.resolve()
    if os.path.lexists(str(path)):
        raise BuildError("Output already exists; refusing to overwrite: {}".format(path))
    if not path.parent.exists() or not path.parent.is_dir():
        raise BuildError("Output parent must already exist: {}".format(path.parent))
    if repo is not None:
        repo = Path(repo).resolve()
        if path == repo or repo in path.parents:
            raise BuildError("Output must be outside the source checkout")
    path.mkdir()
    return path


def collect_inputs(repo, upstream_ref, navigation_ref=None, patch_sequence=PATCH_SEQUENCE,
                   expected_zip_sha256=EXPECTED_ZIP_SHA256, operations_ref=None):
    """Read and verify every pinned input before creating an output directory."""
    if not patch_sequence:
        raise BuildError("Patch sequence must contain at least one patch")
    upstream_commit = resolve_commit(repo, upstream_ref)
    nav_commit = resolve_commit(repo, navigation_ref) if navigation_ref is not None else None
    zip_bytes = git_blob(repo, upstream_commit, ZIP_PATH)
    zip_hash = verify_sha256(zip_bytes, expected_zip_sha256, ZIP_PATH)

    patches = []
    provenance_names = set()
    for index, spec in enumerate(patch_sequence, 1):
        try:
            ref = spec["ref"]
            path = spec["path"]
            expected = spec["expected_sha256"]
        except (KeyError, TypeError) as exc:
            raise BuildError("Invalid patch sequence entry {}: {}".format(index, exc))
        artifact_name = PurePosixPath(path).name
        if (not artifact_name or artifact_name in (".", "..") or
                artifact_name in provenance_names or "\\" in path):
            raise BuildError("Invalid or duplicate patch artifact path: {!r}".format(path))
        provenance_names.add(artifact_name)
        commit = resolve_commit(repo, ref)
        patch_bytes = git_blob(repo, commit, path)
        patch_hash = verify_sha256(patch_bytes, expected, path)
        patches.append({
            "index": index,
            "ref": ref,
            "commit": commit,
            "path": path,
            "artifact_name": artifact_name,
            "sha256": patch_hash,
            "expected_sha256": expected,
            "bytes": patch_bytes,
        })

    nav_files = navigation_sources(repo, nav_commit) if nav_commit is not None else None
    nav_test_files = navigation_tests(repo, nav_commit) if nav_commit is not None else None
    operations_commit = None
    operations = None
    if operations_ref is not None:
        operations_commit = resolve_commit(repo, operations_ref)
        operations = operations_files(repo, operations_commit)
    return {
        "upstream_commit": upstream_commit,
        "zip_bytes": zip_bytes,
        "zip_sha256": zip_hash,
        "patches": patches,
        "navigation_commit": nav_commit,
        "navigation_files": nav_files,
        "navigation_tests": nav_test_files,
        "operations_ref": operations_ref,
        "operations_commit": operations_commit,
        "operations": operations,
    }


def build(args, patch_sequence=None, expected_zip_sha256=EXPECTED_ZIP_SHA256):
    repo = Path(args.repo).resolve()
    if not repo.is_dir() or not (repo / ".git").exists():
        raise BuildError("--repo must be a Git checkout")
    integration_ref = getattr(args, "integration_ref", None)
    explicit_navigation_ref = getattr(args, "navigation_ref", None)
    operations_ref = getattr(args, "operations_ref", None)
    if integration_ref is not None:
        if explicit_navigation_ref is not None:
            raise BuildError("--integration-ref cannot be combined with an explicit --navigation-ref")
        if operations_ref is not None:
            raise BuildError("--integration-ref cannot be combined with --operations-ref")
        navigation_ref = None
        integration_commit = resolve_commit(repo, integration_ref)
        integration_snapshot = integration_snapshot_files(repo, integration_commit)
    else:
        navigation_ref = explicit_navigation_ref or "294c667"
        integration_commit = None
        integration_snapshot = None

    inputs = collect_inputs(repo, args.upstream_ref, navigation_ref,
                            PATCH_SEQUENCE if patch_sequence is None else patch_sequence,
                            expected_zip_sha256,
                            operations_ref=None if integration_commit is not None else operations_ref)

    output = ensure_new_destination(args.output, repo)
    try:
        # Preserve every verified input before applying its patch.
        provenance = output / "provenance"
        provenance.mkdir()
        (provenance / ZIP_PATH).write_bytes(inputs["zip_bytes"])
        for patch in inputs["patches"]:
            (provenance / patch["artifact_name"]).write_bytes(patch["bytes"])
        count = safe_extract(inputs["zip_bytes"], output)
        for patch in inputs["patches"]:
            apply_patch_once(output, patch["bytes"], patch["path"], patch["index"])
        baseline_source_hashes = source_tree_hashes(output) if integration_commit is not None else None
        if integration_commit is not None:
            replace_snapshot_trees(output, integration_snapshot["files"])
            candidate_dll = output / "GameData/MegaCrew/Plugins/MegaCrew.dll"
            if candidate_dll.exists() or candidate_dll.is_symlink():
                raise BuildError("Full snapshot retained a baseline MegaCrew DLL; compilation must be fresh")
        else:
            overlay_groups = [
                ("Navigation source", inputs["navigation_files"]),
                ("Navigation tests", inputs["navigation_tests"]),
            ]
            if inputs["operations"] is not None:
                overlay_groups.extend([
                    ("Operations source", inputs["operations"]["sources"]),
                    ("Operations tests", inputs["operations"]["tests"]),
                ])
                if inputs["operations"]["docking"] is not None:
                    overlay_groups.append(("Docking instruments", inputs["operations"]["docking"]))
            overlay_files(output, overlay_groups)
        compiled = False
        compiler_inputs = None
        if not args.prepare_only:
            if not args.managed or not args.csc:
                raise BuildError("--managed and --csc are required unless --prepare-only is set")
            _, compiler_inputs = compile_sources(output, args.managed, args.csc)
            compiled = True
        integration_snapshot_hashes = None
        canonical_source_deviations = None
        excluded_tracked_dlls = None
        if integration_commit is not None:
            integration_snapshot_hashes = {
                path: sha256(data) for path, data in sorted(integration_snapshot["files"].items())
            }
            snapshot_sources = {path: value for path, value in integration_snapshot_hashes.items()
                                if path.startswith("Source/")}
            canonical_source_deviations = {}
            for path in sorted(set(baseline_source_hashes) | set(snapshot_sources)):
                before = baseline_source_hashes.get(path)
                after = snapshot_sources.get(path)
                if before != after:
                    canonical_source_deviations[path] = {
                        "status": ("added" if before is None else "removed" if after is None else "modified"),
                        "canonical_sha256": before,
                        "integration_sha256": after,
                    }
            excluded_tracked_dlls = integration_snapshot["excluded_dlls"]
        docking_group_manifest = ({
            "status": "from_full_snapshot",
            "ref": integration_ref,
            "commit": integration_commit,
            "files": {path: integration_snapshot_hashes[path] for path in DOCKING_GROUP_PATHS
                      if path in integration_snapshot_hashes},
        } if integration_commit is not None else {
            "status": ("present" if inputs["operations"] is not None and
                       inputs["operations"]["docking"] is not None else "absent"),
            "ref": inputs["operations_ref"],
            "commit": inputs["operations_commit"],
            "files": ({path: sha256(data) for path, data in sorted(inputs["operations"]["docking"].items())}
                      if inputs["operations"] is not None and
                      inputs["operations"]["docking"] is not None else None),
        })
        files, output_hash = hash_tree(output)
        manifest = {
            "tool": "megacrew-integration-builder",
            "mode": "full_git_snapshot" if integration_commit is not None else "legacy_allowlist",
            "builder_sha256": sha256(Path(__file__).read_bytes()),
            "upstream_ref": args.upstream_ref,
            "upstream_commit": inputs["upstream_commit"],
            "navigation_ref": navigation_ref,
            "navigation_commit": inputs["navigation_commit"],
            "navigation_tests": {
                "ref": navigation_ref,
                "commit": inputs["navigation_commit"],
                "files": ({path: sha256(data) for path, data in sorted(inputs["navigation_tests"].items())}
                          if inputs["navigation_tests"] is not None else None),
            },
            "operations_ref": inputs["operations_ref"],
            "operations_commit": inputs["operations_commit"],
            "inputs": {
                ZIP_PATH: {
                    "sha256": inputs["zip_sha256"],
                    "expected_sha256": expected_zip_sha256,
                },
            },
            "patch_sequence": [{key: patch[key] for key in
                                ("index", "ref", "commit", "path", "sha256", "expected_sha256")}
                               for patch in inputs["patches"]],
            "zip_member_count": count,
            "navigation_files": ({path: sha256(data) for path, data in sorted(inputs["navigation_files"].items())}
                                 if inputs["navigation_files"] is not None else None),
            "operations_sources": ({path: sha256(data) for path, data in sorted(inputs["operations"]["sources"].items())}
                                   if inputs["operations"] is not None else None),
            "operations_tests": ({path: sha256(data) for path, data in sorted(inputs["operations"]["tests"].items())}
                                 if inputs["operations"] is not None else None),
            "integration_ref": integration_ref,
            "integration_commit": integration_commit,
            "integration_snapshot_files": integration_snapshot_hashes,
            "integration_excluded_tracked_dlls": excluded_tracked_dlls,
            "integration_excluded_historical_metadata": (
                integration_snapshot["excluded_historical_metadata"]
                if integration_commit is not None else None),
            "canonical_source_deviations": canonical_source_deviations,
            "docking_group": docking_group_manifest,
            "compiled": compiled,
            "compiler_language_version": "7.3" if compiled else None,
            "compiler_inputs": compiler_inputs,
            "files": files,
            "output_tree_sha256_excluding_manifest": output_hash,
        }
        (output / "manifest.json").write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        return output
    except Exception:
        shutil.rmtree(output, ignore_errors=True)
        raise


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", required=True, help="Git checkout to read (no writes)")
    parser.add_argument("--upstream-ref", default="f76e437", help="Pinned upstream artifact ref (default: f76e437)")
    parser.add_argument("--navigation-ref", help="Explicit navigation source ref (legacy overlay mode; default: 294c667)")
    parser.add_argument("--operations-ref", help="Optional explicit commit/ref for reviewed onboard operations addons and focused tests; no default")
    parser.add_argument("--integration-ref", help="Build a complete package snapshot from this immutable Git ref; incompatible with explicit navigation/operations overlays")
    parser.add_argument("--output", required=True, type=Path, help="New output directory; must not exist")
    parser.add_argument("--managed", type=Path, help="KSP Data/Managed directory")
    parser.add_argument("--csc", type=Path, help="Roslyn csc.dll")
    parser.add_argument("--prepare-only", action="store_true", help="Reconcile sources without KSP compilation")
    args = parser.parse_args(argv)
    try:
        output = build(args)
    except (BuildError, subprocess.CalledProcessError, OSError, zipfile.BadZipFile) as exc:
        print("ERROR: {}".format(exc), file=sys.stderr)
        return 2
    print("Prepared {}".format(output))
    print("Manifest: {}".format(output / "manifest.json"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
