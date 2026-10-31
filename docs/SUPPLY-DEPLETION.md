# Observed depletion and supply reconciliation

Apply Hermes-Supply-Depletion.patch after the cumulative 0.3.4 source patch.
This focused source change is independent of the console-input and cryo-QA
patches. It contains no DLL or save and does not enable accelerated coast.

Previously, an observed ration shortage set a permanent supply error while the
daily routine suppressed the practice needs model. That left hunger, thirst
and the ledger frozen after the first depleted interval.

A new persisted SupplyDepleted flag now describes an observed shortage. Finite
food/water transactions, actual-UT routine needs and cumulative unmet demand
continue over valid observed intervals. Restocking accessible stores supports
gradual recovery during later funded meals. Historic unmet demand is retained;
the adapter does not silently take extra supplies to repay old shortages.

Unknown intervals, regressed clocks, missing cryo/crew state and malformed save
history still quarantine accounting. Depletion is distinct from those errors;
this change does not guess what happened while the vessel was unobserved.
Old 0.3.4 depletion-error saves migrate the exact known depletion message into
the new flag, preserving all totals. The next interval must still satisfy the
existing observation rules, including the 60-UT-second limit.

Production supply adapter tests cover repeated shortage, rising hunger/thirst,
finite resupply, gradual recovery and retained history. Production scenario
save/load tests cover the flag, legacy migration and malformed history. These
use KSP boundary doubles; actual KSP load, time, resource and DeepFreeze behavior
remain pending. Full KSP 1.12.5 plugin compile passes. No play copy was updated.
