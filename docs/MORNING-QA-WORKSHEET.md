# Morning KSP QA worksheet — 0.3.7 candidate + reviewed overlays

## Before any launch

**Do not start KSP before 09:00 Sydney time on 31 October 2026.** After 09:00, proceed only if the user desktop is actually unlocked and available and no other KSP instance is running. The clock reaching 09:00 does not establish desktop availability. Keep all runtime work in a fresh disposable KSP copy and save. Do not touch original saves, the Luna server/Universe, or the normal/play installation. Keep time warp at 1× throughout; no high-warp run is qualified.

Candidate identity to match before testing:

- Pinned 0.3.7 base: upstream commit `f76e4371d41ff6c246f3cea5b88d2a516f36bc38`, patch SHA-256 `5f59faa0a8220abf9a5119b76fb98caac8e4e73c189cc7e4d2f05948b80b8770`, pristine 0.3.0 archive SHA-256 `0071eacf3a1b38a63c611fe2a65877ea2f62f6076b7e97d22e9cdf58b412cf40`.
- Current integrated builder candidate: inspect `/tmp/megacrew-builder-combined-037/manifest.json`. Its output tree SHA-256 is `3557363223bff50bc4ae655f5ab172576c18d6cd26fdae61d8b2ecd715110be1`; `GameData/MegaCrew/Plugins/MegaCrew.dll` SHA-256 is `2d6fd46d353b1cac3b971deff623a2f7f24d2d52cb44cc605e01e8548c5ca986`. Use the manifest and these hashes as the candidate identity.
- An existing test-copy plugin was observed with a different SHA-256 (`a73b54de690a316a8ddf3386c077e52283036a0b3143da4b837b95d3c0422b8f`). Do not treat an older installed copy as the matching candidate. Source compile and model tests are not live KSP evidence.

**Prerequisites:** use fresh disposable copies; existing test copies have older assemblies, prior QA saves, or consumed markers and are not a valid target. Cryo, Finch, and node gates remain pending until exact candidate/probe/save inputs are assembled.

Use only the scripts already documented in the pinned packet: `tools/prepare_cryo_qa.py`, `tools/build_cryo_qa.py`, `tools/inspect_cryo_qa.py`, and `tools/fix_background_cryo.py` from the reconstructed host packet. The navigation probe compiler/source must be read separately from pinned PR #8 `c170bfc`: `tools/build_navigation_probe.py` and `tests/ksp/CircularNodeProbe.cs`. The combined builder copies model tests, not runtime probe addons; no probe is automatically installed. The cryo prepare helper requires a **new, nonexisting** destination whose final directory name is exactly `KSP-Hermes-QA`; it copies `KSP.app` and `GameData`, not personal saves or server data, and restores only the pristine 0.3.0 test save. It creates `HermesCryoQA.scope`, `.variant`, and `.enable`. The helper copies all of `GameData`. In the new copy only, identify and quarantine any old QA probe plugins, then stage exactly the selected matching candidate and one intended QA probe. Never delete or alter QA plugins in the normal install. If the QA plugins cannot be identified unambiguously, stop with this prerequisite missing; do not launch.

## Ordered gates

### 1. Scoped cryopod refusal, then raw single-pod report

1. Make **two separate fresh QA profiles** in distinct parent directories, each with final directory name `KSP-Hermes-QA`; never reuse the manual profile for automation. The manual profile has no `HermesCryoQA.enable` marker. First verify the cryopod API is ready, then with Ada outside a pod use **F8 → Crew & cryosleep → Freeze Ada Kerman**. Record the missing-pod refusal only if Ada remains awake and the UI gives the expected transfer instruction. Next, if an actual crew-transfer-in-progress state can be observed naturally, invoke the action during that transfer and record the busy refusal. Do not fabricate flags or state; if the busy condition cannot be observed, mark that case inconclusive. These manual checks are not in the probe result schema.
2. Keep the second fresh profile pristine for the one-shot automated run. The probe acts only when `HermesCryoQA.enable` exists, consumes it as `.running`, and ends at `.done`; use no preexisting marker/result. Do not restore or reuse a marker in the manual profile. Confirm exactly one selected probe plugin is present in the new copy before launch.
3. Confirm the automated profile has save `MegaCrew Background QA`, one initial ProtoVessel, 17 parts/four awake crew, stowed solar arrays, and more than 3300 EC. The probe uses stock crew transfer, waits for readiness, freezes, observes an unloaded vessel at 1× for 45 real seconds, saves/reloads, recharges via real solar generation, and thaws; it does not inject EC or set UT.
4. Review `HermesCryoQA-result.cfg` and focused `[HermesCryoQA]` log lines. Run the existing checker from the pinned packet: `python3 tools/inspect_cryo_qa.py /path/to/KSP-Hermes-QA`.

