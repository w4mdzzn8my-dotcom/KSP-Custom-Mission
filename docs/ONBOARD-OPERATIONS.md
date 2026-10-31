# Onboard operations candidate

Open **OPERATIONS** or **Ctrl+Alt+O** (Control+Option+O on Mac) in flight/IVA.
Only MegaCrew-prefixed saves and non-EVA vessels expose the panel.

| Station | Implemented behavior |
| --- | --- |
| Pilot | Altitude, surface/vertical speed, apsides, inclination, time to apsides, existing maneuver node time/delta-v |
| Flight controls | SAS on/off and stock-supported stability/prograde/retrograde/normal/radial/maneuver modes, requiring a selected Pilot and shared duty readiness; disabled during pause/warp |
| Rendezvous | Internal same-body vessel selection, synchronized-epoch range/closing/relative-speed readouts and a bounded 60-second linear projection |
| Science | Run fitted stock ModuleScienceExperiment modules, show experiment ID/deployment status and stored data from science containers; selected Scientist and shared readiness required for the panel Run action |
| Supplies | Actual EC/Food/Water/Glykerol totals, low-reserve labels, two-second net rates and depletion estimates |
| Cryo checks | Orbit situation, crew roster count, reserve charge, Glykerol total and DeepFreeze connection; manual readiness sign-off |
| Log | Orbit survey and station actions recorded with UT and vessel name, last 100 entries retained per save |

The engineer panel uses Ctrl+Alt+E. Its window ID now differs from the 0.3.0
room panel. Existing upstream crew needs and room controls remain available on I.

## Limits

This is a local single-player overlay; no LMP synchronization, physical interior
prop binding or new trajectory planner is included. Normal commands require a selected roster operator and the shared local duty
query. Physical room occupancy and authenticated player identity remain separate
requirements. The campaign owns needs/recovery; this addon does not duplicate
its fitness thresholds.

Science uses the stock deploy routine/result dialog and its ordinary restrictions;
no results are fabricated, discarded or automatically transmitted. Stock science
points may not be meaningful in the Sandbox practice save. SAS capabilities still
depend on actual stock pilot/probe capability. Maneuver nodes are displayed, not
created or executed. No throttle or staging is automated.

Resource rates are recent samples rather than a power/cooling simulation. A meal,
transfer or pod operation can temporarily dominate the estimate. Clock jumps,
reverts and scene/vessel changes reset sampling; there is no offline consumption.
Cryo checks do not calculate pod-local costs, future sunlight or trajectory
safety, and never freeze crew or accelerate time by themselves.

## Persistence and packaging

`MegaCrewOperations.cfg` contains only journal entries and is separate from the
existing mission state. A failed write reports failure while retaining entries in
memory. Entries are capped at 100. Rates and UI position are intentionally transient.

The source checkout includes the exact upstream 0.3.0 crew/interior sources plus
these addons. The rebuilt DLL is a candidate, while the published 0.3.0 ZIP still
contains the upstream release. Extracting that ZIP over the checkout removes the
candidate build. Restart KSP before installing a candidate DLL; do not replace it
while the game is running.

## Verification

The full combined source compiles against KSP 1.12.5. Production resource-model
tests pass for depletion, recharge, steady state, warp/load gaps, clock regression,
invalid input and reset. Upstream needs-model tests and six Python preflight tests
pass. These checks do not exercise Unity UI or stock flight effects.

Pending flight checks:

1. From IVA compare orbital instruments with stock values and existing nodes.
2. With a pilot aboard toggle SAS and each supported mode; unsupported modes and
   pause/warp must disable commands. Verify no command executes a burn.
3. Run a fitted experiment with a scientist aboard. Confirm the stock result
   dialog and data listing; rerunning an inoperable/deployed experiment disables.
4. Consume/generate EC; compare the trend with observed totals. Change vessel,
   reload and revert to confirm stale estimates disappear.
5. Record surveys/readiness checks, save/reload and switch save folders. Confirm
   journal isolation and no duplicated supplies or stock science data.
6. Open engineering, operations and I panels together. Check distinct windows,
   mouse-over control locks, closing and scene-change cleanup.

No flight pass is claimed for this candidate. Use a disposable test save.


## Rendezvous station source candidate

The Rendezvous tab keeps target selection within the Operations overlay; it does
not set a stock target or command the vessel. See
[the rendezvous instrument guide](ONBOARD-RENDEZVOUS.md) for sign conventions,
linear-horizon interpretation and the pending runtime checklist. The geometry
model uses the same observation epoch for both vessels. Loaded and unloaded
target state is labelled; the active vessel must be loaded, unpacked and orbiting.

Builds can use `tools/build.py --output /tmp/megacrew-candidate/MegaCrew.dll` to
avoid overwriting the branch's generated DLL. Integrate only these addon source
files with the newest campaign source; do not replace newer campaign/needs code
with this branch's older expanded baseline. Assembly compilation is source-level
evidence; IVA controls, target readouts and cache invalidation require a
disposable flight run before release.

The read-only docking-port panel selects loaded, same-body ports and displays
their measured relative geometry. See [onboard docking instruments](ONBOARD-DOCKING-INSTRUMENTS.md)
for the axis signs, invalidation rules and pending runtime checks. It does not
recommend or perform a docking maneuver.


## Retained-science observation candidate

The Science tab now supports a transient before/after observation of actual
stored reports, alongside the existing stock experiment controls and inventory
listing. A current stock science context label helps the crew identify the
situation being discussed. See [science observations](SCIENCE-OBSERVATIONS.md)
for interpretation, provider limitations and pending flight checks. Changes in
retained data do not certify experiment completion or science rewards.


## Shared duty query integration

Normal Pilot/Scientist actions now select a named operator and consult the
campaign's shared readiness both for display and dispatch. Read-only observations
remain trait-neutral. See [duty-gated stations](DUTY-GATED-STATIONS.md) for the
current 0.3.6 recipe and pending live refusal/recovery checks. Earlier trait-only
notes above describe the previous candidate; they no longer authorize these
mutating commands when the shared API is unavailable.


## Observed power survey

Supplies now includes a transient 60-second ElectricCharge survey. It records
actual samples and stops on gaps or changed vessel context; it forecasts no
endurance or cryopod readiness. See [power surveys](POWER-SURVEYS.md) for
measurement interpretation and the pending live checklist.
