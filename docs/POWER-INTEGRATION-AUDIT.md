# MegaCrew observed-power integration audit

## Exact candidate inputs

- Upstream source candidate: `f76e4371d41ff6c246f3cea5b88d2a516f36bc38`. Its `docs/SOURCE-CANDIDATE.md` directs extraction of pristine 0.3.0 and application of the cumulative 0.3.7 patch exactly once. Patch SHA-256: `5f59faa0a8220abf9a5119b76fb98caac8e4e73c189cc7e4d2f05948b80b8770`. No 0.3.6 or other older cumulative/supplemental patch was stacked.
- Published pristine `Hermes-Practice-v0.3.0.zip` SHA-256: `0071eacf3a1b38a63c611fe2a65877ea2f62f6076b7e97d22e9cdf58b412cf40`.
- Addon base: `45830f41caa7949056eb9ec65d43761e6beba0ad`; final power additions and operations-panel integration: `3c1fe22d001d52630a0a75430274f86fb75e45af`.
- Navigation source and focused tests: `294c667470e5304ac32d5f905950b357a8b9d45e`.
- Reconstruct in a new output directory: run the isolated builder from PR #8 commit `b1e4fde`, then overlay only the ten additive onboard source files from `3c1fe22` and their five model test/runner pairs. Compile all Source/**/*.cs recursively with the local KSP Managed references. Do not overwrite the reconstructed host core with the older branch base.

## Build

The final combined 36-source plugin compiled as C# 7.3 against 89 KSP 1.12.5 `Data/Managed` assemblies. Roslyn `csc.dll` SHA-256: `e6d16f66fdc014d5386eaa31238cd6f8069295f37f5e6e7b09f6e9462bd6db38`. Combined DLL SHA-256: `7b75656a9f25b40afe8f2165b8ae31e6d48b2c44649153539269d9d5b3ad9e83`. Source-list SHA-256: `3177540f8f5817f20ec25204e9a9437af1893f7080d741e540743572e96ef0b9`. Candidate tree SHA-256 excluding the root manifest: `69f9e1b5ee3cd7f4b1ba32f91a81a594d489809416d2eb6a2db6ea2ed4913657`.

The addon-only changes since the already-tested 0.3.7 + onboard + navigation candidate are the intentional `OnboardOperations.cs` integration and the new `OnboardPowerSurvey.cs`, `PowerSurveyModel.cs`, and power model test/runner. Hash comparison found all 67 previously tested source/test blobs identical when excluding that changed operations panel; the combined compile covers the final panel.

## Test results

All 13 distinct suites passed:

1. Campaign timing/supply/persistence/coast models.
2. Crew needs.
3. Console input and FreeIVA lock recording-boundary checks.
4. Cryo readiness bridge.
5. Epoch planner.
6. Crew duty query.
7. Observed-time crew recovery.
8. Resource trend/operations model.
9. Rendezvous model.
10. Science observation model.
11. Duty readiness bridge model.
12. Power survey model: observed drain/recharge/steady/varying readings, coverage/gap and duplicate-time boundaries, invalid inputs, cancellation, and immutable snapshots.
13. Navigation: 2,000 randomized Lambert cases (0 failures), 37 burn-duration assertions, and 27 watch-coverage cases.

The additional integrated crew-duty runner invocation passed with `--bridge Source/DutyReadinessBridge.cs`, exercising the production duty query and production bridge together against doubled KSP boundaries.

The isolated builder committed at `b1e4fde` defaults to the single 0.3.7 patch and navigation ref `294c667`. Its 14 safety tests, `py_compile`, prepare-only build, and full compile passed. Profile hashes: `tools/build_navigation_candidate.py` `0ca652e9bb86bb26e157ff73040f7c3c44ddce55832c37bd255d8036b3d6d0f1`; `tests/test_navigation_candidate.py` `f60340be61a291ea9c610b2fbdd48953712d679ca9e1b55907448406b4e385ea`; `docs/BUILD-NAVIGATION-CANDIDATE.md` `2c91d41a83b05ed528c6598c63315ffe8667e5c4b099fdf46aa4f5e51f5fb9b1`.

## Power-survey behavior and limits

The survey records vessel ElectricCharge amount and capacity over supplied universal-time samples, requires continuous coverage with gaps no longer than five seconds, and terminates on invalid readings, clock regression, vessel/save/body changes, part-structure changes, pause, or warp. It reports the measured interval and does not forecast future power or claim endurance/cryosleep readiness. It is trait-neutral and does not call the crew-duty readiness query. This compile and model evidence do not establish behavior in a live KSP flight; KSP was not launched, and no installed package, personal save or server data was modified.
