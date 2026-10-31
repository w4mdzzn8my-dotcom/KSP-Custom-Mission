# Consolidated source candidate 0.3.7

Extract the exact published Hermes-Practice-v0.3.0.zip into a NEW build directory,
then apply Hermes-Campaign-v0.3.7-source.patch once. Do not stack older cumulative
packets or console/supply/QA/epoch/state/coast-handover/duty-test supplements. This packet includes all
current host-owned source, tests and documentation, including the epoch planner,
strict/lossless campaign state guards, shared crew-duty query, symmetric cryosleep
handover checks and independent observed-time medical recovery. No DLLs or
personal saves are included. Build only an isolated current-source candidate.

Basil's navigation/operations/lander draft branches remain separate. Overlay
only explicitly reviewed current additive sources and rebuild recursively.
Never overwrite host-owned core files or install an older branch DLL. Publishing
this source artifact does not merge or qualify any draft.

Public operator eligibility: MissionConsole.CanPerformDuty(name, vesselId,
out reason). See CREW-DUTY.md for dispatch, role, room and authority boundaries.
Readonly retained-data views remain trait-neutral. Mutating standalone panels
in reviewed PR5 45830f4 call the shared query; those additive files are not
included here. Their live UI refusal/recovery remains unqualified.

Build with tools/build.py. C# runners: needs, campaign, console input, cryo
readiness, epoch planner, crew duty and crew recovery. The duty runner also
accepts --bridge PATH to test the actual reviewed production bridge with the
actual host query; KSP boundaries remain doubled. Python unittest discovery covers
preflight/provider patch/QA guards and report inspection. These passes are
models or recording boundaries; actual KSP flight remains unqualified.
High-rate coast remains disabled and live cryopod/persistence/thermal/
failure-policy/actual-burn/lander gates remain open.
