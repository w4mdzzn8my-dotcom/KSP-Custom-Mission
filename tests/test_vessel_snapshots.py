import unittest

from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from validate_vessel_snapshots import parse_config, validate_text


VALID = """\
VESSEL
{
    name = Synthetic valid
    root = 0
    PART
    {
        name = rootPart
        uid = 100
        persistentId = 1000
        parent = -1
        attN = top, 1
        symLinkIdxs = 0
    }
    PART
    {
        name = surfaceChild
        uid = 101
        persistentId = 1001
        parent = 0
        attm = 1
        srfN = srfAttach, 0
        attN = bottom, 0
        symLinkIdxs = 0, 1
    }
}
"""


def errors(text, vessel_name=None):
    _, reports = validate_text(text, vessel_name)
    return [issue for _, items in reports for issue in items]


def finch_order_regression() -> str:
    """Synthetic 35-part tree with the saved QA fixture's cabin/port order defect."""
    lines = ["VESSEL", "{", "    name = Finch order regression", "    root = 0"]
    for i in range(35):
        if i == 0:
            parent = 0
        elif i == 18:  # cabin appears before its docking-port parent at index 21
            parent = 21
        elif i == 21:  # lander port is attached to carrier port at index 17
            parent = 17
        elif i == 19:
            parent = 18
        elif i == 20:
            parent = 19
        elif i == 17:
            parent = 4
        elif i in range(22, 35):
            parent = 19 if i in (22, 25, 28, 31) else (18 if i != 34 else 18)
        else:
            parent = 0
        name = "mk2LanderCabin.v2" if i == 18 else ("dockingPort2" if i in (17, 21) else f"part{i}")
        lines += [
            "    PART", "    {", f"        name = {name}",
            f"        uid = {2700000000 + i}", f"        persistentId = {970001000 + i}",
            f"        parent = {parent}", "    }",
        ]
    lines.append("}")
    return "\n".join(lines) + "\n"


class ParserTests(unittest.TestCase):
    def test_node_name_before_close_is_rejected(self):
        with self.assertRaisesRegex(ValueError, "node name 'PART'.*not followed by"):
            parse_config("VESSEL\n{\nPART\n}\n")

    def test_node_name_before_value_is_rejected(self):
        with self.assertRaisesRegex(ValueError, "node name 'PART'.*not followed by"):
            parse_config("VESSEL\n{\nPART\nname = broken\n}\n")

    def test_unexpected_open_and_close_braces_are_rejected(self):
        with self.assertRaisesRegex(ValueError, "opening brace without node name"):
            parse_config("{\n}\n")
        with self.assertRaisesRegex(ValueError, "unexpected closing brace"):
            parse_config("}\n")


class SyntheticSnapshotTests(unittest.TestCase):
    def test_valid_parent_first_tree_allows_root_parent_minus_one(self):
        self.assertEqual(errors(VALID, "Synthetic valid"), [])

    def test_forward_parent_is_rejected(self):
        text = """\
VESSEL
{
    name = Forward parent
    root = 0
    PART
    {
        name = root
        uid = 1
        persistentId = 11
        parent = 0
    }
    PART
    {
        name = child
        uid = 2
        persistentId = 12
        parent = 2
    }
    PART
    {
        name = parent
        uid = 3
        persistentId = 13
        parent = 0
    }
}
"""
        got = errors(text, "Forward parent")
        self.assertTrue(any("part[1] child: parent 2 is not parent-first" in x for x in got), got)

    def test_finch_35_part_order_regression_fails_at_cabin(self):
        got = errors(finch_order_regression(), "Finch order regression")
        self.assertEqual(got, [
            "ERROR part[18] mk2LanderCabin.v2: parent 21 is not parent-first (must precede part index 18)"
        ])

    def test_reordered_finch_35_part_tree_passes(self):
        text = finch_order_regression()
        # Move the original lander port (21) before the cabin (18); regenerate parents.
        root, reports = validate_text(text, "Finch order regression")
        vessel = reports[0][0]
        original = vessel.nodes("PART")
        order = list(range(18)) + [21, 18, 19, 20] + list(range(22, 35))
        remap = {old: new for new, old in enumerate(order)}
        lines = ["VESSEL", "{", "    name = Finch reordered", "    root = 0"]
        for old in order:
            p = original[old]
            parent = int(p.get("parent"))
            if old == 0:
                new_parent = 0
            else:
                new_parent = remap[parent]
            name = p.get("name")
            uid = p.get("uid")
            pid = p.get("persistentId")
            lines += ["    PART", "    {", f"        name = {name}", f"        uid = {uid}", f"        persistentId = {pid}", f"        parent = {new_parent}", "    }"]
        lines.append("}")
        self.assertEqual(errors("\n".join(lines) + "\n", "Finch reordered"), [])

    def test_attachment_symmetry_and_surface_targets_are_checked(self):
        text = VALID.replace("attN = top, 1", "attN = top, 9")
        text = text.replace("srfN = srfAttach, 0", "srfN = srfAttach, -1")
        text = text.replace("symLinkIdxs = 0, 1", "symLinkIdxs = 0, 8")
        got = errors(text, "Synthetic valid")
        self.assertTrue(any("attN[top]: target 9 outside" in x for x in got), got)
        self.assertTrue(any("srfAttach target is -1 but parent is 0" in x for x in got), got)
        self.assertTrue(any("symLinkIdxs: target 8 outside" in x for x in got), got)

    def test_ids_are_uint32_and_normalized_for_duplicate_check(self):
        text = VALID.replace("uid = 101", "uid = 0100")
        text = text.replace("persistentId = 1001", "persistentId = 01000")
        got = errors(text, "Synthetic valid")
        self.assertTrue(any("non-canonical decimal ID '0100'; use 100" in x for x in got), got)
        self.assertTrue(any("duplicate uid=10" in x for x in got), got)
        self.assertTrue(any("non-canonical decimal ID '01000'; use 1000" in x for x in got), got)
        self.assertTrue(any("duplicate persistentId=1000" in x for x in got), got)

    def test_missing_non_numeric_and_overflow_ids_are_rejected(self):
        text = VALID.replace("uid = 101", "uid = ").replace("persistentId = 1001", "persistentId = 4294967296")
        got = errors(text, "Synthetic valid")
        self.assertTrue(any("expected nonempty unsigned decimal ID, got ''" in x for x in got), got)
        self.assertTrue(any("exceeds uint32 range" in x for x in got), got)

    def test_duplicate_ids_and_bad_parent_graph(self):
        text = VALID.replace("uid = 101", "uid = 100").replace("persistentId = 1001", "persistentId = 1000")
        text = text.replace("parent = 0\n        attm", "parent = 7\n        attm")
        got = errors(text, "Synthetic valid")
        self.assertTrue(any("duplicate uid=100" in x for x in got), got)
        self.assertTrue(any("duplicate persistentId=1000" in x for x in got), got)
        self.assertTrue(any("parent index 7 outside" in x for x in got), got)
        self.assertTrue(any("parent graph disconnected/cyclic" in x for x in got), got)


if __name__ == "__main__":
    unittest.main(verbosity=2)
