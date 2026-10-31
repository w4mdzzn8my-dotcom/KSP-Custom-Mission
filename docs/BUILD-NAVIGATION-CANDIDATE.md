# Build an isolated MegaCrew candidate


## Current consolidated branch

Use `--integration-ref HEAD` for the complete reviewed branch, including core
UI changes and Duna mission files. Commit source changes first: the builder
reads immutable Git objects, never uncommitted files. This mode cannot be
combined with explicit `--navigation-ref` or `--operations-ref` overlays.

```sh
python3 tools/build_navigation_candidate.py --repo . --integration-ref HEAD \
  --output /tmp/new-megacrew-candidate \
  --managed /path/to/KSP_Data/Managed \
  --csc /path/to/dotnet/sdk/Roslyn/bincore/csc.dll
```

It verifies/reconstructs the pinned base, then replaces Source, docs, tools,
tests, Ships, Missions and GameData/MegaCrew with the tracked snapshot. README,
dependencies and visual profile metadata also come from that commit. Deleted
base sources do not linger. Tracked DLLs and the historical base DLL are omitted;
normal compilation is fresh, and `--prepare-only` produces no plugin DLL.
The staging manifest records snapshot/source hashes, exclusions, changes from
the canonical core, builder/compiler/reference provenance and actual output
hashes. It is the authority for this staging output, separate from the repo's
historical integration manifest.

No game is launched or installed. During Basil's active-play hold, preparation
and compilation may use a new temporary source output only; do not run its
installer or copy it into any game directory. Full flight/UI qualification is
still pending.

## Historical addon overlay mode

The builder defaults to upstream commit `f76e437` and navigation commit
`294c667`. It reads the published `Hermes-Practice-v0.3.0.zip`, verifies its
pinned SHA-256, then applies the single cumulative
`Hermes-Campaign-v0.3.7-source.patch` exactly once and verifies SHA-256
`5f59faa0a8220abf9a5119b76fb98caac8e4e73c189cc7e4d2f05948b80b8770`.

Do not stack 0.3.6, coast, tests, console, supply, epoch, or QA patches on 0.3.7.
The packet contains the host-owned campaign source, tests, and documentation.

By default the tool prepares an upstream-plus-navigation candidate. It overlays
tracked C# files under `Source/Navigation` and the fixed five navigation test
files from the explicit navigation ref. Navigation-only use remains the default.

To build a combined candidate, pass `--operations-ref` with an explicit reviewed
Git commit or ref. There is no default or mutable working-tree fallback. The
builder reads only its fixed ten-file onboard source allowlist and the five
model test/runner pairs at that commit. It never overlays upstream campaign
core files such as `MegaCrew.cs`, `Practice.cs`, or campaign/needs sources. All
navigation and operations source/test refs and file hashes are written to the
manifest. Existing candidate paths are rejected; overlays are collision-checked
as a group and never overwrite files.

The seven docking-instrument files form an optional fixed group at the operations
ref: `Source/DockingGeometryModel.cs`, `Source/OnboardDockingInstruments.cs`,
`tests/DockingGeometryModelTests.cs`, `tests/run_docking_geometry_model_tests.py`,
`tests/DockingInstrumentControllerTests.cs`,
`tests/DockingInstrumentControllerFakes.cs`, and
`tests/run_docking_instrument_controller_tests.py`. The group is included only
when all seven paths exist. If none exist, older operations refs remain valid;
if only a subset exists, preparation fails before an output directory is
created. The manifest records the group status and each file's SHA-256.

Examples (from repository root):

```sh
python3 tools/build_navigation_candidate.py \
  --repo . \
  --output /tmp/megacrew-037-nav \
  --prepare-only

python3 tools/build_navigation_candidate.py \
  --repo . \
  --navigation-ref 294c667 \
  --operations-ref 3c1fe22 \
  --output /tmp/megacrew-037-combined \
  --managed /path/to/KSP_Data/Managed \
  --csc /path/to/dotnet/sdk/Roslyn/bincore/csc.dll
```

An output directory must be new and outside the source checkout. Inputs are
resolved and verified before the output is created. The builder does not
install the plugin, access personal saves or a server, download inputs, or modify the
checkout. With `--managed` and `--csc`, it compiles every candidate plugin source
as C# 7.3 against the supplied local KSP assemblies.

The safety tests cover unsafe ZIP members, traversal and symlink defense,
artifact hashes, existing destinations, relative output paths, ordered
cumulative patch application, missing operations blobs, collision/no-overwrite
behavior, core-source preservation, and navigation-only, pre-docking combined,
and full/absent/partial seven-file docking groups in synthetic candidates.


Rebuild a combined candidate with this script into a new output directory. In historical overlay mode the
extracted host packet's `tools/build.py` scans only flat `Source/*.cs`,
so it omits nested navigation sources. This builder compiles `Source/**/*.cs`
recursively. Keep using it for combined builds until the host helper is updated;
an in-place rebuild with the flat helper would produce a different plugin.

The consolidated snapshot instead carries the reviewed recursive build helper.