**Pass:** API-ready missing-pod refusal is recorded in the manual profile; the transfer-in-progress refusal is separately recorded if observed (otherwise that subcheck remains inconclusive); automated checker exit 0; raw report has `run_state=completed`, `stage=procedure_finished`, `probe_completed=true`, `flight_qualification=false`, exact provider hash for its `variant`, actual frozen-before/after-unload/after-reload and thaw-complete fields true, and internally consistent positive UT/pod intervals and EC delta. A patched-provider run additionally needs net EC draw. This is only single-pod 1× procedural evidence.

**Fail:** checker exit 1, a `failure`/failed report, hash mismatch, incorrect actual-state/count/EC evidence, or a refusal action that actually freezes Ada before transfer. **Inconclusive:** checker exit 2, missing/partial report, timeout, desktop/runtime unavailable, or refusal outcome not observed. Do not rearm a consumed copy or interpret `probe_completed=true` as flight qualification. No multiple-pod, high-warp, thermal, settings-off, or depletion/recharge-failure policy pass is claimed.

### 2. Shared duty refusal and recovery

On the same disposable candidate, exercise Pilot, Engineer, and Scientist controls with an explicitly selected crew name. Check that the UI uses the shared `MissionConsole.CanPerformDuty(name, vesselId, out reason)` decision and refuses a normal command when that selected crew member is unfit; test that the same named member can become eligible only after actual care/rest/recovery and a fresh query. Keep a separate note for each refusal/recovery. Do not edit meters or save files to manufacture readiness. If no naturally reproducible unfit state is available, mark this gate inconclusive and leave it pending.

The API is local readiness only: it does not prove a physical room, authenticated player, multiplayer authority, or burn approval. Stock emergency controls remain outside this gate. Source runners in the packet are boundary/model evidence, not runtime PASS.

### 3. Inputs and save persistence

Use the disposable copy and follow the runtime checks in `docs/CONSOLE-INPUT.md`: hover both panels and their toolbar buttons; verify ship controls do not leak through; leave/close panels and verify controls return; type/edit fields; check outside-click focus, F8/Close, scene change, app focus loss, and no lingering MegaCrew input lock. Repeat with the navigation panel open. Exercise save/reload for campaign state in the same disposable save and inspect only that copy’s `MegaCrewMission.cfg` and `persistent.sfs`. Do not claim source persistence tests prove live persistence. Power, science-observation, and rendezvous snapshots are transient; verify they reset on their documented context changes rather than expecting them in a save.

### 4. Finch full 35-part orbital cradle

Use the latest probe source pinned at commit `3b0ffe6`, `tests/ksp/FinchOrbitProbe.cs` (SHA-256 `13eab72b59f4a0dc7c208ef38b909b3fe9febcd529f51de156b69044e0bab51f`); the temporary compiled probe SHA-256 is `bf5d62cb70c90760d8bf8a8c80823f820b0401059c5d64727f2a89acfb7863c6`. The authoritative cradle is pinned at the same commit, `Ships/VAB/Hermes Finch Docking Cradle.craft` (Git blob `83dc899d9a073f5bc24c0b2a420891bb349746d5`, content SHA-256 `2eddd59356cc092a060ae3ff2fa6b2ba07ed16928486454716174e36b1a38cc6`). Stage this exact craft; do not assume the copy’s craft has the same ordering/content. Use a fresh save/profile and stage the matching candidate and probe. Existing copies/consumed markers are not reusable evidence.

For the eventual matching copy, the code uses save folder `MegaCrew Finch Spawn4 QA`, craft path `Ships/VAB/Hermes Finch Docking Cradle.craft`, and root marker `FinchSpawn4QA.enable` (existence-only; it moves to `.consumed`). It captures the practice GUID from `flightState.protoVessels[0]`, requires the same save/body/GUID and loaded/unpacked practice vessel before spawning, then observes only the spawned GUID. Do not rely on vessel name.

**Pass:** focused `[FinchOrbitQA]` lines show the same spawned GUID and QA save/body at every stage, `LOAD_GATE` and the pre-dock/after-20 observations each show `loaded=True`, `packed=False`, `liveParts=35`, `snapshotParts=35`, with docking event/state lines for that same GUID and no `INCONCLUSIVE`. **Fail:** a docking/count result attributed to the wrong GUID or a probe exception; this is a failed test procedure, not proof of a defective craft. **Inconclusive:** a guard correctly refusing changed target/save/body, timeout, unavailable/unloaded target, part-count mismatch, missing logs, or mismatched assembly/craft/save. This probe reads docking state/events; it does not actuate docking/undocking or qualify physical clearances.

### 5. Detached stock maneuver-node check

Use `docs/NAVIGATION-NODE-QA.md` and runtime probe source/compiler from pinned PR #8 `c170bfc`. Build that probe against the exact combined candidate assembly in a separate source/output directory; temporary files from earlier runs are not required. The one-shot marker is `CircularNodeQA.enable` in the KSP application root with exactly:

```text
AUTHORIZE ONE IN-MEMORY NODE QA RUN FOR SAVE: MegaCrew Navigation QA
```

