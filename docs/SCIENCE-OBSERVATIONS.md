# Retained science observations candidate

The Operations Science tab can capture a baseline of reports stored aboard the
active vessel, then compare a later capture. It also shows the current stock
science situation, body, altitude and geographic biome. Experiment eligibility
still belongs to the stock experiment; a displayed biome does not establish that
a particular experiment uses biome-specific results.

## Procedure and meaning

1. With an unpacked active vessel and normal unpaused
   1x time, capture the baseline. The panel records vessel identity and UT.
2. Operate a fitted experiment through its existing stock Run button and choose
   how to handle the result in the stock dialog. The request itself earns no
   observed completion.
3. Compare a fresh retained-data capture. Read subject IDs, before/after quantities,
   reported entry counts and both timestamps. A report retained aboard may
   increase a subject total; transmission, transfer away or disposal may decrease
   it. An unchanged inventory says nothing about science points earned elsewhere.

Captures are manual and temporary. They are discarded on scene, vessel, body,
save/game changes or UT rollback, and are not a persistent mission achievement.
Failed captures do not leave a previous comparison displayed as a fresh one.
No experiment is run automatically, and the observer never transmits, dumps,
resets or moves reports. The existing read-only inventory listing remains available.

## Measurement model

The collector calls `IScienceDataContainer.GetData` once for each module, treating
a null result as empty according to the container API. A stock experiment is
already such a container. Identical `ScienceData` object references exposed by
multiple wrappers count once. Distinct cards with the same exact `subjectID`
contribute to that subject's aggregate. There is no documented unique report ID;
third-party providers returning copied wrappers cannot be reliably deduplicated.

The pure model copies quantities into immutable snapshots, sorts IDs and amounts
for deterministic aggregation, and compares finite nonnegative retained units.
Reordering or transferring the same inventory inside the vessel does not create
a model-level increase. Data units are not science points, unique discoveries or
proof that a specific experiment request succeeded.

## Runtime acceptance — pending

Source/model tests and compilation do not establish stock UI behavior. Use a
disposable save after the overnight runtime restriction clears.

| Check | Required observation |
| --- | --- |
| Empty vessel inventory | Baseline and comparison show zero; no success is awarded |
| Retain a stock result | Observed subject/quantity agree with the stock container |
| Internal transfer | Whole-vessel totals remain unchanged for the same reports |
| Multiple cards/containers | Legitimate cards aggregate; the same object view is not counted twice |
| Transmit or remove | Comparison reports retained inventory change, without inferring rewards or failure |
| Failed read | No partial/fresh result is shown; retry path is clear |
| Quickload/revert/context | Prior session baseline is invalidated correctly |
| Pause/warp | Capture controls disable; the panel Run action requires a selected Scientist and shared readiness |
| Input and layout | Science, room and engineering panels coexist; locks and scrolling recover on close/scene exit |

Read-only captures are available to any panel operator, including the human
pilot/engineer watch while the scientist is frozen. The panel Run action requires
a selected Scientist and shared duty readiness; human-watch experiment operation and shared
duty-readiness gating remain integration work. These are local single-player overlays. No
physical scientist occupancy, authenticated player identity or LMP synchronization
is established by this candidate.
