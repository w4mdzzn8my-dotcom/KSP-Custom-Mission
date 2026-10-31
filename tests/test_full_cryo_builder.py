import importlib.util
import os
from pathlib import Path
import sys
import tempfile
import unittest


REPO = Path(__file__).resolve().parents[1]
sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location(
    "build_cryo_qa", REPO / "tools" / "build_cryo_qa.py")
builder = importlib.util.module_from_spec(spec)
spec.loader.exec_module(builder)


class FullCryoBuilderTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="full-cryo-builder-")
        self.base = Path(self.temp.name)
        self.qa = self.base / builder.ROOT_NAME
        self.qa.mkdir()
        (self.qa / "GameData").mkdir()
        (self.qa / builder.SCOPE_NAME).write_bytes(builder.SCOPE_TEXT)
        (self.qa / builder.ENABLE_NAME).write_bytes(builder.ENABLE_TEXT)

        self.managed = self.qa / "KSP.app/Contents/Resources/Data/Managed"
        self.managed.mkdir(parents=True)
        for name in ("Assembly-CSharp.dll", "UnityEngine.dll", "mscorlib.dll"):
            (self.managed / name).write_bytes(("managed:" + name).encode())
        for rel in builder.REQUIRED_GAME_REFS:
            path = self.qa / rel
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(("reference:" + rel.as_posix()).encode())

        self.repo = self.base / "repo"
        source = self.repo / builder.SOURCE_REL
        source.parent.mkdir(parents=True)
        source.write_text("// test fixture source\n", encoding="utf-8")
        self.csc = self.base / "csc.dll"
        self.csc.write_bytes(b"csc placeholder")
        self.dotnet = self.base / "dotnet"
        self._write_fake_dotnet(success=True)

    def tearDown(self):
        self.temp.cleanup()

    def _write_fake_dotnet(self, success):
        code = """#!/usr/bin/env python3
import pathlib, shlex, sys
if %r is False:
    raise SystemExit(17)
response = pathlib.Path(sys.argv[-1][1:])
tokens = shlex.split(response.read_text())
out = next(x[len('-out:'):] for x in tokens if x.startswith('-out:'))
pathlib.Path(out).write_bytes(b'compiled QA fixture')
""" % success
        self.dotnet.write_text(code, encoding="utf-8")
        self.dotnet.chmod(0o755)

    def test_fresh_exact_scope_token_and_all_references_validate(self):
        inputs = builder.validate_inputs(self.qa, self.csc, self.repo)
        self.assertEqual(inputs["root"], self.qa.resolve())
        self.assertEqual(len(inputs["managed_refs"]), 3)
        self.assertEqual(len(inputs["game_refs"]), 3)
        self.assertFalse(inputs["output"].exists())

    def test_enable_must_be_exact_one_shot_line(self):
        (self.qa / builder.ENABLE_NAME).write_bytes(b"one-shot")
        with self.assertRaisesRegex(ValueError, "exactly the fresh one-shot"):
            builder.validate_inputs(self.qa, self.csc, self.repo)
        self.assertFalse((self.qa / builder.OUTPUT_REL).exists())

    def test_scope_must_be_exact(self):
        (self.qa / builder.SCOPE_NAME).write_bytes(builder.SCOPE_TEXT + b"extra\n")
        with self.assertRaisesRegex(ValueError, "exact disposable-root sentinel"):
            builder.validate_inputs(self.qa, self.csc, self.repo)

    def test_existing_run_and_consumption_markers_refuse_rearm(self):
        for marker in builder.RUN_MARKERS:
            with self.subTest(marker=marker):
                path = self.qa / marker
                path.write_text("existing\n", encoding="utf-8")
                with self.assertRaisesRegex(ValueError, "already has a run marker"):
                    builder.validate_inputs(self.qa, self.csc, self.repo)
                path.unlink()

    def test_other_qa_dll_in_live_gamedata_blocks_build(self):
        other = self.qa / "GameData/Other/Plugins/OtherQA.dll"
        other.parent.mkdir(parents=True)
        other.write_bytes(b"old")
        with self.assertRaisesRegex(ValueError, "another QA DLL"):
            builder.validate_inputs(self.qa, self.csc, self.repo)

    def test_root_qa_backups_are_outside_live_plugins(self):
        backup = self.qa / "QA-Backups/OldProbeQA.dll"
        backup.parent.mkdir(parents=True)
        backup.write_bytes(b"archived")
        builder.validate_inputs(self.qa, self.csc, self.repo)

    def test_named_backup_inside_gamedata_is_still_a_live_plugin(self):
        backup = self.qa / "GameData/QA-Backups/OldProbeQA.dll"
        backup.parent.mkdir(parents=True)
        backup.write_bytes(b"still-loadable")
        with self.assertRaisesRegex(ValueError, "another QA DLL"):
            builder.validate_inputs(self.qa, self.csc, self.repo)

    def test_symlinked_gamedata_directory_is_rejected(self):
        game_data = self.qa / "GameData"
        import shutil
        shutil.rmtree(game_data)
        outside = self.base / "outside"
        outside.mkdir()
        game_data.symlink_to(outside, target_is_directory=True)
        with self.assertRaisesRegex(ValueError, "Symlink path component"):
            builder.validate_inputs(self.qa, self.csc, self.repo)
        self.assertEqual(list(outside.iterdir()), [])

    def test_symlinked_subtree_cannot_hide_a_qa_dll(self):
        outside = self.base / "outside"
        outside.mkdir()
        (outside / "HiddenQA.dll").write_bytes(b"hidden")
        (self.qa / "GameData/hidden").symlink_to(outside, target_is_directory=True)
        with self.assertRaisesRegex(ValueError, "symlinked or escaping directory"):
            builder.validate_inputs(self.qa, self.csc, self.repo)

    def test_symlinked_qa_backup_is_still_rejected(self):
        outside = self.base / "outside"
        outside.mkdir()
        (self.qa / "GameData/QA-Backups").symlink_to(outside, target_is_directory=True)
        with self.assertRaisesRegex(ValueError, "symlinked or escaping directory"):
            builder.validate_inputs(self.qa, self.csc, self.repo)

    def test_missing_required_dependency_refuses_without_writing_output(self):
        missing = self.qa / builder.REQUIRED_GAME_REFS[1]
        missing.unlink()
        with self.assertRaisesRegex(ValueError, "Required QA reference is missing"):
            builder.build(self.qa, self.csc, self.repo, str(self.dotnet))
        self.assertFalse((self.qa / builder.OUTPUT_REL).exists())

    def test_compiler_failure_leaves_no_output_or_reference_changes(self):
        before = {p: p.read_bytes() for p in [*self.managed.glob("*.dll"),
                                               *(self.qa / r for r in builder.REQUIRED_GAME_REFS)]}
        self._write_fake_dotnet(success=False)
        with self.assertRaises(Exception):
            builder.build(self.qa, self.csc, self.repo, str(self.dotnet))
        self.assertFalse((self.qa / builder.OUTPUT_REL).exists())
        self.assertEqual(before, {p: p.read_bytes() for p in before})

    def test_success_installs_exactly_one_exclusive_output(self):
        before = {p: p.read_bytes() for p in [*self.managed.glob("*.dll"),
                                               *(self.qa / r for r in builder.REQUIRED_GAME_REFS)]}
        output = builder.build(self.qa, self.csc, self.repo, str(self.dotnet))
        self.assertEqual(output, (self.qa / builder.OUTPUT_REL).resolve())
        self.assertEqual(output.read_bytes(), b"compiled QA fixture")
        self.assertEqual(before, {p: p.read_bytes() for p in before})
        self.assertEqual([path.resolve() for path in builder.scan_qa_dlls(self.qa / "GameData")], [output])

    def test_existing_output_is_never_overwritten(self):
        output = self.qa / builder.OUTPUT_REL
        output.parent.mkdir(parents=True)
        output.write_bytes(b"original")
        with self.assertRaisesRegex(ValueError, "another QA DLL"):
            builder.build(self.qa, self.csc, self.repo, str(self.dotnet))
        self.assertEqual(output.read_bytes(), b"original")


if __name__ == "__main__":
    unittest.main()
