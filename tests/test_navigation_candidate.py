import io
import json
import os
from pathlib import Path
import stat
import subprocess
import tempfile
import unittest
import zipfile
from unittest import mock

import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
import build_navigation_candidate as builder


def make_zip(entries):
    stream = io.BytesIO()
    with zipfile.ZipFile(stream, "w") as archive:
        for info, data in entries:
            archive.writestr(info, data)
    return stream.getvalue()


class BuilderSafetyTests(unittest.TestCase):
    def test_broken_output_symlink_is_rejected_before_resolving(self):
        with tempfile.TemporaryDirectory() as temp:
            temp = Path(temp)
            target = temp / "missing-target"
            link = temp / "output-link"
            link.symlink_to(target, target_is_directory=True)
            with self.assertRaises(builder.BuildError):
                builder.ensure_new_destination(link)
            self.assertTrue(link.is_symlink())
            self.assertFalse(target.exists())

    def test_bad_artifact_hash_rejected(self):
        with self.assertRaisesRegex(builder.BuildError, "SHA-256 mismatch"):
            builder.verify_sha256(b"not the pinned artifact", "0" * 64, "artifact")

    def test_traversal_member_rejected(self):
        unsafe = zipfile.ZipInfo("../escape.txt")
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp) / "out"
            root.mkdir()
            with self.assertRaisesRegex(builder.BuildError, "Unsafe ZIP member"):
                builder.safe_extract(make_zip([(unsafe, b"bad")]), root)
            self.assertFalse((Path(temp) / "escape.txt").exists())

    def test_absolute_windows_and_backslash_paths_rejected(self):
        for name in ("/absolute.txt", "C:/drive.txt", "folder\\escape.txt", "a//b.txt", "a/./b.txt"):
            with self.subTest(name=name), tempfile.TemporaryDirectory() as temp:
                root = Path(temp) / "out"
                root.mkdir()
                with self.assertRaises(builder.BuildError):
                    builder.safe_extract(make_zip([(zipfile.ZipInfo(name), b"bad")]), root)

    def test_symlink_member_rejected(self):
        info = zipfile.ZipInfo("link")
        info.create_system = 3
        info.external_attr = (stat.S_IFLNK | 0o777) << 16
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp) / "out"
            root.mkdir()
            with self.assertRaisesRegex(builder.BuildError, "symlinks"):
                builder.safe_extract(make_zip([(info, b"target")]), root)
            self.assertFalse((root / "link").exists())

    def test_archive_extracts_at_output_root(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp) / "out"
            root.mkdir()
            archive = make_zip([
                (zipfile.ZipInfo("README.md"), b"readme"),
                (zipfile.ZipInfo("Source/Example.cs"), b"source"),
            ])
            count = builder.safe_extract(archive, root)
            self.assertEqual(count, 2)
            self.assertEqual((root / "README.md").read_bytes(), b"readme")
            self.assertEqual((root / "Source/Example.cs").read_bytes(), b"source")
            self.assertFalse((root / "Hermes-Practice-v0.3.0/README.md").exists())

    def test_extract_refuses_overwrite_and_preserves_existing_file(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp) / "out"
            root.mkdir()
            existing = root / "README.md"
            existing.write_text("keep me")
            with self.assertRaisesRegex(builder.BuildError, "overwrite"):
                builder.safe_extract(make_zip([(zipfile.ZipInfo("README.md"), b"replace")]), root)
            self.assertEqual(existing.read_text(), "keep me")

    def test_symlinked_parent_rejected_before_creating_outside_directory(self):
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp)
            root = base / "out"
            outside = base / "outside"
            root.mkdir()
            outside.mkdir()
            (root / "linked").symlink_to(outside, target_is_directory=True)
            archive = make_zip([(zipfile.ZipInfo("linked/new/file.txt"), b"bad")])
            with self.assertRaisesRegex(builder.BuildError, "escaped the output root"):
                builder.safe_extract(archive, root)
            self.assertFalse((outside / "new").exists())

    def test_existing_output_directory_is_not_overwritten(self):
        with tempfile.TemporaryDirectory() as temp:
            existing = Path(temp) / "output"
            existing.mkdir()
            sentinel = existing / "sentinel"
            sentinel.write_text("keep")
            with self.assertRaisesRegex(builder.BuildError, "already exists"):
                builder.ensure_new_destination(existing)
            self.assertEqual(sentinel.read_text(), "keep")

    def test_relative_destination_is_resolved_before_creation(self):
        with tempfile.TemporaryDirectory() as temp:
            previous = os.getcwd()
            try:
                os.chdir(temp)
                output = builder.ensure_new_destination(Path("relative-out"))
                self.assertTrue(output.is_absolute())
                self.assertEqual(output, (Path(temp) / "relative-out").resolve())
            finally:
                os.chdir(previous)

    def test_output_inside_checkout_is_rejected_without_creation(self):
        with tempfile.TemporaryDirectory() as temp:
            repo = Path(temp) / "checkout"
            repo.mkdir()
            output = repo / "candidate"
            with self.assertRaisesRegex(builder.BuildError, "outside the source checkout"):
                builder.ensure_new_destination(output, repo)
            self.assertFalse(output.exists())

    def test_tree_hash_excludes_only_root_manifest(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root / "manifest.json").write_text("root manifest")
            (root / "nested").mkdir()
            nested_manifest = root / "nested" / "manifest.json"
            nested_manifest.write_text("nested manifest")
            files, digest = builder.hash_tree(root)
            self.assertNotIn("manifest.json", files)
            self.assertIn("nested/manifest.json", files)
            self.assertEqual(files["nested/manifest.json"], builder.sha256(b"nested manifest"))
            self.assertTrue(digest)

    def test_operations_overlay_preflights_collisions_without_overwrite_or_partial_write(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            existing = root / "Source" / "OnboardEngineering.cs"
            existing.parent.mkdir()
            existing.write_text("preserve published core")
            with self.assertRaisesRegex(builder.BuildError, "would overwrite an existing path"):
                builder.overlay_files(root, [("Operations", {
                    "Source/OnboardEngineering.cs": b"replacement",
                    "Source/OnboardOperations.cs": b"must not be written",
                })])
            self.assertEqual(existing.read_text(), "preserve published core")
            self.assertFalse((root / "Source" / "OnboardOperations.cs").exists())

    def test_missing_operations_blob_fails_before_destination_creation(self):
        with tempfile.TemporaryDirectory() as temp:
            temp = Path(temp)
            repo = temp / "fixture-repo"
            repo.mkdir()
            subprocess.run(["git", "init", "--quiet", str(repo)], check=True)
            subprocess.run(["git", "-C", str(repo), "config", "user.name", "Fixture"], check=True)
            subprocess.run(["git", "-C", str(repo), "config", "user.email", "fixture@example.invalid"], check=True)

            archive = make_zip([
                (zipfile.ZipInfo("README.md"), b"fixture base\n"),
                (zipfile.ZipInfo("Source/MegaCrew.cs"), b"published core\n"),
            ])
            (repo / builder.ZIP_PATH).write_bytes(archive)
            (repo / "stage.patch").write_bytes(("""
diff --git a/README.md b/README.md
--- a/README.md
+++ b/README.md
@@ -1 +1 @@
-fixture base
+fixture patched
""").lstrip().encode("utf-8"))

            for rel in builder.NAVIGATION_TEST_PATHS:
                target = repo / rel
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_text("// nav test fixture: " + rel + "\n")
            nav_source = repo / "Source" / "Navigation" / "Fixture.cs"
            nav_source.parent.mkdir(parents=True)
            nav_source.write_text("// fixture navigation source\n")
            for rel in builder.OPERATIONS_SOURCE_PATHS + builder.OPERATIONS_TEST_PATHS:
                target = repo / rel
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_text("// operations fixture: " + rel + "\n")
            (repo / "Source" / "MegaCrew.cs").write_text("untrusted core from operations ref\n")

            subprocess.run(["git", "-C", str(repo), "add", "."], check=True)
            subprocess.run([
                "git", "-C", str(repo), "-c", "commit.gpgsign=false",
                "-c", "core.hooksPath=/dev/null", "commit", "--quiet", "-m", "fixture inputs",
            ], check=True)
            fixture_ref = subprocess.run(
                ["git", "-C", str(repo), "rev-parse", "HEAD"], check=True,
                stdout=subprocess.PIPE, text=True,
            ).stdout.strip()

            old_ops_args = type("Args", (), {
                "repo": repo, "upstream_ref": fixture_ref, "navigation_ref": fixture_ref,
                "operations_ref": fixture_ref, "output": temp / "old-operations-ref",
                "prepare_only": True, "managed": None, "csc": None,
            })()
            old_ops = builder.build(old_ops_args,
                                    patch_sequence=({
                                        "ref": fixture_ref, "path": "stage.patch",
                                        "expected_sha256": builder.sha256((repo / "stage.patch").read_bytes()),
                                    },),
                                    expected_zip_sha256=builder.sha256(archive))
            old_manifest = json.loads((old_ops / "manifest.json").read_text())
            self.assertEqual(old_manifest["docking_group"]["status"], "absent")
            self.assertIsNone(old_manifest["docking_group"]["files"])
            self.assertFalse((old_ops / "Source/OnboardDockingInstruments.cs").exists())

            empty_ref_args = type("Args", (), {
                "repo": repo, "upstream_ref": fixture_ref, "navigation_ref": fixture_ref,
                "operations_ref": "", "output": temp / "empty-operations-ref",
                "prepare_only": True, "managed": None, "csc": None,
            })()
            with self.assertRaisesRegex(builder.BuildError, "Invalid Git ref"):
                builder.build(empty_ref_args,
                              patch_sequence=({
                                  "ref": fixture_ref, "path": "stage.patch",
                                  "expected_sha256": builder.sha256((repo / "stage.patch").read_bytes()),
                              },),
                              expected_zip_sha256=builder.sha256(archive))
            self.assertFalse(empty_ref_args.output.exists())

            missing = repo / "Source" / "OnboardEngineering.cs"
            missing.unlink()
            subprocess.run(["git", "-C", str(repo), "add", "-u"], check=True)
            subprocess.run([
                "git", "-C", str(repo), "-c", "commit.gpgsign=false",
                "-c", "core.hooksPath=/dev/null", "commit", "--quiet", "-m", "remove allowed blob",
            ], check=True)
            missing_ref = subprocess.run(
                ["git", "-C", str(repo), "rev-parse", "HEAD"], check=True,
                stdout=subprocess.PIPE, text=True,
            ).stdout.strip()
            args = type("Args", (), {
                "repo": repo, "upstream_ref": fixture_ref, "navigation_ref": fixture_ref,
                "operations_ref": missing_ref, "output": temp / "must-not-exist",
                "prepare_only": True, "managed": None, "csc": None,
            })()
            with self.assertRaisesRegex(builder.BuildError, "Missing Git object"):
                builder.build(args,
                              patch_sequence=({
                                  "ref": fixture_ref, "path": "stage.patch",
                                  "expected_sha256": builder.sha256((repo / "stage.patch").read_bytes()),
                              },),
                              expected_zip_sha256=builder.sha256(archive))
            self.assertFalse(args.output.exists())

    def test_duplicate_and_case_colliding_members_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp) / "out"
            root.mkdir()
            archive = make_zip([
                (zipfile.ZipInfo("Source/A.cs"), b"one"),
                (zipfile.ZipInfo("source/a.cs"), b"two"),
            ])
            with self.assertRaisesRegex(builder.BuildError, "Duplicate/case-colliding"):
                builder.safe_extract(archive, root)

    def test_ordered_patch_sequence_is_verified_and_applied_before_overlay(self):
        with tempfile.TemporaryDirectory() as temp:
            temp = Path(temp)
            repo = temp / "fixture-repo"
            repo.mkdir()
            subprocess.run(["git", "init", "--quiet", str(repo)], check=True)
            subprocess.run(["git", "-C", str(repo), "config", "user.name", "Fixture"], check=True)
            subprocess.run(["git", "-C", str(repo), "config", "user.email", "fixture@example.invalid"], check=True)

            archive = make_zip([
                (zipfile.ZipInfo("README.md"), b"fixture base\n"),
                (zipfile.ZipInfo("Source/MegaCrew.cs"), b"published core\n"),
            ])
            first_patch = (
                "diff --git a/Source/Sequence.txt b/Source/Sequence.txt\n"
                "new file mode 100644\n"
                "--- /dev/null\n"
                "+++ b/Source/Sequence.txt\n"
                "@@ -0,0 +1 @@\n"
                "+stage one\n"
            ).encode("utf-8")
            second_patch = (
                "diff --git a/Source/Sequence.txt b/Source/Sequence.txt\n"
                "--- a/Source/Sequence.txt\n"
                "+++ b/Source/Sequence.txt\n"
                "@@ -1 +1 @@\n"
                "-stage one\n"
                "+stage two\n"
            ).encode("utf-8")
            (repo / builder.ZIP_PATH).write_bytes(archive)
            (repo / "first.patch").write_bytes(first_patch)
            (repo / "second.patch").write_bytes(second_patch)
            nav = repo / "Source" / "Navigation" / "Fixture.cs"
            nav.parent.mkdir(parents=True)
            nav.write_text("// fixture navigation source\n")
            for rel in builder.NAVIGATION_TEST_PATHS:
                target = repo / rel
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_text("// nav test fixture: " + rel + "\n")
            for rel in builder.OPERATIONS_SOURCE_PATHS + builder.OPERATIONS_TEST_PATHS:
                target = repo / rel
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_text("// operations fixture: " + rel + "\n")
            for rel in builder.DOCKING_GROUP_PATHS:
                target = repo / rel
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_text("// docking fixture: " + rel + "\n")
            # The source ref contains a different core file, but the operations
            # allowlist must never copy it over the published package.
            (repo / "Source" / "MegaCrew.cs").write_text("untrusted core from operations ref\n")
            subprocess.run(["git", "-C", str(repo), "add", "."], check=True)
            subprocess.run([
                "git", "-C", str(repo), "-c", "commit.gpgsign=false",
                "-c", "core.hooksPath=/dev/null", "commit", "--quiet", "-m", "fixture inputs",
            ], check=True)
            fixture_ref = subprocess.run(
                ["git", "-C", str(repo), "rev-parse", "HEAD"], check=True,
                stdout=subprocess.PIPE, text=True,
            ).stdout.strip()

            patch_sequence = (
                {"ref": fixture_ref, "path": "first.patch", "expected_sha256": builder.sha256(first_patch)},
                {"ref": fixture_ref, "path": "second.patch", "expected_sha256": builder.sha256(second_patch)},
            )
            output = temp / "candidate"
            args = type("Args", (), {
                "repo": repo, "upstream_ref": fixture_ref, "navigation_ref": fixture_ref,
                "operations_ref": fixture_ref,
                "output": output, "prepare_only": True, "managed": None, "csc": None,
            })()

            # A bad later hash is rejected before the builder creates its destination.
            bad_sequence = (patch_sequence[0], dict(patch_sequence[1], expected_sha256="0" * 64))
            bad_args = type("Args", (), {
                "repo": repo, "upstream_ref": fixture_ref, "navigation_ref": fixture_ref,
                "operations_ref": fixture_ref,
                "output": temp / "rejected", "prepare_only": True, "managed": None, "csc": None,
            })()
            with self.assertRaisesRegex(builder.BuildError, "SHA-256 mismatch"):
                builder.build(bad_args, patch_sequence=bad_sequence,
                              expected_zip_sha256=builder.sha256(archive))
            self.assertFalse(bad_args.output.exists())

            built = builder.build(args, patch_sequence=patch_sequence,
                                  expected_zip_sha256=builder.sha256(archive))
            self.assertEqual((built / "Source" / "Sequence.txt").read_text(), "stage two\n")
            manifest = json.loads((built / "manifest.json").read_text())
            self.assertEqual([item["path"] for item in manifest["patch_sequence"]],
                             ["first.patch", "second.patch"])
            self.assertEqual([item["index"] for item in manifest["patch_sequence"]], [1, 2])
            self.assertEqual([item["sha256"] for item in manifest["patch_sequence"]],
                             [builder.sha256(first_patch), builder.sha256(second_patch)])
            self.assertEqual([item["expected_sha256"] for item in manifest["patch_sequence"]],
                             [builder.sha256(first_patch), builder.sha256(second_patch)])
            self.assertEqual([item["commit"] for item in manifest["patch_sequence"]],
                             [fixture_ref, fixture_ref])
            self.assertEqual(manifest["inputs"][builder.ZIP_PATH]["sha256"], builder.sha256(archive))
            self.assertEqual(manifest["operations_ref"], fixture_ref)
            self.assertEqual(manifest["operations_commit"], fixture_ref)
            self.assertEqual(set(manifest["operations_sources"]), set(builder.OPERATIONS_SOURCE_PATHS))
            self.assertEqual(set(manifest["operations_tests"]), set(builder.OPERATIONS_TEST_PATHS))
            self.assertEqual(manifest["docking_group"]["status"], "present")
            self.assertEqual(manifest["docking_group"]["ref"], fixture_ref)
            self.assertEqual(manifest["docking_group"]["commit"], fixture_ref)
            self.assertEqual(set(manifest["docking_group"]["files"]), set(builder.DOCKING_GROUP_PATHS))
            self.assertEqual(set(manifest["navigation_tests"]["files"]), set(builder.NAVIGATION_TEST_PATHS))
            self.assertEqual((built / "Source" / "MegaCrew.cs").read_text(), "published core\n")
            for rel in builder.OPERATIONS_SOURCE_PATHS + builder.OPERATIONS_TEST_PATHS:
                self.assertEqual((built / rel).read_text(), "// operations fixture: " + rel + "\n")
            for rel in builder.DOCKING_GROUP_PATHS:
                self.assertEqual((built / rel).read_text(), "// docking fixture: " + rel + "\n")
            for rel in builder.NAVIGATION_TEST_PATHS:
                self.assertEqual((built / rel).read_text(), "// nav test fixture: " + rel + "\n")
            nav_only_args = type("Args", (), {
                "repo": repo, "upstream_ref": fixture_ref, "navigation_ref": fixture_ref,
                "output": temp / "nav-only", "prepare_only": True, "managed": None, "csc": None,
            })()
            nav_only = builder.build(nav_only_args, patch_sequence=patch_sequence,
                                     expected_zip_sha256=builder.sha256(archive))
            nav_only_manifest = json.loads((nav_only / "manifest.json").read_text())
            self.assertIsNone(nav_only_manifest["operations_ref"])
            self.assertIsNone(nav_only_manifest["operations_sources"])
            self.assertIsNone(nav_only_manifest["operations_tests"])
            self.assertTrue((nav_only / "tests/navigation/run.py").is_file())
            self.assertFalse((nav_only / "Source/OnboardOperations.cs").exists())
            self.assertTrue((built / "provenance" / "first.patch").is_file())
            self.assertTrue((built / "provenance" / "second.patch").is_file())

            # A ref with only six of the seven optional paths is not treated as
            # an older operations ref and fails before creating its output.
            (repo / builder.DOCKING_GROUP_PATHS[0]).unlink()
            subprocess.run(["git", "-C", str(repo), "add", "-u"], check=True)
            subprocess.run([
                "git", "-C", str(repo), "-c", "commit.gpgsign=false",
                "-c", "core.hooksPath=/dev/null", "commit", "--quiet", "-m", "partial docking group",
            ], check=True)
            partial_ref = subprocess.run(
                ["git", "-C", str(repo), "rev-parse", "HEAD"], check=True,
                stdout=subprocess.PIPE, text=True,
            ).stdout.strip()
            partial_args = type("Args", (), {
                "repo": repo, "upstream_ref": fixture_ref, "navigation_ref": fixture_ref,
                "operations_ref": partial_ref, "output": temp / "partial-docking",
                "prepare_only": True, "managed": None, "csc": None,
            })()
            with self.assertRaisesRegex(builder.BuildError, "Incomplete optional docking group"):
                builder.build(partial_args, patch_sequence=patch_sequence,
                              expected_zip_sha256=builder.sha256(archive))
            self.assertFalse(partial_args.output.exists())
            repo_status = subprocess.run(
                ["git", "-C", str(repo), "status", "--porcelain"], check=True,
                stdout=subprocess.PIPE, text=True,
            ).stdout
            self.assertEqual(repo_status, "")


class IntegrationSnapshotTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="navigation-full-snapshot-")
        self.base = Path(self.temp.name)
        self.repo = self.base / "repo"
        self.repo.mkdir()
        subprocess.run(["git", "init", "--quiet", str(self.repo)], check=True)
        subprocess.run(["git", "-C", str(self.repo), "config", "user.name", "Fixture"], check=True)
        subprocess.run(["git", "-C", str(self.repo), "config", "user.email",
                        "fixture@example.invalid"], check=True)

        archive = make_zip([
            (zipfile.ZipInfo("README.md"), b"canonical README\n"),
            (zipfile.ZipInfo("Source/MegaCrew.cs"), b"canonical source\n"),
            (zipfile.ZipInfo("Source/Stale.cs"), b"stale source\n"),
            (zipfile.ZipInfo("GameData/MegaCrew/Plugins/MegaCrew.dll"), b"old plugin bytes"),
            (zipfile.ZipInfo("docs/stale.md"), b"stale docs\n"),
            (zipfile.ZipInfo("tests/stale.py"), b"stale tests\n"),
            (zipfile.ZipInfo("tools/stale.py"), b"stale tools\n"),
            (zipfile.ZipInfo("Ships/stale.craft"), b"stale craft\n"),
            (zipfile.ZipInfo("Missions/stale/old.json"), b"stale mission\n"),
        ])
        (self.repo / builder.ZIP_PATH).write_bytes(archive)
        self.archive = archive
        self.patch = (
            "diff --git a/README.md b/README.md\n"
            "--- a/README.md\n"
            "+++ b/README.md\n"
            "@@ -1 +1 @@\n"
            "-canonical README\n"
            "+patched base README\n"
        ).encode("utf-8")
        patch_path = self.repo / builder.PATCH_SEQUENCE[0]["path"]
        patch_path.write_bytes(self.patch)

        snapshot = {
            "README.md": b"snapshot README\n",
            "dependencies.json": b'{"dependencies":[]}\n',
            "visual-profile.json": b'{"profiles":[]}\n',
            "Source/MegaCrew.cs": b"snapshot source core\n",
            "Source/New.cs": b"snapshot new source\n",
            "Source/Navigation/Fixture.cs": b"// navigation fixture\n",
            "Source/OnboardDockingInstruments.cs": b"snapshot docking instruments\n",
            "docs/current.md": b"snapshot docs\n",
            "tools/current.py": b"snapshot tools\n",
            "tests/current.py": b"snapshot tests\n",
            "Ships/VAB/current.craft": b"snapshot craft\n",
            "Missions/Duna/mission.json": b'{"mission":"duna"}\n',
            "GameData/MegaCrew/CrewSystems.cfg": b"snapshot config\n",
            "GameData/MegaCrew/Plugins/MegaCrew.dll": b"stale tracked dll must not ship",
        }
        for relative in builder.NAVIGATION_TEST_PATHS:
            snapshot[relative] = b"// navigation test fixture\n"
        for relative, data in snapshot.items():
            path = self.repo / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
        (self.repo / "integration-manifest.json").write_text(
            '{"historical":"not the new stage report"}\n', encoding="utf-8")
        self.snapshot = snapshot
        subprocess.run(["git", "-C", str(self.repo), "add", "."], check=True)
        subprocess.run([
            "git", "-C", str(self.repo), "-c", "commit.gpgsign=false",
            "-c", "core.hooksPath=/dev/null", "commit", "--quiet", "-m", "snapshot fixture",
        ], check=True)
        self.ref = subprocess.run(["git", "-C", str(self.repo), "rev-parse", "HEAD"], check=True,
                                  stdout=subprocess.PIPE, text=True).stdout.strip()
        self.patch_sequence = ({
            "ref": self.ref,
            "path": builder.PATCH_SEQUENCE[0]["path"],
            "expected_sha256": builder.sha256(self.patch),
        },)

    def tearDown(self):
        self.temp.cleanup()

    def args(self, output, **overrides):
        values = {
            "repo": self.repo,
            "upstream_ref": self.ref,
            "navigation_ref": None,
            "operations_ref": None,
            "integration_ref": self.ref,
            "output": output,
            "prepare_only": True,
            "managed": None,
            "csc": None,
        }
        values.update(overrides)
        return type("Args", (), values)()

    def test_full_snapshot_replaces_roots_and_never_keeps_baseline_dll(self):
        # A dirty checkout must not bleed into this immutable-ref build.
        (self.repo / "Source/MegaCrew.cs").write_text("uncommitted poison\n", encoding="utf-8")
        args = self.args(self.base / "candidate")
        built = builder.build(args, patch_sequence=self.patch_sequence,
                              expected_zip_sha256=builder.sha256(self.archive))

        self.assertEqual((built / "Source/MegaCrew.cs").read_bytes(), self.snapshot["Source/MegaCrew.cs"])
        self.assertTrue((built / "Source/New.cs").is_file())
        self.assertFalse((built / "Source/Stale.cs").exists())
        self.assertFalse((built / "GameData/MegaCrew/Plugins/MegaCrew.dll").exists())
        self.assertEqual((built / "GameData/MegaCrew/CrewSystems.cfg").read_bytes(),
                         self.snapshot["GameData/MegaCrew/CrewSystems.cfg"])
        self.assertFalse((built / "docs/stale.md").exists())
        self.assertFalse((built / "tests/stale.py").exists())
        self.assertFalse((built / "tools/stale.py").exists())
        self.assertFalse((built / "Ships/stale.craft").exists())
        self.assertTrue((built / "Missions/Duna/mission.json").is_file())
        self.assertFalse((built / "Missions/stale/old.json").exists())
        self.assertEqual((built / "README.md").read_bytes(), self.snapshot["README.md"])

        manifest = json.loads((built / "manifest.json").read_text())
        self.assertEqual(manifest["mode"], "full_git_snapshot")
        self.assertEqual(manifest["integration_ref"], self.ref)
        self.assertEqual(manifest["integration_commit"], self.ref)
        self.assertFalse(manifest["compiled"])
        self.assertEqual(manifest["integration_snapshot_files"], {
            path: builder.sha256(data) for path, data in self.snapshot.items()
            if not path.lower().endswith(".dll")
        })
        self.assertEqual(manifest["integration_excluded_tracked_dlls"], {
            "GameData/MegaCrew/Plugins/MegaCrew.dll": builder.sha256(
                self.snapshot["GameData/MegaCrew/Plugins/MegaCrew.dll"])
        })
        self.assertEqual(manifest["integration_excluded_historical_metadata"], {
            "integration-manifest.json": builder.sha256(
                b'{"historical":"not the new stage report"}\n')
        })
        self.assertFalse((built / "integration-manifest.json").exists())
        self.assertEqual(manifest["docking_group"]["status"], "from_full_snapshot")
        self.assertEqual(manifest["docking_group"]["files"], {
            "Source/OnboardDockingInstruments.cs": builder.sha256(
                self.snapshot["Source/OnboardDockingInstruments.cs"])
        })
        deviations = manifest["canonical_source_deviations"]
        self.assertEqual(deviations["Source/MegaCrew.cs"]["status"], "modified")
        self.assertEqual(deviations["Source/Stale.cs"]["status"], "removed")
        self.assertEqual(deviations["Source/New.cs"]["status"], "added")
        self.assertIsNone(deviations["Source/New.cs"]["canonical_sha256"])

    def test_compile_uses_the_same_snapshot_source_and_emits_compile_provenance(self):
        managed = self.base / "Managed"
        managed.mkdir()
        (managed / "Assembly-CSharp.dll").write_bytes(b"managed fixture")
        csc = self.base / "csc.dll"
        csc.write_bytes(b"compiler fixture")
        capture = self.base / "compile-inputs.json"
        fake_dotnet = self.base / "dotnet"
        fake_dotnet.write_text("""#!/usr/bin/env python3
import json, pathlib, shlex, sys
response = pathlib.Path(sys.argv[-1][1:])
tokens = shlex.split(response.read_text())
output = pathlib.Path(next(x[len('-out:'):] for x in tokens if x.startswith('-out:')))
sources = [pathlib.Path(x.strip('\\\"')) for x in tokens if x.endswith('.cs')]
output.parent.mkdir(parents=True, exist_ok=True)
output.write_bytes(b'compiled from immutable snapshot')
pathlib.Path(%r).write_text(json.dumps({p.name: p.read_text() for p in sources}))
""" % str(capture), encoding="utf-8")
        fake_dotnet.chmod(0o755)

        args = self.args(self.base / "compiled-candidate", prepare_only=False,
                         managed=managed, csc=csc)
        old_path = os.environ.get("PATH", "")
        try:
            os.environ["PATH"] = str(self.base) + os.pathsep + old_path
            built = builder.build(args, patch_sequence=self.patch_sequence,
                                  expected_zip_sha256=builder.sha256(self.archive))
        finally:
            os.environ["PATH"] = old_path

        assembly = built / "GameData/MegaCrew/Plugins/MegaCrew.dll"
        self.assertEqual(assembly.read_bytes(), b"compiled from immutable snapshot")
        captured = json.loads(capture.read_text())
        self.assertEqual(captured["MegaCrew.cs"], self.snapshot["Source/MegaCrew.cs"].decode())
        self.assertNotIn("Stale.cs", captured)
        manifest = json.loads((built / "manifest.json").read_text())
        self.assertTrue(manifest["compiled"])
        self.assertEqual(manifest["compiler_language_version"], "7.3")
        self.assertEqual(manifest["compiler_inputs"]["compiler_sha256"], builder.sha256(csc.read_bytes()))
        self.assertEqual(manifest["compiler_inputs"]["managed_assemblies"], {
            "Assembly-CSharp.dll": builder.sha256(b"managed fixture")
        })

    def test_integration_mode_rejects_legacy_overlay_flags_before_output(self):
        cases = (
            {"navigation_ref": "294c667"},
            {"operations_ref": self.ref},
        )
        for index, options in enumerate(cases):
            with self.subTest(options=options):
                output = self.base / ("rejected-{}".format(index))
                args = self.args(output, **options)
                with self.assertRaisesRegex(builder.BuildError, "cannot be combined"):
                    builder.build(args, patch_sequence=self.patch_sequence,
                                  expected_zip_sha256=builder.sha256(self.archive))
                self.assertFalse(output.exists())

    def test_integration_ref_missing_or_unresolvable_fails_before_output(self):
        output = self.base / "missing-ref"
        args = self.args(output, integration_ref="no-such-ref")
        with self.assertRaisesRegex(builder.BuildError, "Could not resolve Git ref"):
            builder.build(args, patch_sequence=self.patch_sequence,
                          expected_zip_sha256=builder.sha256(self.archive))
        self.assertFalse(output.exists())

    def test_snapshot_rejects_git_symlink_inside_package_roots(self):
        source = self.repo / "Source/Linked.cs"
        source.symlink_to("MegaCrew.cs")
        subprocess.run(["git", "-C", str(self.repo), "add", "Source/Linked.cs"], check=True)
        subprocess.run([
            "git", "-C", str(self.repo), "-c", "commit.gpgsign=false",
            "-c", "core.hooksPath=/dev/null", "commit", "--quiet", "-m", "snapshot symlink",
        ], check=True)
        ref = subprocess.run(["git", "-C", str(self.repo), "rev-parse", "HEAD"], check=True,
                             stdout=subprocess.PIPE, text=True).stdout.strip()
        with self.assertRaisesRegex(builder.BuildError, "symlink or non-regular"):
            builder.integration_snapshot_files(self.repo, ref)

    def test_snapshot_rejects_case_colliding_git_paths(self):
        for path, data in (("Source/Case.cs", b"one\n"), ("Source/case.cs", b"two\n")):
            blob = subprocess.run(["git", "-C", str(self.repo), "hash-object", "-w", "--stdin"],
                                  input=data, check=True, stdout=subprocess.PIPE,
                                  text=False).stdout.decode().strip()
            subprocess.run(["git", "-C", str(self.repo), "update-index", "--add", "--cacheinfo",
                            "100644", blob, path], check=True)
        subprocess.run([
            "git", "-C", str(self.repo), "-c", "commit.gpgsign=false",
            "-c", "core.hooksPath=/dev/null", "commit", "--quiet", "-m", "snapshot case collision",
        ], check=True)
        ref = subprocess.run(["git", "-C", str(self.repo), "rev-parse", "HEAD"], check=True,
                             stdout=subprocess.PIPE, text=True).stdout.strip()
        with self.assertRaisesRegex(builder.BuildError, "Case-colliding"):
            builder.integration_snapshot_files(self.repo, ref)

    def test_snapshot_rejects_case_colliding_ancestor_directories(self):
        records = []
        paths = (
            "README.md", "dependencies.json", "visual-profile.json", "Source/MegaCrew.cs",
            "Source/Nav/a.cs", "Source/nav/b.cs",
        )
        for index, path in enumerate(paths, 1):
            oid = ("%040x" % index)
            records.append("100644 blob {}\t{}\0".format(oid, path).encode("utf-8"))

        def fake_git(_repo, *args, **kwargs):
            self.assertEqual(args[:3], ("ls-tree", "-r", "-z"))
            return b"".join(records)

        with mock.patch.object(builder, "_git", side_effect=fake_git), \
             mock.patch.object(builder, "git_blob", side_effect=lambda _r, _c, p: p.encode()):
            with self.assertRaisesRegex(builder.BuildError, "Case-colliding"):
                builder.integration_snapshot_files(Path("unused"), "a" * 40)

    def test_legacy_allowlist_mode_still_uses_legacy_default_navigation_ref(self):
        subprocess.run(["git", "-C", str(self.repo), "update-ref", "refs/heads/294c667", self.ref],
                       check=True)
        args = self.args(self.base / "legacy", integration_ref=None, navigation_ref=None)
        built = builder.build(args, patch_sequence=self.patch_sequence,
                              expected_zip_sha256=builder.sha256(self.archive))
        manifest = json.loads((built / "manifest.json").read_text())
        self.assertEqual(manifest["mode"], "legacy_allowlist")
        self.assertEqual(manifest["navigation_ref"], "294c667")
        self.assertEqual(manifest["navigation_commit"], self.ref)
        self.assertTrue((built / "Source/Stale.cs").is_file())


if __name__ == "__main__":
    unittest.main(verbosity=2)
