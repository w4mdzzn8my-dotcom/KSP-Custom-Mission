# Disposable stock-node QA probe

This is a bounded, in-memory QA fixture for the circular lower-periapsis preview. It is deliberately gated by both a disposable save name and a one-use marker. It has **not** been launched in KSP; a successful compile is only an API/type check.

## Compile

```sh
python3 tools/build_navigation_probe.py \
  --managed /path/to/KSP_Data/Managed \
  --navigation /tmp/megacrew-candidate/GameData/MegaCrew/Plugins/MegaCrew.dll \
  --csc /path/to/dotnet/sdk/VERSION/Roslyn/bincore/csc.dll \
  --output /tmp/megacrew-probe/CircularNodeQaProbe.dll
```

Build the exact navigation assembly with the [isolated source builder](BUILD-NAVIGATION-CANDIDATE.md)
first. The probe references that assembly's identity. For an in-game run, stage
both matching assemblies in a **disposable KSP installation** after the runtime
restriction clears. This QA assembly must stay out of normal installations.

## One-run authorization

Prepare a separate save folder exactly named `MegaCrew Navigation QA`, with `persistent.sfs` and an unpacked circular Kerbin-orbit fixture at vessel index `0`. Back it up first. In the KSP application root, create `CircularNodeQA.enable` with exactly this one line:

```text
AUTHORIZE ONE IN-MEMORY NODE QA RUN FOR SAVE: MegaCrew Navigation QA
```

After the marker is consumed, it becomes `CircularNodeQA.consumed`. The run arm exists only in process memory and is consumed once when the flight addon starts in the exact save folder. To re-arm, remove the consumed file and create a fresh enable file. A missing marker does nothing; wrong contents, an existing consumed marker, or a missing exact save refuse startup.

## Probe behavior and acceptance tolerances

The flight addon waits at most 12 real seconds for an active, loaded, unpacked orbiting vessel. It requires 1x time warp, no producing/spooling `ModuleEngines`, no existing maneuver nodes, an active Kerbin patch/body, eccentricity `e <= 1e-5`, and current PeA above the fixed 80,000 m target. It never starts/stages an engine, changes time warp, commands time advancement, or calls a save API.

It computes the detached preview at `UT + 120 s`, independently recomputes the resulting two-body apsides from specific energy and angular momentum, and logs PASS/FAIL lines for:

- Detached preview PeA versus independent conic PeA: `<= 1 m`.
- Independent target PeA: `<= 1 m` from `80,000 m`.
- Stock `ManeuverNode.nextPatch.referenceBody`: exact body object identity match.
- Stored node DeltaV components are `(0,0,signedDv)` within `1e-9 m/s`.
- Stock next-patch PeA versus independent detached PeA: `<= 5 m`.
- Stock next-patch ApA versus independent detached ApA: `<= 5 m`.

It adds exactly one temporary node with `(radial-plus, normal-minus, prograde) = (0, 0, signedPreviewDv)`, waits at most 8 real seconds for the stock patch, then removes only the exact node reference it created and updates the flight plan. If another node appears, the probe aborts and retains that unrelated node. Scene changes and addon destruction run the same exact-node cleanup. Do not manually save during the probe; the fixture is intended to remain disposable.

## Node-frame cross-check status

The probe does not compare `NodeFrame` to `ManeuverNode.GetBurnVector`. KSP's generated API reference specifies the DeltaV component signs (x radial-plus, y normal-minus, z prograde), but describes `GetBurnVector(Orbit)` only as “Perhaps” a world-space translation. The output frame remains undocumented there. Passing Orbit's Y/Z-flipped vectors directly to the helper's right-handed basis could generate a false result. Add a separate frame test only after establishing and explicitly applying the common inertial-frame conversion. [KSP ManeuverNode API](https://anatid.github.io/XML-Documentation-for-the-KSP-API/class_maneuver_node.html), [KSP Orbit API](https://anatid.github.io/XML-Documentation-for-the-KSP-API/class_orbit.html).

No test result here claims that the preview or KSP runtime passed. A morning in-game run should be performed only in the named disposable save/install after reviewing the logs and confirming cleanup.
