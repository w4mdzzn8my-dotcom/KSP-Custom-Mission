# Observed ElectricCharge survey candidate

The Operations Supplies tab can record a continuous 60-second survey of stored
ElectricCharge. It is a read-only measurement, available without an operator
trait or duty grant. It never deploys equipment, consumes resources or permits
cryosleep/coast.

Start the survey on a loaded, unpacked non-EVA vessel at actual 1x. The collector
samples total ElectricCharge and capacity about every two KSP UT seconds. It
binds the vessel GUID, save, Game instance, body and exact sorted part IDs.
Pause, warp, time regression, excessive gaps, changed capacity/structure or
unavailable/invalid resources end collection with partial observations.
Completion requires at least 60 seconds covered by accepted samples with no
gap above five seconds; an idle timer or duplicate timestamp cannot complete it.

The report shows the captured vessel and UT window, observed elapsed coverage,
sample count, net stored-charge change/rate, sampled minimum/maximum, and recent
raw observations. Before a second timed sample the rate is unavailable. A
completed/cancelled report retained on screen belongs to its recorded window;
it is not a live reading of a later vessel. Results are transient.

## Interpretation

Compare observations with actual device states and operating conditions. Total
stored EC includes all reported vessel stores; it is not a promise that each
pod or consumer can access that charge. A full store can hide excess generation,
and an empty store can hide unmet demand. Zero net change alone proves neither
power balance nor readiness. The sampled extrema may miss changes between
samples. Net storage change cannot attribute a cause to one device.

The survey predicts no endurance, sunlight/eclipse history, thermal margin,
provider behavior or future demand. It changes no campaign clock, crew need,
resource state, fault flag or mission achievement.

## Evidence and runtime gates

Pure model tests exercise drain, recharge, steady/varying observations, exact
completion boundaries, duplicates, gaps, regressions, changed capacities, invalid
inputs, cancellation and immutable snapshots. Actual Unity telemetry remains
unqualified until disposable flight testing.

| Check | Required live observation |
| --- | --- |
| Known drain/recharge | Raw samples and summary agree with stock EC at the recorded times |
| Full/empty store | Zero net change is presented without an endurance/readiness claim |
| Normal 60-second run | Completion follows accepted observations, with correct timestamps/count |
| Pause/rate/load gap | Partial report, no unobserved interval counted |
| Dock/decouple/save/game change | Collection stops for changed identity/structure |
| Capacity or malformed resource | No apparently valid aggregate masks invalid part readings |
| Simultaneous station use | Survey remains read-only while actual hardware controls retain duty checks |

Use the current single cumulative campaign packet and reviewed addons in a
disposable build. Source/model tests and compilation do not qualify cryopods,
high-rate coast, or multiplayer synchronization.
