#!/usr/bin/env python3
"""Validate KSP serialized VESSEL/PART snapshot indices (stdlib only)."""
from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
import re
from typing import Iterable


@dataclass
class Node:
    name: str
    values: dict[str, list[str]] = field(default_factory=dict)
    children: list["Node"] = field(default_factory=list)
    line: int = 0

    def get(self, key: str, default: str | None = None) -> str | None:
        vals = self.values.get(key)
        return vals[0] if vals else default

    def gets(self, key: str) -> list[str]:
        return self.values.get(key, [])

    def nodes(self, name: str) -> list["Node"]:
        return [child for child in self.children if child.name == name]


def parse_config(text: str) -> Node:
    """Parse KSP ConfigNode syntax, retaining repeated values and child order."""
    root = Node("<root>")
    stack = [root]
    pending: tuple[str, int] | None = None
    for line_no, raw in enumerate(text.lstrip("\ufeff").splitlines(), 1):
        # KSP comments start with //; no relevant snapshot values need quoted //.
        line = raw.split("//", 1)[0].strip()
        if not line:
            continue
        if pending is not None and line != "{":
            name, start_line = pending
            raise ValueError(f"line {line_no}: node name {name!r} from line {start_line} not followed by '{{'")
        if line == "{":
            if pending is None:
                raise ValueError(f"line {line_no}: opening brace without node name")
            name, start_line = pending
            node = Node(name, line=start_line)
            stack[-1].children.append(node)
            stack.append(node)
            pending = None
        elif line == "}":
            if len(stack) == 1:
                raise ValueError(f"line {line_no}: unexpected closing brace")
            stack.pop()
        elif "=" in line:
            key, value = line.split("=", 1)
            stack[-1].values.setdefault(key.strip(), []).append(value.strip())
        else:
            pending = (line, line_no)
    if pending is not None:
        raise ValueError(f"line {pending[1]}: node name {pending[0]!r} not followed by '{{'")
    if len(stack) != 1:
        raise ValueError(f"unclosed node {stack[-1].name!r}")
    return root


def walk(node: Node, name: str) -> Iterable[Node]:
    for child in node.children:
        if child.name == name:
            yield child
        yield from walk(child, name)


def vessel_nodes(root: Node) -> list[Node]:
    return list(walk(root, "VESSEL"))


def parse_index(value: str, *, field_name: str, part_i: int, issues: list[str]) -> int | None:
    token = value.strip()
    if not re.fullmatch(r"-?\d+", token):
        issues.append(f"ERROR part[{part_i}] {field_name}: expected integer, got {token!r}")
        return None
    return int(token)


def parse_uint_id(value: str, *, field_name: str, part_i: int, issues: list[str]) -> int | None:
    """Parse a canonical decimal KSP uint ID; normalize before uniqueness checks."""
    token = value.strip()
    if not re.fullmatch(r"\d+", token):
        issues.append(f"ERROR part[{part_i}] {field_name}: expected nonempty unsigned decimal ID, got {token!r}")
        return None
    number = int(token, 10)
    if number > 0xFFFFFFFF:
        issues.append(f"ERROR part[{part_i}] {field_name}: value {token!r} exceeds uint32 range")
        return None
    if token != str(number):
        issues.append(f"ERROR part[{part_i}] {field_name}: non-canonical decimal ID {token!r}; use {number}")
    return number


def index_tokens(values: list[str]) -> list[str]:
    """Split serialized index lists; preserves invalid tokens for diagnostics."""
    out: list[str] = []
    for value in values:
        out.extend(token for token in re.split(r"[,\s]+", value.strip()) if token)
    return out


