# Mission Control navigation kernel

Basil requested an onboard computer for planetary intercepts, atmospheric
periapsis targeting and rendezvous, with reliable node creation and realistic
communications delay. These features are not implemented yet. This first kernel
contains only two independently tested calculations:

- Round-trip light time for a specified outbound and inbound path length, in
  metres, using 299,792,458 m/s. A static direct path has delay 2d/c. Relay path,
  changing geometry, CommNet connection and asynchronous request handling must
  be integrated before treating this as mission-control authorization.
- Ideal signed tangential impulse from a *circular* orbit at its current radius
  to a lower target periapsis, assuming one central body and an instantaneous
  burn. Invalid inputs and periapses at/above the initial orbit are rejected.
  This is a math primitive, not a live orbit detector, node creator or flight plan.

Tests independently reconstruct the target orbit's apsides from energy and
angular momentum. Run with local .NET paths:

```sh
python3 tests/navigation/run.py --runtime /path/to/shared/Microsoft.NETCore.App/VERSION --csc /path/to/sdk/VERSION/Roslyn/bincore/csc.dll
```

Before enabling node creation, require a finite, current orbit and explicit
assumption checks, independently propagate the proposed maneuver using KSP's
patched-conic solver, compare predicted periapsis to a defined tolerance, and
refuse creation if the prediction cannot be verified. Never silently replace an
existing node. Explicitly coordinate plan revision, burn time and the two human
approvals with the upstream shift system; both approvals must be invalidated by
any plan change. Single-player name checks are a rehearsal, not authenticated
multiplayer approval. Include connection loss, delayed responses, vessel change,
SOI transitions, stale plans and save/reload in integration testing.

A planet intercept needs multi-body patched-conic search and independent
propagation, not a circular two-body shortcut. Docking rendezvous needs relative
position and velocity at encounter, collision constraints and approach staging.
Those remain open work. No automatic burns or time warp are introduced here.

## KSP detached lower-periapsis preview candidate

`CircularLowerPeriapsisPreview.Create` previews a retrograde impulse from a
near-circular active-vessel orbit. It samples the future orbit state, computes a
circular-apoapsis estimate, applies Δv only to detached vectors, and checks the
result with `Orbit.UpdateFromStateVectors`. It returns a proposal only when the
result is a finite bound ellipse above the body and its PeA matches the requested
target within the supplied tolerance. The proposal's scalar values are read-only
after construction; its `StartingOrbit` and `Body` are live KSP references and
must be revalidated before any later action.

The adapter requires the active vessel to be orbiting on an active patch with no
existing maneuver nodes, rejects negative or excessive starting eccentricity,
checks finite orbit/body values and a surface-clear target, and requires the burn
UT to be future-dated. For patches whose `patchEndTransition` is not `FINAL`,
`EndUT` must be finite and the burn must precede it. KSP describes `FINAL` as
meaning there is no following patch, without documenting an `EndUT` sentinel; for
that case the adapter validates the requested time by propagating and checking
the future state vectors. This path has only been compiled against local KSP
managed assemblies; it has not been loaded or runtime-tested in KSP.