It moves to `.consumed`. The required exact save folder `MegaCrew Navigation QA` with `persistent.sfs` and an unpacked circular Kerbin-orbit vessel at index 0 is currently absent from the test copy; the matching navigation assembly and probe are not staged together in a fresh QA install. Prepare/verify those before arming; do not create the marker against a missing save.

The probe requires loaded/unpacked orbiting Kerbin, 1×, no engines producing/spooling, zero existing nodes, eccentricity `<=1e-5`, and current PeA above 80 km. It computes the detached proposal at UT+120s, creates one temporary node, compares detached and stock `nextPatch`, then removes only its own node and verifies the list is empty. It never burns, warps, or calls save APIs.

**Pass:** `[CircularNodeQA] SUMMARY PASS`, `completed=True`, `cleanupVerified=True`, and each independent/detached/node/stock-patch comparison line passes. **Fail:** any `FAIL` comparison or cleanup failure. **Inconclusive:** precondition rejection, timeout, missing summary, scene change, or cleanup not verified. Never delete another node or save during the probe.

### 6. PR5 telemetry and onboard UI

Only after the exact integrated candidate is staged and the earlier context/input gates pass:

- **Power survey:** start/cancel and one full 60-second survey in loaded/unpacked 1× flight. Note captured vessel GUID/save/body, UT window, coverage, sample count, amount/capacity and observed net stored-charge change. Pause, warp, topology/capacity change, clock regression or context change must abort. Zero net change is not proof that generation equals demand; no endurance forecast/readiness certification is implied.
- **Science:** inspect retained reports; capture a baseline; run one available stock experiment only in the disposable save; compare actual stored-data change. Confirm read-only inventory/baseline/compare do not discard, transmit, reset, award points, or claim a unique report ID. Stock Run still needs selected Scientist readiness.
- **Rendezvous:** compare two same-body vessels by GUID, including an unloaded target if available. Check sampled UT, range, closing rate, relative speed and bounded 60-second linear estimate; verify no stock-target lock, SAS, engine, node, or warp changes. It is not intercept guidance or docking clearance.

Record screenshots/brief readings and context. If the second vessel, experiment data, or matching candidate assembly is absent, mark the corresponding check inconclusive rather than inventing a result. Read-only telemetry is trait-neutral and does not establish player identity, room occupancy, LMP authority, flight safety, or mission completion.

## Evidence record

For each gate write: date/time, QA root/save, candidate plugin SHA, probe SHA where applicable, marker transition, result status, and a short focused log excerpt or UI note. Keep raw logs local and redact them before sharing. Keep a failed or incomplete one-shot report for diagnosis; do not rerun the same copy by deleting markers. Source tests/builds are recorded separately from runtime evidence.

### 31 October morning attempt: INCONCLUSIVE

After 09:00 Sydney, the desktop was unlocked and no other KSP process was
running. A fresh manual profile used the exact stable 36-source candidate
`2d6fd46d…`, original BackgroundResources provider, pristine QA fixture, and no
probe or automation marker. Its log reached `MAINMENU` at 10:21:17 local and
reported launcher GUI ready at 10:21:19. Computer control exposed an icon-sized
image and no usable game UI after focus/raise/window recovery attempts. No
flight was loaded and no cryo refusal was observed. The QA process was closed;
local logs and provenance were preserved. The cause of the capture/presentation
problem remains undetermined. Menu startup is not a passed cryo gate.

The separate automated patched-provider profile is prepared but has not been
launched. Keep manual refusal checks first. The new docking instruments in
operations ref `2a2a91f` are excluded from this stable cryo candidate; they need
a separately identified candidate and stock-port runtime checks after the
earlier gates. No personal save, normal-play installation or server data was
modified.

### Follow-up: capture restored, save-dialog input still inconclusive

A fresh disposable clone with a unique macOS app identifier produced the same
icon-sized capture, so identity alone did not resolve it. Selecting the
observed **KSP** item in the native **Window** menu (`makeKeyAndOrderFront`)
restored the real game window and screenshot capture. `getApp`, AX Raise and
window zoom had not accomplished that. Always obtain fresh menu indices before
selecting the window. KSP's F1 key also produced a valid game image in its own
Screenshots folder; this helped distinguish game rendering from window capture.

Native mouse input moved the pointer but did not activate tested game buttons.
Source-confirmed keyboard navigation (Up/Down then Return on the highlighted
item) successfully opened Start Game, Resume Saved and the sole disposable QA
save selector. The save dialog did not reach an observable Load activation;
do not claim that a highlighted row was a loaded flight. No flight, manual
refusal, freeze or thaw was observed. The process was closed using Cmd+Q and
the pristine save remained SHA-256
`6fd1a826062dafcdf02fa485c89ca818819ea5352bcfc5eca5f1bfde2c1bb4b9`.

The scope of the unresolved limitation is now game-button/save-dialog input,
not absent rendering. Preserve this evidence and retain the manual-first gate.
The animation catalog tooling on PR #5 `76497fe` has not been staged or run;
its compile and recording-boundary tests are separate evidence.
