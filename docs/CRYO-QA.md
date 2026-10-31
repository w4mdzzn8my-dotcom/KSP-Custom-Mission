# One-shot real-KSP cryopod QA — pending runtime execution

**Integration status:** the probe now uses KSP's public save-upgrade and
load-game pipeline for both initial launch and frozen reload. It waits for the
save-upgrade success callback, refuses all failure/LoadAnyway paths, updates
scenario modules, performs the native setup save, fires the post-load event,
and starts the loaded Game. It also uses explicit, bounded MAINMENU, FLIGHT,
and TRACKSTATION phases; it does not apply flight-only readiness checks during
scene transitions. The code compiles against the installed KSP, DeepFreeze,
BackgroundResources, and pinned MegaCrew APIs. A complete runtime result is
still pending. The separate direct refusal observation does not qualify this
full freeze/thaw procedure or the GUI/transfer-busy gates.

This probe belongs only in a fresh, marked `KSP-Hermes-QA` copy. Never put its
DLL in the play copy. The helper refuses existing/nested destinations and does
not copy personal saves, logs or server configuration. On macOS it uses APFS
copy-on-write cloning for KSP.app and GameData. Only the exact published 0.3.0
archive provides the pristine test save; original saves are never read/copied.

1. Stage the hash-pinned provider using `tools/fix_background_cryo.py`, retaining
   its original. No runtime pass is implied by this staged candidate.
2. Prepare a fresh directory named KSP-Hermes-QA:

```
python3 tools/prepare_cryo_qa.py --source-root /path/to/separate/MegaCrew --qa-root /path/to/KSP-Hermes-QA --pristine-archive Hermes-Practice-v0.3.0.zip --provider-candidate /path/to/staged/BackgroundResources.dll
python3 tools/build_cryo_qa.py --ksp-root /path/to/KSP-Hermes-QA --csc /path/to/sdk/Roslyn/bincore/csc.dll
```

Before launch, the disposable profile must have DeepFreeze's normal public
electricity-required policy enabled. The probe reads this through
`CryoBridge.TryPowerPolicy` before arranging crew in the pod and again before
the freeze request. Unknown policy state or electricity-required disabled is
reported as inconclusive and no cryogenic request is made. The fatal-consequence
setting is recorded as context only; the probe does not change either setting
or qualify a settings/fatality matrix.

3. With no other KSP running and when desktop testing is permitted, launch the
   QA copy's KSP.app. The marked test starts once at the main menu. The probe
   validates root/save/hash boundaries and uses KSP's public native load path
   plus DeepFreeze freeze/thaw calls. To arrange Ada inside the pod for this
   serial procedure, it programmatically updates part crew lists and emits the
   crew-transferred event; this is not GUI transfer evidence and does not test
   a transfer-in-progress Busy refusal. It never fabricates frozen dictionary
   entries, sets UT, or injects EC. The source save starts with the original
   trainer, actual battery/Glykerol supplies and stowed solar arrays.
4. It observes the genuinely unloaded vessel in the tracking station for 45 real
   seconds at unpaused 1x, checking actual UT progression and recording battery
   delta and pod ledger timestamps. It snapshots the live Game, verifies the
   single saved proto-vessel and active index, and returns through MAINMENU
   before performing the native frozen reload. It verifies the same vessel,
   scenario, frozen ledger and pod membership, deploys real solar arrays, and
   waits for real recharge before thawing Ada. Eclipse/recharge can extend the
   run to about 40 minutes. Timeout/failure is recorded, never treated as a
   pass. The addon closes only the explicitly marked disposable KSP app.
5. Read `HermesCryoQA-result.cfg` in the QA root and focused `[HermesCryoQA]` log
   lines. The one-shot marker moves enable -> running -> done. An incomplete
   marker/result needs inspection; do not blindly rerun or replace the copy.

The probe is compiled as GameData/HermesQA/HermesCryoQA.dll, separate from the
normal plugin. `probe_completed=true` means the single-pod procedure finished;
`flight_qualification=false` remains explicit. Corrected net EC draw, genuine
freeze/reload/thaw and all recorded amounts still require review. No multiple-pod,
settings-off, depletion/recharge failure-policy, high-warp or thermal pass is
claimed. Compare original/patched providers only in separately fresh QA copies.

Current host evidence: probe compiles against the actual KSP 1.12.5, DeepFreeze,
BackgroundResources and stable MegaCrew APIs; preparation/guard tests pass. The
direct backend refusal has separate runtime evidence. This full freeze/thaw,
unloaded-power and frozen-reload procedure has not yet completed a runtime run.
Play-copy provider and original saves are unchanged.

## Guard/report follow-up

The current source includes the guard/report behavior directly; no topic patch
stack is required. It refuses old result/running/done markers, records progress
stages, and binds the run to its disposable root, save and active Game. Flight
and tracking-station checks are phase-specific; scene transitions have bounded
waits, and steady observations require actual unpaused 1x. Both game loads use
the native upgrade callback path. The one-shot done marker means the attempt
ended, including failures or inconclusive timeouts.

Read the report without launching or modifying KSP:

```
python3 tools/inspect_cryo_qa.py /path/to/KSP-Hermes-QA
```

Exit 0 means internally consistent single-pod procedural evidence, never
flight qualification. Exit 2 means incomplete (including no report). Exit 1
means failed or invalid. The reader rejects duplicate keys, contradictory
markers, missing actual-state evidence, nonfinite intervals and inconsistent
battery deltas. It does not substitute for review of raw measurements/logs or
validate high-rate/thermal/failure-policy behavior. Its unit-test complete
fixtures are synthetic and are not live gameplay evidence. The separate direct
refusal run is not a full-procedure report. This full freeze/thaw procedure
still needs a live run; do not infer completion from the direct refusal result.

The report reader accepts the bare top-level values emitted by ConfigNode.Save
(as confirmed in an existing host-generated mission CFG), and an explicit
HERMES_CRYO_QA wrapper. Both forms share duplicate/nesting checks.
