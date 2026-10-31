# Rendezvous integration build audit

## Inputs and reconstruction

- Upstream base: `w4mdzzn8my-dotcom/KSP-Custom-Mission` commit `95749349533e87da7597f65506dd6ce3ed8e1915`.
- Baseline inputs read from that commit: `Hermes-Practice-v0.3.0.zip` SHA-256 `0071eacf3a1b38a63c611fe2a65877ea2f62f6076b7e97d22e9cdf58b412cf40`; `Hermes-Campaign-v0.3.4-source.patch` SHA-256 `3d97e71e2aea4f1359ba5528bfd9ef2c0d9be08cfd74ac04d3c26f518b89711a`. Both match the pinned expected hashes. Patch was applied once to the extracted archive.
- Onboard source overlay: `2d6721e6fcae1b695d9288953fdfa4cb5e984e02` for `OnboardEngineering.cs` and `OperationsModel.cs`; rendezvous commit `62ec16ca73aec6bd7a4bc1d118d61dc7846ed81f` for `OnboardOperations.cs`, `OnboardRendezvous.cs`, and `RendezvousModel.cs`.
- Final rendezvous source/test blobs at `62ec16c` were confirmed byte-identical to `c08d4f6`, where the focused test runs were performed. The four test-source hashes are recorded below.
- Combined candidate additionally overlays the nine tracked `Source/Navigation/*.cs` files from `418229a76f82fbd774baf16b09586fabcc826334`.
- Reconstruction procedure: extract the pinned archive into a new disposable directory and apply the pinned cumulative patch once (`git apply --check` followed by `git apply`). Read each of the five addon source blobs with `git show <ref>:<path>` using the refs above, and write them only to their corresponding paths in the disposable tree. Read the four addon test blobs from the same refs. For the combined candidate, add the nine navigation blobs from `418229a`. Compile every `Source/**/*.cs` file using Roslyn with `-nologo -target:library -nostdlib+ -langversion:7.3`, references to every DLL in the local KSP Managed directory, and an output DLL path inside the disposable tree. This audit used a temporary helper to perform those steps; that helper is not a published build tool. No working-tree source files were used as inputs.

## Compilation

Both fresh exact-ref candidates compiled as C# 7.3 using Roslyn SDK 10.0.103 (`csc.dll` SHA-256 `e6d16f66fdc014d5386eaa31238cd6f8069295f37f5e6e7b09f6e9462bd6db38`) and the KSP 1.12.5 Managed assembly set (89 DLLs, individual hashes in each generated manifest).

| Candidate | C# source count | Plugin SHA-256 | Output tree SHA-256 (excluding root manifest) |
|---|---:|---|---|
| Patched 0.3.4 + onboard operations/rendezvous | 18 | `b8cc3ee8a241e8c7042b7f54b38395abc5c31a40b513f0dfbe84bdcd53dd5eb3` | `9759333e6d4d2a5e8a42c58ef2efd9b6cead0c70dda7fd8111edbd5b1427bb76` |
| Same + nine navigation sources from `418229a` | 27 | `b8a3be7c890f33ed439fd8d1933042989bd253c79212ca20e94d9022375753bc` | `20fb42d3fb8ec51b73c276b41b72af9365f24069726e279842906d3963f2761b` |

## Focused tests

The following all passed on both candidates at `c08d4f6`, each with `--runtime <Microsoft.NETCore.App/VERSION directory> --csc <Roslyn/bincore/csc.dll>`; the exact `62ec16c` source and test blobs used here were verified identical, so no duplicate test execution was needed:

- `python3 tests/run_campaign_tests.py`
- `python3 tests/run_needs_tests.py`
- `python3 tests/run_operations_tests.py`
- `python3 tests/run_rendezvous_model_tests.py`

One existing `CS0414` unused-field warning appeared in `CampaignSupplyAdapterTests`; it did not fail a suite.

Key overlay hashes: `OnboardEngineering.cs` `d7bd65246d11588ff54a6941806adffcd06f30f53055fd6a177f4c2bc3af24d7`; `OnboardOperations.cs` `045068990d738eb0534a88d9e348be511e74fae99967468e2837c57721770345`; `OnboardRendezvous.cs` `17f0277b12aaebb95f1e9738b69c6455cab14526b553f10817fddad33357a48a`; `OperationsModel.cs` `f961e1fd26845c57f3dd2e95bb52b9eebc03689260fcc871aa6af295e7a40e0b`; `RendezvousModel.cs` `f95d73ba45cc159165ec732991d1b76b4e2e037ca2118849786f67afd3ad2684`. Test blobs: Operations model tests `0d0069be15adfb50a32943e03499c457583640391a2ae72dedc9141bc14c3918`; operations runner `7e866883d39e598340d0c638bcb155860438ceee8ef146981dbc30a652c853f8`; rendezvous model tests `3b737faec1a93ffa1929f540a0adf3ff14741c43bc8da3ef13aad8d8e68bc724`; rendezvous runner `d139692eddd4ce85d45e00595d6b3f25dcc91b573e68ea07ec51d60ebf80c438`.

## Scope and review notes

- Resource trend sampling remains independent from rendezvous estimation. The rendezvous panel samples both orbit states at the same UT, uses vessel GUID/body checks, and invalidates its snapshot on context changes, pause/warp, invalid inputs, and UT regression.
- The displayed estimate is a read-only bounded linear closest-approach calculation. It is not intercept guidance, docking clearance, or a maneuver approval.
- This audit includes source compilation and pure/model test suites only. No KSP runtime or flight qualification was performed.