def validate_vessel(vessel: Node) -> list[str]:
    """Return diagnostics for a serialized ProtoVessel ConfigNode."""
    issues: list[str] = []
    parts = vessel.nodes("PART")  # direct children only; MODULE/PART cargo is excluded
    n = len(parts)
    label = vessel.get("name", "<unnamed>")
    if not n:
        return [f"ERROR vessel {label}: no direct PART nodes"]

    root_raw = vessel.get("root")
    root_i = parse_index(root_raw, field_name="root", part_i=-1, issues=issues) if root_raw is not None else None
    if root_raw is None:
        issues.append(f"ERROR vessel {label}: missing root index")
    elif root_i is not None and not (0 <= root_i < n):
        issues.append(f"ERROR vessel {label}: root index {root_i} outside [0,{n - 1}]")

    for field_name in ("uid", "persistentId"):
        seen: dict[int, int] = {}
        for i, part in enumerate(parts):
            value = part.get(field_name)
            if value is None:
                issues.append(f"ERROR part[{i}] {part.get('name', '?')}: missing {field_name}")
                continue
            parsed = parse_uint_id(value, field_name=field_name, part_i=i, issues=issues)
            if parsed is None:
                continue
            if parsed in seen:
                issues.append(f"ERROR part[{i}] {part.get('name', '?')}: duplicate {field_name}={parsed} (also part[{seen[parsed]}])")
            else:
                seen[parsed] = i

    parents: list[int | None] = []
    for i, part in enumerate(parts):
        name = part.get("name", "?")
        parent_raw = part.get("parent")
        if parent_raw is None:
            issues.append(f"ERROR part[{i}] {name}: missing parent index")
            parents.append(None)
        else:
            parent = parse_index(parent_raw, field_name="parent", part_i=i, issues=issues)
            parents.append(parent)
            if parent is not None:
                if i == root_i:
                    if parent not in (-1, i):
                        issues.append(f"ERROR root part[{i}] {name}: parent must be -1 or self, got {parent}")
                elif not (0 <= parent < n):
                    issues.append(f"ERROR part[{i}] {name}: parent index {parent} outside [0,{n - 1}]")
                elif parent == i:
                    issues.append(f"ERROR part[{i}] {name}: non-root self-parent")
                elif parent >= i:
                    issues.append(f"ERROR part[{i}] {name}: parent {parent} is not parent-first (must precede part index {i})")

        # Stack attachment nodes are serialized as "nodeName, partIndex".
        for att in part.gets("attN"):
            chunks = att.split(",", 1)
            if len(chunks) != 2:
                issues.append(f"ERROR part[{i}] {name} attN: malformed value {att!r}")
                continue
            target = parse_index(chunks[1], field_name=f"attN[{chunks[0].strip()}]", part_i=i, issues=issues)
            if target is not None and target != -1 and not (0 <= target < n):
                issues.append(f"ERROR part[{i}] {name} attN[{chunks[0].strip()}]: target {target} outside [0,{n - 1}]")

        # Surface nodes use the same part-index domain. -1 is a legal empty sentinel,
        # but suspicious for a part explicitly marked as surface-attached with a parent.
        for srf in part.gets("srfN"):
            chunks = srf.split(",", 1)
            if len(chunks) != 2:
                issues.append(f"ERROR part[{i}] {name} srfN: malformed value {srf!r}")
                continue
            node_name, raw_target = chunks[0].strip(), chunks[1].strip()
            target = parse_index(raw_target, field_name="srfN", part_i=i, issues=issues)
            if target is not None and target != -1 and not (0 <= target < n):
                issues.append(f"ERROR part[{i}] {name} srfN: target {target} outside [0,{n - 1}]")
            attm = part.get("attm", "0").lower() == "1"
            parent = parents[i]
            if node_name == "srfAttach" and attm and parent not in (None, -1, i) and target == -1:
                issues.append(f"ERROR part[{i}] {name}: srfAttach target is -1 but parent is {parent}")
            elif node_name == "srfAttach" and target not in (None, -1) and parent not in (None, target):
                issues.append(f"ERROR part[{i}] {name}: srfAttach target {target} disagrees with parent {parent}")

        # KSP ProtoPartSnapshot symmetry references are indices. Some snapshots omit
        # these fields; ordinary symMethod values are modes, not references.
        for key in ("symLinkIdx", "symLinkIdxs", "symLinks", "symmetryLinks"):
            for token in index_tokens(part.gets(key)):
                target = parse_index(token, field_name=key, part_i=i, issues=issues)
                if target is not None and target != -1 and not (0 <= target < n):
                    issues.append(f"ERROR part[{i}] {name} {key}: target {target} outside [0,{n - 1}]")

    # Validate that parent links form one rooted tree, independently of ordering.
    if root_i is not None and 0 <= root_i < n:
        children: dict[int, list[int]] = {i: [] for i in range(n)}
        for i, parent in enumerate(parents):
            if i == root_i or parent is None or not (0 <= parent < n) or parent == i:
                continue
            children[parent].append(i)
        visited: set[int] = set()
        todo = [root_i]
        while todo:
            i = todo.pop()
            if i in visited:
                continue
            visited.add(i)
            todo.extend(children[i])
        if len(visited) != n:
            missing = ", ".join(f"{i}:{parts[i].get('name', '?')}" for i in range(n) if i not in visited)
            issues.append(f"ERROR vessel {label}: parent graph disconnected/cyclic; unreachable parts: {missing}")

    return issues


def validate_text(text: str, vessel_name: str | None = None) -> tuple[Node, list[tuple[Node, list[str]]]]:
    root = parse_config(text)
    vessels = vessel_nodes(root)
    if vessel_name is not None:
        vessels = [v for v in vessels if v.get("name") == vessel_name]
    return root, [(v, validate_vessel(v)) for v in vessels]


def main() -> int:
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("save", type=Path)
    parser.add_argument("--vessel", help="exact VESSEL name; default validates every vessel")
    args = parser.parse_args()
    try:
        _, reports = validate_text(args.save.read_text(errors="replace"), args.vessel)
    except (OSError, ValueError) as exc:
        parser.error(str(exc))
    if not reports:
        print("ERROR: no matching VESSEL nodes")
        return 2
    total_errors = 0
    for vessel, issues in reports:
        parts = vessel.nodes("PART")
        print(f"VESSEL {vessel.get('name', '<unnamed>')}: {len(parts)} direct parts")
        if not issues:
            print("  PASS: no index/tree validation errors")
        else:
            for issue in issues:
                print("  " + issue)
                if issue.startswith("ERROR"):
                    total_errors += 1
    return 1 if total_errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
