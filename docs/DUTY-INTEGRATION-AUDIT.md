# MegaCrew 0.3.6 duty-gated integration audit

## Reproducible inputs

- Published pristine source: `Hermes-Practice-v0.3.0.zip`, SHA-256 `0071eacf3a1b38a63c611fe2a65877ea2f62f6076b7e97d22e9cdf58b412cf40`.
- Upstream cumulative 0.3.6 patch: commit `f4269977b596a129426bec2dc14eb3f0761eea5b`, `Hermes-Campaign-v0.3.6-source.patch`, SHA-256 `8756f2dfb46e3e915f1185fd440e79cd84f6d6a5cbd291d7583863d1a0777f05`. Applied once to pristine 0.3.0. No 0.3.5, epoch supplement, 0.3.4, console, supply, or QA patch was stacked.
- Onboard additions and tests: `45830f41caa7949056eb9ec65d43761e6beba0ad` (8 addon source files, 8 addon test/runner files).
- Navigation source/tests: `418229a76f82fbd774baf16b09586fabcc826334` (9 source files, 5 focused test files).
- Reconstruct with the isolated builder at PR #8 commit `294c667`, then overlay only the eight additive source files from `45830f4`: OnboardEngineering.cs, OnboardOperations.cs, OperationsModel.cs, OnboardRendezvous.cs, RendezvousModel.cs, OnboardScienceObservation.cs, ScienceObservationModel.cs and DutyReadinessBridge.cs. Copy their model tests/runners from that ref, and the navigation tests from `418229a`. Compile every Source/**/*.cs file into the new output directory; never copy the old branch core over the cumulative packet.

The builder profile preserves the fixed patch sequence and synthetic dependent-patch test. Its 14 safety tests pass; `py_compile`, prepare-only, and full compile also pass. Profile files: `tools/build_navigation_candidate.py` SHA-256 `fd6e9cd14b2c00680a5bbfda199b156a4b7116ba34757fe3551059402fefe956`; `tests/test_navigation_candidate.py` SHA-256 `f60340be61a291ea9c610b2fbdd48953712d679ca9e1b55907448406b4e385ea`; `docs/BUILD-NAVIGATION-CANDIDATE.md` SHA-256 `195183e8b3fa782e7042707c9cf8740ebbd1aa4a6741de5d2ff76f327048f117`.

## Combined compile

The isolated candidate contains 33 C# source files. Compile passed against the local KSP 1.12.5 Managed assemblies with Roslyn `csc.dll` (SDK 10.0.103, SHA-256 `e6d16f66fdc014d5386eaa31238cd6f8069295f37f5e6e7b09f6e9462bd6db38`). Candidate plugin SHA-256: `b75eb20cf4249e6714b4106500ed36b85b75c1aa48c4b3e89bf395551db311dd`. Source-list SHA-256: `cf3635bb157a4cb31d8951360faf8a14047ed49740f925c69ab9ed0962ea593b`. Tree SHA-256 excluding root manifest: `7f89fa50a4b348a0a55fe465e8db747e27be51cf2da197aa56c439a621666aa2`.

## Tests

All pass against the exact reconstructed candidate:

- Campaign timing/supply/persistence/coast and burn-window models.
- Needs, console input, cryo readiness, epoch planner, crew duty query.
- Operations/resource trend, rendezvous, science observation, duty readiness bridge.
- Navigation: 2,000 randomized Lambert cases (0 failures), 37 burn-duration assertions, 27 watch-coverage cases.

The production plugin compile is not a game/runtime execution. Tests use deterministic model/KSP boundary doubles as described by their runners; no live KSP behavior or flight qualification is claimed.

## Contract integration review

`CrewDuty` exposes a fresh local `MissionConsole.CanPerformDuty(crewName, vesselId, out reason)` query. The custom onboard engineering and operations UI passes the selected crew name and vessel ID to `DutyReadinessBridge` at each mutation dispatch. It does not cache fitness, substitute another crew member, or reuse a previous allow result. Caller-owned role, vessel-context and hardware checks remain in place; physical room occupancy is not established. Retained-data observation and rendezvous review stay trait-neutral; the panel experiment Run action requires a selected Scientist and shared readiness. Bridge tests cover exact query binding, fresh forwarding, fail-closed API errors, and denial before dispatch.

This contract applies to the addon's custom onboard controls; it does not globally gate KSP's stock right-click/context-menu science actions. The local name is not an authenticated multiplayer identity, and this does not grant Luna authority, establish LMP synchronization, or approve a burn.

The reconstruction read pinned Git objects and published fixtures and wrote only a disposable candidate. No repository files, installed game, personal saves or server data were modified, and KSP was not launched.
