# Docking instruments integration audit

## Pinned candidate

- Pristine package: `Hermes-Practice-v0.3.0.zip`, SHA-256 `0071eacf3a1b38a63c611fe2a65877ea2f62f6076b7e97d22e9cdf58b412cf40`.
- Cumulative upstream 0.3.7 packet: ref `f76e4371d41ff6c246f3cea5b88d2a516f36bc38`, patch SHA-256 `5f59faa0a8220abf9a5119b76fb98caac8e4e73c189cc7e4d2f05948b80b8770`, applied once to pristine 0.3.0.
- Navigation ref: `294c667470e5304ac32d5f905950b357a8b9d45e` (9 source files and 5 focused navigation test files).
- Operations ref: `2a2a91fe15efc2d871cd8c97a339aa26202c8708` (10 fixed operations sources, 10 operations tests/runner files, all 7 docking group files).
- Combined candidate: `/tmp/megacrew-docking-combined-037-2a2a91f`.

## Builder extension

The builder extension is published in this PR over navigation-core commit `6297e9946f5ada663d512852f47e4f8765e6453c`. `--operations-ref` remains explicit and optional. The optional docking group consists of exactly:

1. `Source/DockingGeometryModel.cs`
2. `Source/OnboardDockingInstruments.cs`
3. `tests/DockingGeometryModelTests.cs`
4. `tests/run_docking_geometry_model_tests.py`
5. `tests/DockingInstrumentControllerTests.cs`
6. `tests/DockingInstrumentControllerFakes.cs`
7. `tests/run_docking_instrument_controller_tests.py`

The builder includes the group only when every path is a regular tracked file in the explicit operations commit. No paths means an older operations ref is accepted; a partial group fails before output creation. Manifest records docking status/ref/commit and all seven SHA-256 values. The source allowlist remains fixed; no host-core scanning or overlay is performed. Overlay collisions are preflighted before any group member is written.

All 16 focused builder safety tests pass, including synthetic present/absent/partial group cases, missing input rejection before output creation, core-source preservation, and collision rejection without partial writes. `py_compile` passes. A real old operations ref (`3c1fe22`) with no docking files was also prepared and compiled successfully, with manifest status `absent`.

## Combined compile and tests

The exact 0.3.7 + operations + navigation candidate compiles as C# 7.3 with 38 plugin source files against local KSP 1.12.5 Managed assemblies. Plugin DLL SHA-256: `64d014f01c6a7682a6a74421f51e8d5ea5189060434f6a245b10b327466cbeb9`. Candidate tree SHA-256 excluding root manifest: `26c20a9edde4cfcb204dd391ba912b6cd85797581e6c4a7da8f91e67994fecda`.

All 15 included suite groups passed:

- Campaign, needs, console input, cryo readiness, epoch planner, crew duty plus the actual production bridge, and observed-time crew recovery.
- Operations/resource trend, rendezvous, science observation, duty bridge, and power survey.
- Navigation (2,000 randomized Lambert cases with 0 failures, 37 burn-duration assertions, 27 watch-coverage cases).
- Docking geometry model and docking controller.

The docking controller test covers first selection, target switching and reselection, target-specific fresh range, part-change invalidation, pause invalidation, and no docking-module mutations. It runs against KSP/Unity fakes, not a live game.

`Source/MegaCrew.cs` and `Source/Practice.cs` remain from the published 0.3.7 candidate (SHA-256s `7c87788b3d279664e55ceec45009dab7c1d54bc0707110b9bf5921123370db91` and `b652c64921d57c1b557331af67c6aea6567fbc58d1f09bc61f3b4fa3ab026445`). Hash comparison against the prior no-docking candidate found no differences in any non-addon source file.

## Read-only source review

The geometry model takes port positions and outward axes in one common world frame. Axial distance is signed target-minus-own displacement along the own port outward direction; lateral offset is the perpendicular component. Opposing-face angle compares own inward direction to target outward direction. Closing rate is the measured finite difference from two range samples, not a prediction. The UI resolves selected vessel and port identities again on each sample, validates save/game/body and part signatures, and clears on pause, warp, time regression, packing/unloading, target loss, or structural change. It does not set the stock target, control axes, nodes, staging, or docking state.

The transform convention is supported by the pinned KSP1 Docking Port Alignment Indicator source cited in PR #5 `docs/ONBOARD-DOCKING-INSTRUMENTS.md`; this is source evidence, not live-flight validation. No game was launched with this 38-source candidate. The separate morning attempt used the earlier stable 36-source candidate and remains inconclusive, as recorded in `docs/MORNING-QA-WORKSHEET.md`.

