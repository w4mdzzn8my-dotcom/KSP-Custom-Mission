import importlib.util
import tempfile
import unittest
from argparse import Namespace
from pathlib import Path
import sys


ROOT = Path(__file__).resolve().parents[1]
sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("build_iva_animation_qa", ROOT / "tools" / "build_iva_animation_qa.py")
builder = importlib.util.module_from_spec(spec)
spec.loader.exec_module(builder)


class IvaAnimationBuilderGuards(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="iva-qa-builder-test-")
        self.base = Path(self.temp.name)
        self.qa = self.base / builder.EXPECTED_ROOT_NAME
        self.qa.mkdir()
        (self.qa / "GameData").mkdir()
        (self.qa / "IvaAnimationQA.scope").write_bytes(builder.SCOPE_CONTENTS.encode("utf-8"))
        (self.qa / "IvaAnimationQA.enable").write_bytes(builder.ENABLE_CONTENTS.encode("utf-8"))
        self.managed = self.base / "Managed"
        self.managed.mkdir()
        (self.managed / "Assembly-CSharp.dll").write_bytes(b"fixture")
        self.csc = self.base / "csc.dll"
        self.csc.write_bytes(b"fixture")
        self.args = Namespace(qa_root=self.qa, managed=self.managed, csc=self.csc, repo=ROOT)

    def tearDown(self):
        self.temp.cleanup()

    def test_dedicated_scope_and_enable_are_required_exactly(self):
        builder.resolve_inputs(self.args)
        (self.qa / "IvaAnimationQA.enable").write_text("AUTHORIZE SOMETHING ELSE\n", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "one-shot save authorization"):
            builder.resolve_inputs(self.args)
        self.assertFalse((self.qa / builder.OUTPUT_RELATIVE).exists())

    def test_quarantine_qa_dll_does_not_count_as_loaded_game_data(self):
        quarantine = self.qa / "QA-Backups"
        quarantine.mkdir()
        (quarantine / "FinchQA.dll").write_bytes(b"backup")
        builder.resolve_inputs(self.args)

    def test_existing_qa_dll_under_gamedata_blocks_candidate(self):
        plugin = self.qa / "GameData" / "Legacy" / "Plugins"
        plugin.mkdir(parents=True)
        (plugin / "OldQA.DLL").write_bytes(b"old")
        with self.assertRaisesRegex(ValueError, "already contains a QA DLL"):
            builder.resolve_inputs(self.args)
        self.assertFalse((self.qa / builder.OUTPUT_RELATIVE).exists())

    def test_symlinked_gamedata_directory_is_rejected(self):
        (self.qa / "GameData").rmdir()
        outside = self.base / "outside"
        outside.mkdir()
        (self.qa / "GameData").symlink_to(outside, target_is_directory=True)
        with self.assertRaisesRegex(ValueError, "real directory inside"):
            builder.resolve_inputs(self.args)
        self.assertEqual(list(outside.iterdir()), [])

    def test_symlinked_subdirectory_cannot_hide_game_dlls(self):
        outside = self.base / "outside"
        outside.mkdir()
        (outside / "HiddenQA.dll").write_bytes(b"hidden")
        (self.qa / "GameData" / "linked").symlink_to(outside, target_is_directory=True)
        with self.assertRaisesRegex(ValueError, "symlinked directory"):
            builder.resolve_inputs(self.args)

    def test_existing_run_marker_prevents_rearm(self):
        (self.qa / "IvaAnimationQA.done").write_text("done\n", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "cannot be re-armed"):
            builder.resolve_inputs(self.args)

    def test_qa_output_path_symlink_cannot_escape_root(self):
        outside = self.base / "outside"
        outside.mkdir()
        (self.qa / "GameData" / "MegaCrewIvaAnimationQA").symlink_to(outside, target_is_directory=True)
        with self.assertRaisesRegex(ValueError, "symlinked directory"):
            builder.resolve_inputs(self.args)
        self.assertEqual(list(outside.iterdir()), [])


if __name__ == "__main__":
    unittest.main()
