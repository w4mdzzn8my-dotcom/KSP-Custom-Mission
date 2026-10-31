# MegaCrew 0.3.5 + epoch + onboard science/operations integration audit

## Upstream review

Issue #1's latest relevant comments say the cumulative `Hermes-Campaign-v0.3.5-source.patch` replaces the 0.3.4 cumulative source and the separate console/supply/QA supplements. Apply it once to the pristine 0.3.0 ZIP; do not stack it on 0.3.4 or the superseded supplements. The `Hermes-Epoch-Planner.patch` is additive after 0.3.5. It selects a campaign start epoch compatible with fixed burn duty intervals; it does not move burn UT, pause watches, edit UT or activate burns. The latest comments keep runtime testing and high-rate coast unqualified.

`Hermes-Combined-035-Audit.md` reports a 31-source compile and nine model/adapter test runners passing, but explicitly says this is not gameplay validation. Its published source-list digest is `998d4896cadfa5792826ac811504951be5faf01442b4813e28124700df790ec0`.

## Reconstructed candidate

Pinned inputs and overlays:

- Pristine published `Hermes-Practice-v0.3.0.zip` from `95749349533e87da7597f65506dd6ce3ed8e1915`; SHA-256 `0071eacf3a1b38a63c611fe2a65877ea2f62f6076b7e97d22e9cdf58b412cf40`.
- Cumulative 0.3.5 patch from `4e86cc0501b7389fb9d00dc27a67ad80da40c52a`; SHA-256 `280c77e0128b2201d875343047f8e4ccdff3e723640d9cf87af3bbc9d4d03851`.
- Additive epoch patch from `591ca74f16337c2f91a4d289dc10ec4726d1190f`; SHA-256 `7fc77d8a78da8b81c7f48d35445a72e73ddd8e1eec89e72859869ab06b067ab4`.
- Seven onboard science/operations files from `2e7d8063bef3b8ff96745df6f4251bf557bf25b7`.
- Nine navigation `.cs` files from `418229a76f82fbd774baf16b09586fabcc826334`.
- Final read-only policy update to `Source/OnboardOperations.cs` and `Source/OnboardScienceObservation.cs` from `4e1114bc9c1169eac851b726ac7d8fcf4831423d`; no model or test source changed.

Reproducible procedure: obtain each artifact via `git show <pinned-ref>:<artifact-path>`, verify its SHA-256 above, create a new output directory, safely extract the pristine ZIP, dry-run then apply the 0.3.5 patch once, and apply the epoch patch once afterward. Overlay only the seven named onboard source files from `2e7d806` and the nine tracked `Source/Navigation/*.cs` files from `418229a`; do not copy the old branch-base `Source/MegaCrew.cs`, `Source/Practice.cs`, or other core files over the cumulative patch. Overlay the final two UI/controller files from `4e1114b` after that, then compile all `Source/**/*.cs` in that fresh tree with C# 7.3 and the KSP 1.12.5 `Data/Managed` reference assemblies. This audit was reconstructed from pinned Git objects and the published archive, not from the checkout's working tree.

## Compile and tests

The full candidate compiled with 31 C# sources using Roslyn SDK 10.0.103 (`csc.dll` SHA-256 `e6d16f66fdc014d5386eaa31238cd6f8069295f37f5e6e7b09f6e9462bd6db38`) against 89 KSP Managed DLLs. Before the final policy overlay, the reconstructed source-list SHA-256 was exactly `998d4896cadfa5792826ac811504951be5faf01442b4813e28124700df790ec0`, matching the upstream combined audit. After overlaying the two changed files from `4e1114b`, the final source-list SHA-256 is `6918ec642d2a92842872a66c525c2db997ee9183149c116b802df4cd6b3edd4c`. Final plugin SHA-256 is `3b2238a77a5db08f7d590ced42f0a3db831db84f202587ad8065608f42279b8a`.

All nine runners passed on the reconstructed candidate before the final policy-only source overlay. That final overlay changed only the two UI/controller source files below; test/model blobs remained unchanged. The exact final candidate was recompiled successfully, so the suite results still apply to the unchanged model/test code, but were not rerun after the UI-only change:

- Campaign, including supply adapters and scenario persistence
- Needs
- Console input
- Cryo readiness boundary tests
- Epoch planner (20,100 independent coverage comparisons)
- Operations/resource trends
- Rendezvous
- Science observations
- Navigation (2,000 bounded propagation cases, 37 estimator assertions, 27 watch cases)

The campaign runner emitted one existing `CS0414` warning for the unused test-stub field `MissionConsole.message`; it passed. Cryo readiness results are recording-boundary/model evidence, not actual freeze/thaw. No game was launched, no DLL installed, and no save/provider/server was touched.

## Compatibility finding

Cumulative 0.3.5's crew-needs module defines custom-work readiness (`NeedsModel.Fit`/`CrewNeeds.CareReady`) and describes hunger/thirst/fatigue and hygiene/conditioning/morale thresholds. The final `4e1114b` policy overlay makes retained-data baseline/compare captures trait-neutral so a human pilot/engineer watch can review retained data while the scientist is frozen; capture still requires an unpaused, 1x, loaded/unpacked vessel and the same save/vessel/body context. Stock experiment `Run` remains gated by the Scientist roster trait. The mutating onboard engineering actions and other roster-based custom commands still do not consult `CrewRecord` or `NeedsModel`; shared duty-readiness for those mutations remains an integration/release gate. The source compiles together, but this does not establish shared readiness enforcement or runtime acceptance.

The epoch planner remains a pre-start schedule model only. Recompute watch coverage if a physical plan changes, preserve separate burn approval, and do not interpret an epoch recommendation as route validity or burn authority.

## Source/API notes

The 0.3.5 patch alters the campaign/needs/cryopod core from the pristine 0.3.0 files. The reconstruction left those files owned by the cumulative patch; it did not overlay the older Hami branch copies. The seven onboard files and nine navigation files were additive overlays from their explicit commits; the two final policy files came from `4e1114b`. Their SHA-256 values are `457d29d7606d0564241a6f7c1c48e30a6f56a1e74ec4084597419c4be693de3f` (`OnboardOperations.cs`) and `7b489828b7789508da73d29167fc9dd75040158d8ae9b3e037c36314df1f263c` (`OnboardScienceObservation.cs`). The single test warning is non-failing. The candidate was built and tested outside the checkout; `git status` stayed clean.

