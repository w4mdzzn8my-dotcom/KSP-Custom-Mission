# MegaCrew KSP campaign implementation and acceptance plan

**Basil host runtime hold — 31 October 2026:** Basil is playing KSP with Hamish
and explicitly asked that Codex neither open KSP nor close/kill the game he is
using. Continue source work, metadata inspection and compile/model tests only.
Do not launch game tests or change running installation files until Basil says
runtime work may resume. This holds even if a later process check finds no KSP.

**Current development branch: `basil/megacrew`.** The six former topic histories
are preserved in its merge ancestry. The working tree starts from the published 0.3.7 core plus onboard/navigation
additions, Finch craft and visuals. A subsequent bounded console layout fix
intentionally changes MegaCrew.cs; the manifest records that deviation.
The 40-source plugin rebuild, 82 Python tests, root model/boundary runners,
navigation cases and IVA catalog harness pass. These are build/model evidence.

The stable 36-source candidate separately completed an actual direct
outside-pod cryo refusal with DeepFreeze ready and unchanged crew/pod state;
see [the runtime record](CRYO-REFUSAL-RUNTIME-EVIDENCE.md). The native KSP load
lifecycle fixed the earlier missing-scenario initialization. The human UI test exposed an oversized/cropping console. A bounded layout
candidate now compiles; its click behavior awaits a permitted retest in
[Human Help Requests #11](https://github.com/w4mdzzn8my-dotcom/KSP-Custom-Mission/issues/11).
Full freeze/thaw, physical crew activities, lander operations, high-rate coast
and LMP/shared authority remain open. Historical ledgers below retain the refs
and limitations that applied when each result was recorded.

**Planning baseline:** cumulative core `f76e437`, newer upstream build-helper fix `3eddae5`, and open Issues/PRs checked on 2026-10-31. This plan preserves the complete requested campaign. It is an execution order and evidence map, not a claim that the work is implemented or qualified.


**Current reconstruction:** pristine published 0.3.0 ZIP → cumulative 0.3.7
(`f76e437`) ONCE → reviewed addon sources. Epoch planning, state guards and
the shared duty query are included. Do not stack 0.3.6/0.3.5, epoch/state packets or
previous core supplements. Historical ledgers below retain their older refs.
High-rate coast and runtime acceptance remain unqualified. First permitted
desktop work is the scoped cryopod QA/raw evidence review requested upstream,
then transfer refusal, duty refusal/recovery, input checks, cradle loading and
navigation comparison. Preserve the normal-play installation.

## Current baseline and operating constraints

- The repository README at this baseline still describes the published 0.3.0 package: single-player only, `MegaCrew*` time warp blocked, no offline simulation, no autonomous NPC maintenance, and no campaign-wide watch or LMP authority. It also marks generated-save load, complete practice, pantry persistence, cryosleep handover, IVA traversal and controls as needing further game verification.
- The 0.3.1–0.3.7 campaign work is available as source-only patch artifacts and issue updates, not as a published replacement package. Integrate the latest cumulative patch against its documented 0.3.0 base in an isolated review tree; do not stack cumulative patches or overlay an old ZIP/DLL onto newer source. PRs #4–#8 remain drafts; their reports have candidate-level scope and do not establish release acceptance.
- A user update in Issue #9 explicitly asked that KSP not be used until about 9 o'clock, noting that the Mac may lock. The 31 October morning attempt occurred after 09:00 Sydney with an unlocked desktop. It reached the main menu but usable UI capture failed, so flight/cryo checks remain inconclusive. Future runtime work still requires an available desktop, isolated copy and matching inputs.
- Preserve the user's original saves, the Luna server Universe, installed server configuration and backups. Use a separate clean KSP 1.12.5 build 03190 copy, disposable QA saves, pinned dependencies and backups for all runtime gates. Do not post credentials, private paths or raw unredacted logs.
- Evidence must distinguish source inspection, model/unit tests, successful compilation, KSP main-menu startup, actual single-player flight behavior, save/reload/revert behavior, and two-client LMP observations. A lower tier never substitutes for a higher tier.

## Campaign contract to preserve

- Twenty-four mission months use the documented calendar convention of 30 days × 24 hours per mission month. Six alternating four-month watches: human Hamish/Basil, then Ada/Vikram relief, repeated three times. The human pair must actually be in DeepFreeze for their NPC watches; a console label or roster assignment alone is insufficient.
- Each human awake month targets 80 minutes of active, varied play (5 h 20 min per four-month human watch). Each four-month NPC watch targets one minute of coast. The stated baseline is 16 h 03 min before alignment, burns, handovers and other added play. Targets are scheduling goals, not permission to leave an idle timer running.
- KSP Universal Time and the actual orbital trajectory/resource behavior are authoritative. Coasting advances real UT and follows the map/orbital trajectory. Burns consume actual fuel and their alignment, ignition, execution and verification take real play time. Watch/mission clocks do not stop for a burn, and burn UT must not be silently shifted to force it into a watch.
- Both Hamish and Basil verify each programmed burn. Before LMP, single-player role gates are rehearsal only and cannot prove two human approvals. In LMP, approvals must bind to distinct authenticated client principals, the immutable current plan revision, a live fingerprint, a communication/request round trip and an unexpired approval window. Any plan edit, stale response, disconnect or identity failure clears readiness and blocks execution.
- Exactly **one planned emergency** occurs in the full expedition. Its schedule is confidential: keep it out of plans, source comments, issues, public lore, test output and ordinary reports. Routine maintenance, social/care work, drills, injuries from training and normal mission events are not emergencies. If an emergency story is authored, it implements/replaces the one planned event; it does not add another. Cooperative tone only: no impostors, murder or sabotage.
- 80-minute months require multiple useful investigation → measurement → decision → action → observation → verification → record activities. Do not use an empty countdown, repetitive checkbox padding, imaginary sensors, invisible repairs or an “all fixed” flag.

## Dependency-ordered work plan

### 0. Reconcile source, ownership and patch baseline

**Work**

1. Obtain a verified snapshot of upstream `main` and its published 0.3.0 archive; record commit, archive hash, dependency manifest and KSP build.
2. Apply the newest cumulative campaign patch (v0.3.7 source patch) once to a disposable copy of the documented 0.3.0 source. Verify the patch's included source/docs/tests against the patch manifest and run its packager comparison. Do not stack older cumulative packets or the superseded console/supply/QA supplements with 0.3.7. The epoch planner is included in this cumulative packet and must not be applied separately.
3. Preserve upstream-owned 0.3 files and define one maintainer/merge boundary per subsystem. Keep separate contributions in separate source files until reviewed; reconcile PR #5's exact-source integration with upstream room/needs ownership rather than merging its older compiled DLL.
4. Maintain a single integration build manifest with source hashes, dependency archive hashes, DLL hash and origin for each test copy. Keep third-party assets and personal server data out of packages.

**Gate 0 evidence**: clean patch apply; package/hash comparison; deterministic test/build commands documented and reproducible. No game test required.

### 1. Restore a known single-player test foundation

**Work**

1. Build the combined current source against KSP 1.12.5 build 03190 assemblies. Check compiler output and ensure the candidate DLL corresponds to this source revision.
2. In a clean separate game copy, install exact pinned SSPX + IVA extras, B9PartSwitch, NearFutureProps, ModuleManager 4.2.3, FreeIVA 0.2.20.2, DeepFreeze 0.31.0.0, CRP, EVS 2.3, and the agreed EVE/BoulderCo visual set. Validate hashes and avoid duplicate ModuleManager/cloud packs.
3. Verify startup logs report exact mod/plugin versions. Run the preflight and needs/model suites, campaign state codec, timing, coast decision, supply adapter and 0.3.7 cryo audit tests. Treat DLL compilation and model tests as code evidence only.
4. After runtime restriction clears, load the generated practice save from the clean copy. Test physical stability, actual vessel and crew manifest, save/reload/revert, room panel interactions, input-lock cleanup and known collider/warning impact. Keep the centrifuge stopped pending its separate qualification.

**Gate 1 evidence**: reproducible install hashes; clean startup with version logs; save load and return-to-flight; no evidence inferred from folder presence or the earlier 0.2 trainer. Record warnings and determine gameplay impact without editing existing saves.

### 2. Qualify the base ship, FreeIVA route, room controls and interaction state

**Work**

1. Verify the 150 km Kerbin practice orbit and all four mission crew in actual flight. Confirm vessel stability and correct IVA/FreeIVA compartments.
2. Walk command → quarters → lab/medical → centrifuge spine/corridor → cryobay. Test hatch traversal, colliders, camera/movement bindings, current-room and viewed-Kerbal association, panel clearance, concurrent I/F8/engineering/operations windows, click capture and cleanup after close/scene changes.
3. Validate physical stock device actions and state transitions: deploy/stow individual arrays/radiators; compare objective state to module animation and part state. No claim that an overlay is a physical IVA prop.
4. Verify room-based food/water/rest, medical treatment, observation logging and DeepFreeze calls against the actual viewed Kerbal, actual room, awake/frozen status, available supplies, pod occupancy, power and Glykerol. Verify restrictions do not block stock controls, treatment or recovery paths.
5. Complete the existing 10–15 minute practice mission end to end, including deployment, real 60-second EC trend, manual route sign-off, training injury treatment/recovery, Ada freeze/thaw, final orbit/crew checks and persistence. Separate manually signed tasks from automatically verified facts.

**Gate 2 evidence**: a fresh-start run and a saved/reloaded run with short redacted logs or screen evidence; verify all objective transitions and errors; specific behavior reports for input, room/crew gating and persistence. Current documented status still leaves several of these unverified.

### 3. Integrate onboard flight operations and navigation in single player

**Work**

1. Rebase/adapt PR #5's onboard engineering/operations candidate onto current 0.3.7 sources. Preserve its actual-device Extend/Retract events, orbital/node/SAS readouts, stock experiment execution, EC/Food/Water/Glykerol diagnostics and bounded per-save journal. Keep overlays as local UI; they are not shared authority or physically animated stations.
2. Perform the PR #5 checklist in a disposable flight save: compare telemetry to stock; test pilot/engineer/scientist gates, unsupported SAS modes, pause/warp disable, actual stock experiment result/data, supply sample resets and journal save/reload/save-folder isolation; test three panels and input-lock cleanup together.
3. Complete PR #8's numerical trajectory work in layers: pure math/range/property tests; KSP frame mapping to stock `GetBurnVector`; patched-conic patch/transition validity; maneuver node creation on a disposable vessel; orbital prediction comparison to KSP after propagation; invalid/near-collinear/extreme-time/refusal behavior. Extend from two-body Lambert into planet intercept/atmospheric periapsis/docking approach only with KSP-patched-conic validation and robust no-solution output.
4. Resolve PR #8 review cases: impossible approval window (`now + roundTrip` must leave adequate time before ignition), unique request nonce/generation and stale request rejection, auth role-to-principal binding and canonical principal identity, exact expiry semantics, disconnect/reconnect invalidation, and finite/range checks. Bind approval to complete canonical maneuver data and recompute fingerprint from the live node at dispatch. The caller must not omit UT, vector/frame, reference body, vessel, target or plan revision from the hash.
5. Before LMP, permit only clearly labeled single-player rehearsal where the adapter explicitly admits it cannot establish distinct humans. Execute no node unless plan fingerprint/revision, two valid approvals, comm state, UT window, watch coverage, vessel identity and fresh simulation all pass.

**Gate 3 evidence**: source/model/property tests, KSP assembly build, disposable flight run of generated node and subsequent actual orbit/fuel change agreeing with prediction within declared tolerance; proof stale/invalid plans fail closed. No planet-intercept or rendezvous feature is accepted on math tests alone.

### 4. Qualify and integrate the Finch lander/storage system

**Work**

1. Resolve PR #6's open craft-loader/fixture ambiguity using the corrected orbital QA fixture with stock PreAttached port state. Determine whether the earlier 18/35 part retention was a fixture artifact; do not count successful `LoadShip` as docking qualification.
2. Reconcile cabin/SSPX variant, carrier hierarchy, docking port parent and loaded/unloaded mass/delta-v. Confirm root, attachment chain and matching port state in the editor and actual flight.
3. Verify cradle attachment and separation, collider/clearance around Hermes, two crew seats, propellant transfer/isolation if designed, safe stored configuration, undock/decouple, controlled Mun/Minmus low-orbit descent and landing, EVA, ascent, rendezvous/redock, crew recovery, save/reload and failure recovery. Include abort windows and real propellant margins; distinguish ideal delta-v from measured flight performance.
4. Keep the craft scope honest: Finch is a vacuum lander candidate. No launch booster, atmospheric return or full Kerbin recovery claim until designed and qualified.

**Gate 4 evidence**: native craft load plus editor/flight observations for full attach → operate → recover loop and persistence in a disposable save; independent mass/propellant reconciliation. Until then, no lander readiness story or deployable/recoverable claim.

### 5. Implement finite needs, daily care, and measured maintenance workload

**Work**

1. Integrate the campaign source model with actual KSP UT and persisted scenario state: human crew needs, actual frozen-state pause, finite meal/drink/rest/hygiene/exercise/social tasks, injury/treatment/recovery, daily schedule, cooldown and per-save journal. Keep all need values explicitly fictional gameplay abstractions, not human medical advice.
2. Test each activity against the actual room, currently viewed Kerbal, awake/available person, 1x clock and cancellation rules. Save/reload/revert mid-activity. Confirm pause, warp, scene/load gap, vessel change, freeze/thaw and crew transfer handling.
3. Reconcile real Food/Water transactions and conservative transitions using actual flow-enabled vessel stores and verified DeepFreeze state. Validate supply ledger deltas against part resources, no duplicate charge at repeated UT, locked stores, depletion, insufficient supply, clock regression, gaps and all freeze/thaw transitions.
4. Resolve background provider concerns before any coast: pinned DeepFreeze active-vessel checks are rate-limited (EC below warp index 5; thermal below index 2). Do not simulate safety by hiding huge rates in lower indices. BackgroundResources handles unloaded vessels but its DeepFreeze IL requires correction/verification; proposed patch is source/IL evidence only, not game qualification. Inspect exact provider hash, consumption direction, elapsed-UT arithmetic, per-pod frozen count, shortage/recharge timestamps, settings, active/unloaded transition and save/reload in a disposable probe. Do not patch third-party binaries without exact-hash guard, separate output, preserved original and review.
5. Measure complete resource and pod-power margins over representative light/dark/eclipsed orbits and actual onboard generation. A pod-only endurance estimate is not whole-vessel safety. Include EC generation/loads, pod thermal requirements, Food/Water, reserves, and a verified horizon to the next stop. Report assumptions and fail closed on unknown telemetry.
6. Address modeled versus real agency: NPCs are not autonomous today. Add durable, auditable NPC watch-maintenance tasks only when actions affect actual modeled state and resource/work logs; never silently repair craft or supply needs for free. Simulated tasks must remain distinguishable from live physical KSP effects.

**Gate 5 evidence**: production model tests plus actual KSP 1x supply and pod telemetry deltas across load/save/revert, transitions, shortage, dark orbit and disabled-setting cases. No accelerated coast until provider behavior and resource horizon are game-qualified.

### 6. Add full 24-month watch progression and safe coast control

**Work**

1. Integrate the pure CampaignTiming/CampaignCoast contracts with an explicit campaign-start scenario module storing epoch UT, original vessel GUID, four-crew snapshot, schema/version and fail-closed load errors. Test quicksave/quickload/revert, save switch and campaign completion: six watches, no wrap, no retroactive offline progress.
2. Validate target arithmetic: 30-day months; 4-month watch length; 540× human coast (one 30-day month in 80 minutes); 172800× NPC coast (four months in one minute); 57780 seconds overall baseline before operational work. These are targets only, never assume KSP/DeepFreeze safely supports the requested warp values.
3. Implement controlled coast only through stock warp/rate APIs. Every dispatch takes fresh observations; only campaign vessel; confirmed watch role/state; actual crew freeze/thaw; verified pod power/temperature and background accounting; resource coverage to the conservative next stop; no missing/stale UT, halt, due event or completed campaign. Brake before watch boundary, burn alignment, next maintenance, alert, eclipse or handover. Respect KSP altitude, physics-warp and rate safety limits; never set UT directly.
4. Coast through actual trajectory, resource depletion/generation and current celestial mechanics. No time shifting, instant travel, universal freeze, resource replenishment, background assumption or skipping an unknown event. Catch-up must be based only on validated, continuously accounted UT and reject unobserved gaps.
5. Build a dispatcher simulation with boundary/property tests for fresh/stale observation, stop windows, all six watches, safe braking, pause, halt, low resources, pod/API failure, light/shadow transitions and save/revert. Then validate at 1x with one loaded vessel and short safe coast before any candidate-rate test.
6. Only after short-rate performance/pod/supply checks pass, gradually qualify higher rates in a disposable save. Test interaction with stock warp, scene changes, time-warp limits, DeepFreeze checks, BackgroundResources, burn planner and rate changes. If the 540×/172800× targets are unsafe or not representable, document a safe achieved rate and honest longer play time; never bypass safety throttles.

**Gate 6 evidence**: documented runtime table of requested/actual warp, UT progression, pod temperature/EC/resources, orbital state, crew, stop latency and save/revert consistency at every accepted rate. Full six-watch completion required before claiming a complete expedition.

### 7. Integrate maneuver execution with watches, burn timing and approvals

**Work**

1. Bind each immutable plan revision to exact node inputs and predicted consequences. Include outbound/return comms delay assumptions, authority source, request nonce, expiration and change audit.
2. Schedule all burn windows inside human awake watches, allowing alignment, distinct Hamish/Basil verification, ignition, execution and post-burn verification. Do not pause the shift clock during burns. Ensure `CoversBurn` checks the *whole* operation interval and does not move burn epoch; replan when a watch or operational stop cannot cover it.
3. At each coast decision, stop for validated burn-alignment window, request/ack round trip, approved burn and observation period. If communications are lost, node changes, identity cannot be authenticated, timing is stale, trajectory verification fails, ship is not the campaign vessel, or fuel/resource checks fail: coast may stop/hold; do not burn.
4. Verify actual maneuver node and resulting UT/orbit/fuel before and after execution. Keep an auditable persisted approval/result journal tied to plan hash/revision and authenticated clients in the future LMP path.

**Gate 7 evidence**: unit tests for boundaries, impossible windows, stale nonce/revision, disconnect/regain, plan-field mutation, duplicate principal, authentication changes and expiry; KSP orbital flight with actual node execution and predicted/observed delta comparison; tested pre/post-burn save/reload. No approval may be represented as two humans in single-player.

### 8. Build maintenance and EVA work as deterministic, recoverable gameplay

**Work**

1. Define a small set of routine degradations/maintenance counters driven by actual module/resource/temperature/EC/orbit state. First failures should be diagnosable and reversible, not random damage or destructive sabotage. Use actual stock/installed components where possible; clearly mark any custom simulated work order.
2. Provide at least four varied 80-minute-month work patterns: resource/pantry reconciliation; power and thermal trend observations; orbital tracking/observation; cryobay readiness/handover; science operations; storage/lander readiness; EVA inspect/replace/repair only where actual part/module action can be verified. Use useful activities during natural observation/rest intervals instead of empty timers.
3. Gate exterior EVA by crew health/readiness, suit/EVA resource and vehicle state, route/airlock access and at least one safe return operator. For repair, make the work alter a real component/state and persist; do not claim physical repairs from a manual checkbox. Define wrong-diagnosis recovery, resupply, safe abort and handover records.
4. Add automated evidence only for actual telemetry/state. A manual sign-off can document inspection but never assert traversal, repair or science success that the code cannot observe.

**Gate 8 evidence**: each maintenance story replayed with success and recoverable mistake paths; actual component/resource changes, persistence and handoff journal demonstrated; all tasks use real clocks with clear active-play estimates. Verify target month contains varied useful work without repetitive or idle padding.

### 9. Integrate Luna LMP and shared authoritative state

**Work**

1. Begin only after single-player systems, watch/coast safety, navigation/burn checks and lander foundation have passed their preceding gates. Preserve the user's existing Luna server and Universe; make a disposable test instance/Universe and tested backups. Existing Luna 0.29.2 alone does not synchronize custom objective, health, routine, resource ledger or approval state.
2. Specify a single authoritative state owner/authority for vessel, crew, resource consumption, health/needs, story variables, watch state, maneuver plan/revision, approval records, emergency lifecycle and save serialization. Define conflict resolution, duplicate/replay rejection, reconnect snapshot, host migration (if supported), schema migration and audit events before implementation.
3. Authenticate separate clients. Bind each account/session to one allowed crew role through trusted multiplayer identity, never UI text, Kerbal name, roster membership or local single-player flags. Distinct Hamish/Basil approvals must be from distinct authenticated clients. Recheck auth and plan hash at execution.
4. Validate matched KSP/mod/DLL/config versions, port/firewall/session setup without exposing secrets, shared vessel and Kerbal state, IVA occupancy/current Kerbal, stock controls, LMP physics and custom replicated commands. Start with one authoritative shared engineering action; observe the same state transition and journal entry on both clients.
5. Add network-fault tests: latency/round-trip, packet reorder/duplicate/loss, disconnect during action/approval/burn, reconnect, simultaneous actions, stale authority, game pause/time warp disagreement and save conflict. Fail closed and reconcile to authority; never let two clients independently consume or repair the same state.
6. Extend tests to shared crew needs, actual stores, DeepFreeze handover, campaign coast, orbital node and post-burn state only after single-owner synchronization is stable. Test a full session with two clients sharing the same Sandbox vessel/Kerbal. The old server Universe remains untouched.

**Gate 9 evidence**: two independent client logs/video/screen observations of shared vessel and crew; same authoritative actual resource/objective/mission state before and after reconnect; distinct authenticated Hamish/Basil approvals; controlled induced network failures recover without duplication/desync. A local overlay on two clients is not LMP success.

### 10. Author and implement the cooperative story campaign

**Work**

1. Preserve the #7 “Before the Handover” scene as routine maintenance/social/crew-care: pantry ledger mismatch, sample-observation versus feeding/rest/medical priorities, truthful diagnosis, actual Food/Water and observational data, cryopod power/Glykerol, corrected records and verified relief watch. It is not an emergency.
2. Author all stories against a capability matrix and the supplied issue template: place/crew/starting state, observable problem/clues, actual supplies/equipment, sequenced tasks, active time, dialogue/social choices, outcomes/recovery, handover state and acceptance evidence. Tag each item existing/implemented/proposed. Do not invent sensors, working repairs, NPC autonomy or on-screen animation as already implemented.
3. Sequence the roadmap chapter themes reported in the coordination thread: **First Watch**, **Warm Loop**, **Quiet Side**, **Lander Readiness**, **Red Horizon**, **Long Way Home**. Use a clear destination campaign (Duna is current starting suggestion; Laythe remains an alternate not yet selected) and introductory command-pod/mothership flight as onboarding only after the underlying vessel/flight path is qualified.
4. Implement story triggers, state, branching and replay/recovery in the shared authoritative campaign layer. Scripted animated Kerbal handoffs are a requested feature; first define animation/IK/room pose, crew ownership and LMP synchronization gates. Until implemented, describe dialogue/handovers as UI or player performed rather than seamless animation.
5. Add exactly one planned-emergency record/lifecycle with single issuance and resolution, duplicate prevention across save/load/reconnect and clear separation from routine maintenance. Keep its schedule in private runtime state that is never surfaced in docs, plans, logs or story templates. Test uniqueness without displaying the scheduled time.
6. Include human care and maintenance without extra “emergency” labels: meals, drinking, sleep, exercise, hygiene, social care, medical care and useful routine tasks. Keep threats cooperative; exclude murder, impostors, sabotage.

**Gate 10 evidence**: each chapter replayable on two clients; trigger, state, dialogue choice, actual task effects, recovery and handover persist through save/reload/reconnect; active-play timing is observed; one-emergency uniqueness invariant passes while its scheduled time remains undisclosed.

### 11. Release, documentation and joint acceptance

**Work**

1. Reconcile main README and dependency manifest with actual qualified artifact, controls, campaign defaults, supported KSP/mod versions and limitations. Do not describe future targets as implemented. Document deterministic test/build/run commands and clean-copy install/update/rollback.
2. Build a release archive from reviewed source with pinned hashes and license/redistribution boundaries. Confirm no personal saves, server files, credentials, unreleased emergency timing or unrelated visual prototype assets are included.
3. Verify install on both Macs with separately prepared copies. Capture exact KSP/plugin/mod versions and short redacted diagnostics. Run complete single-player campaign before final two-client LMP session.
4. Use focused PRs, one integration owner per file/subsystem and trace each acceptance criterion to evidence. Keep draft PRs unmerged until their specific runtime gates pass; do not replace a newer installed combined build with an older candidate.
5. Final joint review with Hamish and Basil: fresh campaign, realistic campaign timescale, actual burns, alternating watches, co-op decisions, lander operations, recovery and ending; confirm no pre-existing save/Universe changed.

**Gate 11 evidence**: release checksum; clean install and reproducible build; complete documented single-player and multiplayer evidence; all release-gate rows signed with evidence URLs/log references; owner review and explicit acceptance. Until all gates pass, state that the project remains incomplete.

## Immediate next actions

1. Treat the bedtime request as no KSP runtime tests before 09:00 Australia/Sydney on 2026-10-31 (the next morning after the request). Continue source/model work in the meantime; inspect availability before resuming runtime tests.
2. Review the source-only v0.3.7 patch and PR #8 pure timing/approval contracts against the exact base without installing a new DLL. Add deterministic coverage for impossible approval windows, request replay/nonce, full burn watch coverage and canonical plan hashes.
3. Resolve the PR #6 test-fixture state and mass/hierarchy evidence in a disposable environment after runtime is allowed.
4. Build a test matrix and collect missing single-player evidence for Gates 1–5 before enabling any accelerated coast or declaring lander readiness.
5. Keep LMP and in-game stories downstream of qualified single-player foundations and lander operations.

## Status summary at review time

Source/model and compilation work has progressed: 0.3.7 source-only campaign/care/accounting/cryo audit candidates; PR #5 single-player onboard overlay candidate; PR #6 craft loader evidence; PR #8 bounded orbital math and approval/timing candidates. Runtime/high-warp/pod-power/lander flight qualification remains explicitly pending in upstream updates. LMP, synchronized custom state, two-human approval, physical EVA maintenance and authored playable stories remain future acceptance gates. The bedtime request is treated conservatively as no KSP runtime tests before 09:00 Australia/Sydney on 2026-10-31; source/model work continues. No emergency schedule is reproduced here.


## Source progress ledger — overnight development pass

This ledger supplements the baseline above; it does not advance any runtime gate.

- **Gate 3 / PR #8, `e85e0dd`:** detached KSP orbit preview for a near-circular
  lower-periapsis retrograde impulse; requires active vessel/patch, future UT,
  no existing nodes, finite body/state vectors and a predicted PeA within the
  requested tolerance. Handles FINAL patches without assuming an EndUT sentinel.
  It creates no node or burn. Updated disposable QA compares this prediction with
  the circular model and stock next-patch orbit when it is eventually run. Plugin
  and probe compile against KSP assemblies; runtime comparison remains pending.
- **Gate 7 / PR #8, same commit:** positive-delta-v consumption/duration underflow
  now rejects; insufficient fuel no longer receives a tolerance clamp. Default
  interval structs reject at containment checks. Reviewer intervals must be
  refreshed at approval and dispatch. Full navigation runner passes, including
  2,000 randomized two-body cases, 37 estimator assertions and 27 coverage cases.
- **Gate 4 / PR #6, `c0e6d24`:** a read-only serialized-vessel validator checks
  IDs, parent tree/order, attachment and symmetry indices. Eleven portable tests
  plus nine existing structural/setup checks pass. The prior saved 35-part QA
  fixture reports only the cabin's forward parent (18 -> 21). Its surface links
  are valid. The preceding generator reorder still requires a new native load;
  the validator does not establish docking or flight readiness.
- **Gate 10 / PR #8:** added a routine post-burn handover coda to The Burn Brief,
  using observed orbit/fuel and a player-written record. It claims no automatic
  post-burn verification, synchronized log or animation.

**Next bounded work:** after the runtime restriction clears, compare detached
preview, stock node next patch and observed orbit in a disposable fixture; load
the reordered 35-part cradle and require loaded/unpacked full part count before
examining release. Until then, source integration and provider/coast audit work
can continue. Do not install these branch builds over the newer campaign package.


### Read-only station and source reconciliation checkpoint

PR #8 commit `11f757e` adds the separate onboard navigation-preview toolbar
panel, with target/lead input, current orbit, detached predicted Pe and signed
prograde delta-v. It invalidates changed/expired inputs and physical orbit state,
handles propagation errors, and manages ship-control locks around input. It
creates no node, burn, warp or approval. Toolbar/IVA visibility, click isolation,
lock cleanup and prediction stability remain pending runtime checks in
`docs/NAVIGATION-PREVIEW-VALIDATION.md` on that branch.

The same commit rejects impossible approval requests: replies must arrive
strictly before both ignition and the exclusive expiry. The navigation runner
passes including the new boundary cases. `tools/build.py --output` now permits
compilation directly to temporary output without overwriting a generated DLL.

A disposable source integration applies the pinned cumulative 0.3.4 patch once
to the published 0.3.0 ZIP and overlays the nine navigation source files. All
22 combined C# files compile against KSP 1.12.5 assemblies. Package preflight,
needs and campaign tests pass. Exact input/source/output hashes and reproduction
steps are recorded in `docs/NAVIGATION-INTEGRATION-AUDIT.md` on #8. No campaign
source ownership changed, no provider was installed and no game was launched.
The Burn Brief gains a short routine planning dialogue; it assumes no automatic
maneuver, animated handoff or extra emergency.


### Reproducible candidates and morning QA preparation

- **Gate 0 / #8 `9737865`:** `tools/build_navigation_candidate.py` reconstructs
  the pinned 0.3.0 archive plus one cumulative 0.3.4 patch and an explicit tracked
  navigation ref into a new directory outside the checkout. It rejects input
  hash mismatches and unsafe archive paths, supports prepare-only, and records
  source, builder, compiler, reference-assembly and output hashes. Thirteen
  focused safety tests pass. A full promoted-tool build succeeds; generated-tree
  campaign and needs model tests pass. This is a review artifact, not an installer.
- **Gate 3 / #8 `25e24c1`:** the disposable circular node probe now requires
  exact save/marker authorization and a process-local one-use arm; it waits for
  a loaded/unpacked idle-engine fixture, compares detached and independently
  reconstructed apsides with stock nextPatch body/Pe/Ap, then verifies cleanup
  of only its own temporary node. It compiles against the combined candidate.
  No runtime pass is claimed. NodeFrame/GetBurnVector comparison remains a
  separate open frame-conversion gate; this prograde-only probe cannot prove it.
- **Gate 4 / #6 `3c1b8a9`:** design regeneration no longer erases historical
  loader observations. Those observations have a separate file and explicitly
  unknown exact runtime-artifact hashes. Current craft hashes accompany current
  design calculations. Temporary regeneration matches checked-in outputs; all
  22 Python checks pass. The reordered cradle still awaits native flight loading.
- **Gate 10 / #8 `418229a`:** Match the Ledger is a proposed routine handover
  comparing unlike mass records honestly before planning a standardized repeat
  measurement. It claims no missing part, repair or flight qualification.

Morning order remains: review and build the current combined source artifact;
use only disposable install/save copies; first establish full loaded cradle
state and stock-versus-detached prediction evidence. Then qualify UI/input and
actual flight behavior. Source tests do not advance those runtime gates.


### Onboard rendezvous instrumentation checkpoint

PR #5 `c08d4f6` adds an Operations rendezvous tab with internal same-body vessel
selection, including labelled unloaded orbit snapshots. Both orbits are sampled
at the same UT. Readouts include separation, signed closing speed, relative
speed and a bounded constant-relative-velocity minimum over the 60 seconds
after the observation. Boundary labels distinguish the start from the horizon.
Coincident positions have no defined radial/closing direction. The instrument
does not set the stock target or command controls, nodes, engines or warp.

The source clears stale observations on vessel/body/save/target changes, packing,
pause/warp, invalid time and clock regression. It shows observation time and age.
A pure C# model suite passes approach/recession, crossing, stationary/coincident,
horizon, invalid-input and overflow cases; resource-model and six preflight tests
also pass. Branch and latest campaign/operations/navigation integration builds
compile. These checks establish source compatibility only. Runtime comparison
with stock target instruments, loaded/unloaded behavior, reverts, UI interaction
and input-lock cleanup remain open in docs/ONBOARD-RENDEZVOUS.md on #5.

The branch retains an older expanded base; integrate only the addon sources with
the latest cumulative campaign patch and rebuild into a disposable output.
Pre-existing DLLs/ZIPs do not contain this new feature. No game was launched or
installed package changed during this pass.

PR #5 `62ec16c` adds Compare the Epoch, a proposed routine crew briefing about
reading identity, timestamp and projection horizon together. It awards no
rendezvous, docking success or new emergency.


### 0.3.5 baseline, stored-science observations and identity gates

- **#5 `2e7d806`, `4e1114b`, `7881126`:** retained-science baseline/compare
  observes actual stored report quantities, deduplicates identical report-object
  views and keeps distinct cards aggregated by subject. Reordered inventories
  do not manufacture progress. Read-only capture is trait-neutral for the human
  pilot/engineer watch; stock Run retains its existing Scientist gate. The
  original inventory view and stock controls remain. Failed captures clear old
  comparisons. Context/times identify each temporary observation; no science
  points or unique discovery is inferred. Pure tests pass and the final 31-source
  0.3.5 + epoch + operations/science + navigation candidate compiles. Nine
  underlying model/boundary suites pass; no runtime result is claimed.
- **Shared readiness release gate:** mutating onboard engineering/science/SAS
  actions still use roster traits, not the campaign's authoritative awake/fit
  CrewRecord state. Wire one shared duty query at display and execution; do not
  duplicate thresholds or treat traits as human identity. Human-watch stock
  experiment policy also needs reconciliation while Ada is frozen. Read-only
  data review does not grant command authority.
- **#8 `0b3342c`:** candidate builder now verifies and applies cumulative 0.3.5
  then the additive epoch packet, with manifests/provenance for both. Fourteen
  tests pass, including a dependent two-patch Git fixture; real prepare/build
  succeeds. Older 0.3.4 reconstruction instructions are historical.
- **#8 `3ea374b`:** epoch-planner endpoint review found no mismatch with full
  duty-window coverage. A future selected epoch remains only a pre-start
  proposal; revalidate at dispatch and initialize actual campaign UT once.
  Never rebind an active campaign or shift physical burn epochs. Production
  trajectory/dual-approval integration is still open.
- **#6 `3b0ffe6`:** orbital QA now validates both original practice and spawned
  craft GUIDs, expected save/body and loaded/unpacked state across waits. It
  starts observation explicitly after spawning and requires 35 live/35 snapshot
  parts before docking diagnostics. Probe compiles; runtime switching/load and
  release/redocking are pending.

What Stayed Aboard is a proposed routine scene attached to the science feature.
It distinguishes measured retained reports from requested actions, preserves
actual stock data and introduces no extra emergency.


### Shared normal-duty integration on 0.3.6

- **#5 `45830f4`:** explicit operator selection is bound to name, trait, vessel
  and save. Deploy/stow, SAS toggle/modes and the panel's experiment Run recheck
  context/hardware, then invoke the public shared crew-duty query as the final
  guard. They do not cache eligibility or substitute another crew member. The
  bridge rejects unavailable/incompatible queries; older expanded branch code
  can compile but cannot authorize these commands without the current core.
- **#5 `89b1c1a`:** exact 0.3.6 + eight onboard sources + nav9 compiles with
  33 source files. All 11 model/boundary runners pass, including crew duty and
  bridge tests. The prior shared-readiness source gap is now wired; live UI
  refusal/recovery and actual device behavior remain acceptance gates. Physical
  room/player authority and LMP synchronization are still open. Native stock
  actions outside the custom panels remain independent.
- **#8 `294c667`:** candidate builder now uses ONE pinned cumulative 0.3.6
  packet. Fourteen safety tests, prepare-only and full build pass. Earlier
  0.3.5-plus-epoch recipes are historical, not current build instructions.

A Check by Name is a proposed ordinary engineering handover while the selected
operator obtains actual rest/care; it implies no automatic recovery or emergency.
Human-watch science execution still needs a role-policy decision while Ada is
frozen: do not thaw the relief pair or change traits merely to bypass the gate.
Read-only scientific review remains available to the human pilot/engineer pair.


### Observed power survey and cumulative 0.3.7

- **#5 `3c1fe22`:** Supplies can collect a bounded continuous 60-second stored-EC
  survey from actual ~2-second UT samples at real 1x. Identity/save/Game/body,
  unique part IDs and capacity are bound. Gaps over 5s, invalid resources,
  regression, pause/warp or changed context end collection with partial data.
  It shows recorded window, count, observed net/rate and sampled extrema; no
  idle timer, forecast, repair or cryopod clearance is inferred. Retained results
  are labelled by original vessel and time. Pure tests pass.
- **#5 `b88befb`:** exact 0.3.7 + ten onboard sources + nav9 compiles with 36
  sources. Thirteen model/boundary suites passed on verified unchanged inputs
  and the new power model; production crew-duty + production bridge variant
  passes too. The final UI integration compiles. Live telemetry/dispatch/device
  evidence remains pending; stock stores and control state were never altered
  by the observer.
- **#8 `b1e4fde`:** reproducible builder now pins ONE cumulative 0.3.7 packet,
  which includes observed-time medical recovery, symmetric cryopod handover and
  earlier host changes. Fourteen safety tests, prepare-only and compile pass.
  Do not stack older coast/duty-test or other supplemental packets.

One Interval (`d83734c`) is a proposed routine power-review dialogue. A flat full
or empty store is recorded without declaring balance or future endurance. The
scene grants no mission achievement, physical repair or extra emergency.


### Full combined reconstruction and morning worksheet

- **#8 `c170bfc`:** `--operations-ref` now reconstructs the full plugin from
  explicit Git objects: canonical host core17, allowlisted operations10 and
  navigation9. It also supplies the corresponding focused model tests/runners.
  Empty/missing operations refs fail before output; collisions never overwrite
  core files. Navigation-only mode remains available without that argument.
  Sixteen builder tests and 22 total Python checks pass. The combined 36-source
  build and all 13 focused suite groups pass.
- **#8 `7145244`:** independent audit verifies every host source unchanged,
  every addon/test blob against its pin, complete manifest/tree digest, expected
  MegaCrew assembly identity and no packaged KSP dependency DLLs. The candidate
  remains a local review artifact; no installation or live qualification occurred.
- `docs/MORNING-QA-WORKSHEET.md` orders the permitted runtime gates: actual
  time/desktop check, fresh separate cryopod refusal/automated profiles, duty
  refusal/recovery, inputs/persistence, exact-GUID full Finch, stock-node
  comparison and telemetry. It identifies unavailable prerequisites and exact
  marker/report schemas. Quarantine old QA plugins only in fresh copies; use
  one intended probe at a time. Never reuse consumed profiles or interpret a
  model/compiler pass as gameplay evidence.

Verify the Build is a proposed ordinary handover of source/probe identities.
Its test results remain blank until observed; it adds no campaign achievement,
animated interaction or emergency.

### Docking instruments and first morning runtime attempt

- **#5 `2a2a91f`:** Operations now offers read-only own/target port selection,
  separation, signed axial offset, lateral offset, opposing-face angle and
  measured closing rate. Live identity/context and part checks prevent stale
  selections; pause/warp invalidate readings. The feature never sets a stock
  target or controls attitude, engines or docking. Full branch compilation and
  all eight operations test suites pass, including the production controller
  against recording KSP/Unity fakes. Actual stock-port axis/sign, UI and physical
  docking acceptance remain open.
- **#8 `a6de258`:** reproducible builder accepts the complete seven-file docking
  group from an explicit operations ref, preserves old refs without the group,
  and rejects partial groups before output creation. Twenty-two Python checks
  pass. The exact 0.3.7 + nav294c667 + ops2a2a91f candidate compiles 38 sources;
  all 15 model/boundary suite groups pass. Prior 35 source blobs are unchanged;
  the intended rendezvous hook changes and two docking sources are added. This
  candidate remains outside all game installs.
- **Runtime evidence:** the separate fresh manual cryo profile retained the
  earlier verified 36-source candidate and original provider, with no probe or
  automation marker. KSP reached MAINMENU at 10:21 local on 31 October. Computer
  control returned only an icon-sized capture and no usable game interface.
  The process was closed and logs/provenance retained. No flight, refusal,
  freeze, thaw or gameplay pass was observed. The separate automated patched
  profile is prepared and unlaunched; manual refusal remains first.

Compare the Port Readings is a proposed ordinary instrument cross-check. It
asks the crew to read port labels and sample time aloud before comparing the
figures. It adds no docking clearance, completed task, animated interaction or
campaign emergency. Live validation is the next priority once UI observation
is restored; LMP authority and synchronization remain later gates.

### IVA animation evidence tooling and input recovery

- **#5 `76497fe`:** local 1.12.5 API inspection identifies existing seat/model
  lookup via `InternalSeat.kerbalRef` and `Kerbal.Animators`. The EVA animation
  manager is not a seated-IVA gesture API. Asset names alone do not establish
  playable states. A separate one-shot catalog probe records actual live
  controllers, clips, layers/state hashes and candidate `HasState` results.
  Exact disposable root, scope/enable, save, 1x IVA and crew/seat/render identity
  are checked; unknown cryo state makes results inconclusive. It never plays
  animations or changes care, seats or camera. Seven builder guard tests and
  31 recording-harness assertions pass; the actual dedicated builder compiled
  the probe against KSP assemblies in an ephemeral fixture. Nothing is staged
  or runtime-qualified. The normal gameplay plugin is unchanged by this commit.
- A read-only public Social activity snapshot was requested from Hamish's core
  to provide actor/partner/room/vessel/elapsed context to a future presentation
  adapter. This API is still requested, not implemented here. Care progression
  and rewards retain one owner. The full proposed Port Readings scene includes
  active workload, choices, recoverable mistakes and evidence/persistence gaps;
  it adds no emergency.
- **#8 `4e36f51`:** selecting KSP through the native Window menu restored
  capture. Keyboard navigation reached the sole disposable QA save selector;
  game-button/save-dialog activation remains unverified. No flight/cryo action
  occurred, the game was closed, and the pristine save hash is unchanged.
  Manual refusal and all later runtime gates remain open. The separate
  automated cryo profile remains unlaunched.

Next animation evidence is a catalog from an actual IVA model, followed only
then by a separate opt-in gesture/restoration test. A successful catalog alone
will not qualify animation, animated handover, autonomous NPCs or multiplayer.

## Source progress during the active-play hold — 31 October 2026

The main console now starts closed, uses a bounded window, a four-column/two-row
tab grid, independent scrolling content and a wrapped status region. Global GUI
skin/enabled state is restored after drawing. The Crew tab makes EVA context
explicit and disables its crew/cryo mutations there. Source review and a complete
38-source KSP build pass; the reported click/cropping problem is not yet verified
as resolved in game. No active installation was updated.

The full cryo probe now follows the public save-upgrade/scenario/start path for
initial and frozen reloads, snapshots live state explicitly, and checks genuine
1x within separate scene phases. A disabled or unknown electricity policy stops
as inconclusive before a freeze request. The builder refuses reused roots and
QA DLLs anywhere in live GameData, including folders named backups. Its report
reader distinguishes incomplete preconditions from provider failures. These are
source/compile/guard results; full freeze/thaw and background power remain open.

Hamish's Duna planner v0.1 passed independent review and 608 model assertions.
Its six additive files are integrated with a bounded-panel refinement; the full
40-source candidate compiles. A full Git-snapshot staging build preserves the
new UI and mission data, strips old DLLs and records source/compiler provenance.
The separate vehicle v0.3 review found a Station/Ship fixture mismatch, reported
to its owner; that package is not silently overlaid here. No Duna mission is
declared playable.

A routine Duna story proposal, [The Red Window](stories/THE-RED-WINDOW.md),
adds a cooperative observation/caption disagreement and recoverable handover
question. It is not canon or a playable trigger, grants no science and adds
no emergency. Persistence and the in-ship notebook remain implementation work.
