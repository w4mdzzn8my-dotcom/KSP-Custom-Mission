# Cryo refusal runtime evidence

## Scope

This is evidence for one narrow backend result: the original `CryoBridge`
refused a request to freeze Ada while she was aboard the loaded vessel and
outside the DeepFreeze part. It does not test the GUI gate, transfer-busy
protection, a freeze/thaw cycle, power endurance, or save persistence. It is
not flight qualification. The result file explicitly records
`ui_gate_tested = False`, `transfer_busy_tested = False`, and
`save_qualification = False`; it has no `flight_qualification` field.

## Evidence identity

The run used a fresh `KSP-Hermes-QA` copy with the pinned pristine save, the
stable MegaCrew DLL, and the original BackgroundResources provider. The
pre-load hashes in the report match the pinned values documented in
[`CRYO-REFUSAL-QA.md`](CRYO-REFUSAL-QA.md). The actual loaded bridge assembly
also reported the expected MegaCrew hash.

| Artifact | SHA-256 |
| --- | --- |
| `CryoRefusalQA-result.cfg` raw report | `7e1b398be671ca837971d18bad918e2a17aa70a611b0e52d1d2dd0c8dca75c92` |
| `HermesCryoRefusalQA.dll` used for this run | `e0a200274613403d690516952d7452824d5a8562103f123b5f4c174827840c26` |
| `KSP.log` for this run | `136c2267cea53e49f101eee62ce55514014bac340ca5774211dcbabff22a1278` |
| Persistent save after native KSP initialization and observation | `057d4af0ff857b9d1c81323be320904495ebf7152985b5f7bc623be1807ed1d6` |

The disposable save hash differed from the pinned pristine value after setup
and observation. The report records `save_unchanged = False` and
`save_qualification = False`; this run makes no persistence claim.

## Observed run

KSP's normal scenario initialization added 13 scenario modules, including
`DeepFreeze`, then saved the disposable game state, fired the post-load event,
and started the game through `Game.Start()`. The actual flight became ready at
unpaused 1x. The vessel had 17 parts and four awake crew. `DeepFreeze` reported
connected and idle; there was one empty freezer part. Before the request,
`TryFrozen` returned a known unfrozen state for all four crew members, and Ada
was present exactly once aboard outside the freezer.

The probe made one backend request at UT `100007.340000002`. It returned
`False`. The probe observed 5.08 UT seconds before the request and 5.08 seconds
afterward. The immediate and final snapshots showed the same crew-to-part
assignments, 17-part vessel, four awake crew, empty freezer, unfrozen states,
and resource amounts. `DeepFreeze` remained connected and idle. The report
completed with `result = REFUSED_AS_EXPECTED`.

The log also contains repeated `NullReferenceException` messages in KSP's
`AppUIInputPanel.RefreshUI` / `TransferTypeSimple.UpdateTopData` /
`ManeuverTool.Update` path near the end of the observation, plus a duplicate
destroy warning during shutdown. The probe itself logged its completed result
and final snapshots; no causal connection between those UI/shutdown messages
and the refusal result is inferred.
