# Campaign state validation and rejected-data preservation

Apply Hermes-Campaign-State-Guards.patch after cumulative 0.3.5. It is independent
of the additive epoch planner. No DLL, save or third-party code is included.

Previously malformed ration/routine booleans became false on load, silently
disabling finite needs. The loader now rejects malformed/missing required flags,
incompatible daily-routine/ration combinations, unsupported schemas and invalid
ledger/history values. A newly created empty scenario remains valid, and actual
older pre-ration saves without ledger fields still load. State is reset before
every load to prevent values leaking from an earlier load of the same module.

Rejected nodes are copied into memory using the actual ConfigNode.CreateCopy
API. OnSave emits that original node via ClearData/CopyTo, preserving invalid
flags, future schema values, unknown fields and repeated child nodes. The next
load must reject the same data again; it cannot become a silently reset campaign.
No auto-repair, epoch reset or resource replenishment is offered. Restore a
consistent earlier KSP save to recover. Existing files are not edited by this
source patch. ConfigNode API signatures/copy behavior were inspected in the
installed KSP assembly; third-party implementation code is not redistributed.

Production ScenarioModule tests with boundary doubles cover malformed flags,
missing flags beside a ledger, future schemas even when not started, repeated
unknown children, copied-state isolation, legacy compatibility, and load-state
reset. Campaign tests and full KSP compile pass. Actual KSP scenario persistence
still needs disposable save/load verification; boundary tests do not prove it.
