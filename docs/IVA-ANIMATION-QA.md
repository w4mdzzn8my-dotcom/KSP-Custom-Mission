# Disposable IVA animation catalog QA

This utility builds one read-only catalog addon for an explicitly authorized,
disposable KSP copy. It inventories the live controller attached to already
rendered IVA Kerbals. It does not try to play a gesture and does not qualify an
animation for use in a handover.

## Preconditions

The KSP root must be a separate copy whose directory basename is exactly
`KSP-Hermes-QA`. Create these two exact UTF-8 files at that root, each with a
final newline.

`IvaAnimationQA.scope`:

```text
MEGACREW_DISPOSABLE_IVA_ANIMATION_QA_V1
```

`IvaAnimationQA.enable`:

```text
AUTHORIZE ONE READ-ONLY IVA ANIMATION CATALOG FOR SAVE: MegaCrew Background QA
```

The builder refuses a missing or different scope/enable file, existing
running/done/failed marker or report, or any existing DLL under `GameData`
whose filename contains `QA` (case insensitive). It rejects symlinked
`GameData` traversal so the scan covers the installable tree. It never scans
quarantine/backup folders or writes the regular MegaCrew plugin. It compiles
into a temporary directory first, then exclusively creates only
`GameData/MegaCrewIvaAnimationQA/Plugins/MegaCrewIvaAnimationQA.dll` in the
disposable copy. It does not start or modify a save.

Example, after preparing that dedicated copy and authorization file:

```sh
python3 tools/build_iva_animation_qa.py \
  --qa-root "/path/to/KSP-Hermes-QA" \
  --managed "/path/to/KSP-Hermes-QA/KSP.app/Contents/Resources/Data/Managed" \
  --csc "/path/to/dotnet/sdk/VERSION/Roslyn/bincore/csc.dll"
```

Use the matching KSP 1.12.5 `Data/Managed` and Roslyn compiler. Building the
addon is not authorization to load it; the exact root and scope sentinel are
checked at startup, then scope and enable contents are rechecked at claim.

## Runtime behavior

At addon startup it caches whether the exact root basename and scope sentinel
are valid. It does not reread the scope each frame. The addon waits for all of
the following: that startup check, `MegaCrew Background QA` save, flight
scene, ready active loaded and unpacked non-EVA vessel, finite UT, unpaused 1x,
and IVA camera mode. Immediately before claiming the run it revalidates scope
and enable contents. A same-directory move consumes `IvaAnimationQA.enable`
into `IvaAnimationQA.running`; only then may cataloging begin. Before
cataloging it captures the active vessel object and ID, save, and `Game`
object. It rechecks the same context before and after the synchronous read. A
context change makes the result inconclusive.

The catalog first filters to parts with actual `protoModuleCrew` membership,
then reads their internal-model seats and already rendered
`InternalSeat.kerbalRef`. The public MegaCrew `CryoBridge.TryFrozen(string, out bool)`
query excludes only known frozen crew; if that read is unavailable, the catalog
remains inconclusive. A rendered Kerbal must match the part's
`protoModuleCrew`, `seat.crew`, `protoCrewMember.seat`, and `InPart`. Empty
parts and known-frozen crew do not count as expected rendered actors. Missing
models on crewed parts, awake occupants without matching rendered Kerbals,
missing animators/controllers, or changed context prevent a complete result.
This is deliberately strict: partial data is `INCONCLUSIVE`, not a pass.

For each rendered Kerbal it records Animator/controller names; attached
animation clip names and lengths; layer names; current and, when transitioning,
next state hashes; transition flags; and `HasState` results for
`wavesToOtherKerbal`, `true_noddingA`, and their layer-qualified forms. The
candidate names are strings to query, not assumed APIs or playable states.

No call is made to `Animator.Play`, `CrossFade`, animation parameters,
`InternalSeat.SpawnCrew`/`DespawnCrew`, camera controls, seat assignment, crew
state, resources, or vessel controls. The only writes are in the disposable
root. The first valid run atomically moves the enable file to
`IvaAnimationQA.running`, then writes `IvaAnimationQA.report.txt` and moves
the running marker to either `IvaAnimationQA.done` or `IvaAnimationQA.failed`.
A failure before the enable token is claimed writes no marker. Existing
markers block all later runs; there is no rearm/reset path in the addon.

`probe_completed=true` means only that the catalog was complete for the
observed vessel seats and the context stayed stable. Every report and marker
keeps `animation_qualification=false`, even when cataloging completes. It
does not establish gesture semantics, safe restoration, visual quality,
FreeIVA compatibility, multiplayer synchronization or a functioning handover.

## Review before any runtime use

The expected addon is the only `*QA*.dll` in the disposable copy. Confirm the
scope text and save name, launch only that disposable copy, load the named QA
save, enter IVA at 1x, and inspect the report after the catalog completes. If
the report says `INCONCLUSIVE`, preserve it as an observation and diagnose
missing render/controller data before changing the probe. Never infer that an
asset clip name is a callable seated-Kerbal gesture.

## Verification so far

The dedicated builder compiled the probe against the installed KSP 1.12.5
Managed assemblies and emitted exactly one separate QA DLL in an ephemeral
scoped build fixture. Its enable token remained unconsumed. Nothing was
installed into a game copy.

Seven Python builder guard tests pass. The recording harness compiles the
actual probe against KSP/Unity fakes and passes 31 assertions for authorization,
one-shot markers, unknown cryo state, actual crew/seat/render identity, missing
renderers and ignoring uncrewed parts. The fake Animator exposes no playback
methods. Run these from the repository root:

```sh
python3 -m unittest discover -s tests -v
python3 tests/iva_animation/run_tests.py --runtime /path/to/Microsoft.NETCore.App/VERSION --csc /path/to/Roslyn/bincore/csc.dll
```

This verifies code and boundaries, not real rendered IVA controllers. No live
catalog report or gesture qualification exists yet. Use a combined 0.3.7
candidate exposing the public `CryoBridge.TryFrozen(string, out bool)` query;
older cores without that method yield an inconclusive freeze-state observation.