This is still a two-body detached prediction, not stock patched-conic solver
verification. It does not model SOI changes, atmospheric drag, terrain, finite
burn duration or target encounters, and it does not create a node or control a
burn. These API semantics are documented in the [KSP Orbit reference](https://anatid.github.io/XML-Documentation-for-the-KSP-API/class_orbit.html),
including Y/Z-flipped state vectors, `UpdateFromStateVectors`, `activePatch`,
and `FINAL` transition meaning. The solver node list comes from the [PatchedConicSolver API](https://kspmoddinglibs.github.io/KSPDocsSite/class_patched_conic_solver.html).

## Two-body Lambert prototype

`Lambert.SolveShortWay` computes a zero-revolution, short-way transfer between
3D position vectors in one central-body frame. It returns departure and arrival
velocities; it rejects collinear geometry, invalid inputs and cases where it
cannot bracket a well-conditioned solution. It does not select a launch window,
solve long-way or multi-revolution transfers, model sphere-of-influence crossings
or check collisions. A small internal time residual is not an independent proof
that the transfer is operationally safe.

The numerical tests cover a circular quarter-orbit analytic benchmark and a
noncircular transfer at 300, 1200 and 3000 seconds, verified with independent
20,000-step Runge–Kutta propagation. These are finite examples, not a full
accuracy guarantee. Before use for node creation, add broader geometry/time
coverage and compare the candidate against KSP's actual patched-conic trajectory.

An independent Luna subagent supplied a deterministic randomized propagation
regression: 2,000 short-way cases with nondimensional radii 0.8–1.2 and flight
times from about 0.2 to 10 characteristic periods. Independent fixed-step RK4
propagation reported no position or velocity relative error above 1e-5; the
worst observed was about 1.2e-10. This does not cover SOI transitions,
near-collinear cases or extreme radius ratios and does not validate KSP frames.

The `NodeFrame` helper converts a world delta-v into radial-plus, normal-minus
and prograde components using an orthonormal velocity-based basis. A Luna
subagent checked the signs against [MechJeb's source](https://github.com/MuMech/MechJeb2/blob/dev/MechJeb2/OrbitExtensions.cs)
and tested circular and eccentric vector round-trips. The radial-plus vector
is perpendicular to prograde and is not generally the direct radial vector in
an eccentric orbit. The inputs must share a right-handed inertial frame; KSP's
Orbit coordinate conversions and `ManeuverNode.GetBurnVector` still require
runtime verification before this can safely write nodes.

## Burn-approval guard

`BurnApprovals` is a pure workflow guard for a future trusted identity adapter.
It requires the assigned Hamish and Basil roles to resolve to distinct authenticated
principals, enforces the request's round-trip delay and expiry, clears approvals
when a plan is revised or connection is lost, and checks the live plan fingerprint
again at readiness. The adapter must supply a canonical fingerprint covering the
vessel, node, burn time, vector and target, and must bind role to authenticated
session identity. Calling code must not use a KSP crew name as proof of a human
identity. This class does not authenticate, communicate, persist, create nodes or
execute a burn. It is not connected to the shift system or multiplayer yet.
Single-player rehearsal requires a separately identified rehearsal path and must
never be presented as two authenticated people.

Following Luna review, the guard rejects reused request IDs for the current
session, enforces a trusted configured minimum delay and expires at the exact
window boundary. `PlanFingerprint` hashes vessel, node, body, target, frame,
burn time and all three components. The caller must recompute it from the live
node, observe every mutation and include any additional mission-specific
constraints. IDs and identity canonicalization remain the trusted adapter's
responsibility; the current guard is in-memory and is not a replay-proof
persistent protocol across process restarts.

## Finite burn and watch coverage candidates

Luna agents supplied the C#7.3 constant-thrust burn estimator and fixed-window
watch predicate. The estimator uses SI units, the rocket equation and mass flow,
rejects insufficient propellant and handles tiny delta-v without cancellation.
It excludes staging, throttle/pressure variation, spool-up and trajectory losses.
It cannot certify an actual KSP burn until usable propellant and engine data are
read from the correct active stages and independently checked.

The watch predicate checks the entire fixed ignition/preparation/burn/verification
window against the watch and both reviewers' awake/fit windows. It never shifts
UT or pauses a watch. Half-open windows permit an operation to end exactly at the
watch boundary but reject any positive extension beyond it. These are pure
models: no shift, daily routine, coast or flight-control adapter is connected yet.


## Isolated candidate builds

`tools/build.py` accepts `--output /tmp/megacrew-candidate/MegaCrew.dll` alongside
the existing `--managed` and `--csc` arguments. This compiles the branch without
replacing its generated GameData DLL. A successful build does not install the
plugin or establish in-game behavior. Keep this branch's older expanded base
separate from the current campaign package; source compatibility with the latest
cumulative campaign patch is checked in a disposable integration tree.

Approval requests now reject a communication round trip that reaches or passes
ignition. The reply must also arrive before the half-open expiry boundary. This
prevents a request from waiting on a response that could never be accepted; it
does not supply a real communication link or establish two human identities.


## Read-only onboard panel

The standalone navigation addon exposes the preview through a stock toolbar
button and an IVA-accessible overlay. It accepts target Pe in kilometres and a
lead time, shows current orbit plus detached predicted result, and invalidates
results when inputs, physical orbit, vessel/patch or validity window change.
It does not connect the approval helper to flight controls.

See [the candidate validation checklist](NAVIGATION-PREVIEW-VALIDATION.md) for
controls, scope and pending in-game checks. The panel has compiled against KSP
assemblies, including in a disposable 0.3.4 campaign source integration. Toolbar
visibility in IVA, text/mouse input isolation, stable previews and lock cleanup
remain runtime acceptance gates.
