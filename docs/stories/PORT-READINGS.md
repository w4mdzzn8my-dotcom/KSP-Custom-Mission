# Routine scene: Port readings

**Status:** proposed routine cooperative scene using the current docking,
power-survey and retained-science instruments. It is not a playable story
trigger, does not claim a completed dock or qualified flight, and adds no
emergency or emergency timing. The planned single emergency remains the only
emergency in the campaign.

The human watch is on duty: Hamish is at the command deck and Basil has the
engineering watch. Ada and Vikram remain frozen in the relief pair. The crew
does not thaw them to make this scene work. There is useful routine work for
the two people already awake.

## Port check

Hamish selects the intended target vessel in the docking panel. Basil asks him
to read the target vessel and the two selected port identifiers aloud before
they discuss the geometry. The panel shows its own and target part IDs, port
state, observation time, separation, axial and lateral offset, and opposing-face
angle. Those labels give both crew a shared reference for the reading.

They first discover that Hamish selected a second port on the target vessel.
The picture looked plausible, but the part ID does not match the port on their
checklist. He selects the listed port; the readings clear and return only after
a fresh sample. Basil does not treat the old angle as current. The panel still
does not set the stock target, command attitude or engines, create a maneuver,
or establish docking clearance.

“Read the part number back to me before we use the angle,” Basil says.

“Confirmed. Same body, loaded ports, new sample time,” Hamish replies.

They record the displayed identifiers and UT in the handover notes. If the
target becomes unavailable, a port changes, or the vessels cease to meet the
instrument's context rules, they stop and choose again after the context is
valid. No one calls the geometry a docking attempt or success.

## Power interval

Basil starts a 60-second ElectricCharge survey at actual 1x on the loaded,
unpacked vessel. The report is tied to the vessel, its structure and the UT
window; it needs accepted observations, not simply sixty seconds on a timer.
Hamish reads back the first and last sample times and sample count. They compare
the observed stored-charge change with which equipment was actually operating
during that interval.

If the interval is interrupted by pause, warp, a long sample gap, a changed
capacity or vessel structure, they keep the partial result labelled partial and
start a new interval only when ready. If stored charge is unchanged, they note
that it proves neither balance nor spare endurance. A full or empty store can
also hide generation or unmet demand. Neither pilot nor engineer attributes a
change to a particular panel, radiator, pod or consumer from the vessel total
alone.

“The trace is flat,” Hamish says.

“Then the observation is flat. Check the devices and the next interval before
we write a cause beside it,” Basil answers.

The useful choice is whether to repeat the measurement in a deliberately
different, recorded equipment configuration or leave the first window intact
for the next watch. They do not silently combine windows or change vessel
hardware as part of the survey.

## Stored science handover

At the science station, the crew reviews retained reports using an explicit
baseline and a later manual capture. They read the subject IDs, stored data
units, entry counts and both capture times. An unchanged inventory is still a
valid observation. A decrease prompts a check of the actual containers and
recent transfers; it is not evidence that a science reward was lost. An
increase is retained data, not proof of experiment success, transmission or
new discovery.

The stock experiment Run action remains separate and keeps its Scientist and
shared-duty requirements. The routine reading does not require Ada to be awake:
read-only observation is available to the human watch while she remains in
cryosleep. Hamish checks the body, altitude and biome context display, while
Basil confirms that the capture belongs to this vessel and not an earlier
session. The context is not treated as proof that a selected experiment uses
biome-specific results.

## Handover and presentation boundary

Before the next watch, Hamish and Basil give the incoming pair the actual port
IDs and sample UTs, the power-survey window and status, and the science
comparison's capture times and subject changes. They separate measured values
from their interpretation and note which task needs a fresh sample. They make
no claim that a visual NPC gesture completed the work.

If a future separate addon presents an animated social check-in, it should use
a supported public care-session query from the console (requested, not yet implemented) and remain a
presentation layer. It must not inspect private console fields or award care,
morale, duty readiness, port clearance or task completion. The current care
activity model updates its own meters; the addon must not become a second owner
of crew state. The presentation may stop when the viewed Kerbal or room changes,
the partner is no longer eligible, or the session is cancelled. Whether KSP's
runtime can show the intended characters and animation in the actual IVA remains
an evidence gate.

This is ordinary cooperative measurement and careful handover. A mislabeled
port, stale interval or changed inventory can be corrected by reselecting,
resampling or checking the relevant stock state. None creates a fault, injury,
repair requirement or additional emergency.

## Implementation and evidence matrix

| Scene element | Existing seam | Required evidence before claiming it in a playable story |
| --- | --- | --- |
| Port selection and geometry | Operations docking panel; selected port keys resolve to actual `ModuleDockingNode`s and report part IDs | In disposable flight, verify displayed IDs, transform signs, sample timestamp, port reselection invalidation and unchanged stock target/controls; non-stock ports with unavailable transforms must show no guessed reading |
| Relative-motion reading | Closing speed is derived from separation samples, not a single instantaneous velocity | Confirm two valid samples within the documented interval and sign against observed range change; never present it as docking clearance or capture prediction |
| Power survey | ElectricCharge samples over the accepted 60-second coverage window | Capture raw times/count and actual vessel amount/capacity; verify interruption, gap, structure and capacity changes leave a partial/invalid report, and zero net change makes no endurance claim |
| Retained science comparison | Manual vessel-wide science-data baseline and later capture | Compare real subject IDs and stored quantities against stock containers; verify transfer/transmit/removal interpretations and make no inference about science points or experiment success |
| NPC social presentation | Separate addon requires a supported public care-session query, still requested | Public query must expose only presentation context needed (actor, partner, room/session validity); test start, completion, cancel, actor/room change, partner frozen/resting/unavailable, pause and scene teardown; care model remains sole owner of benefits |
| Cooperative handover | Crew readback and notes; current onboard log supports manual entries | Record actual identifiers, UTs and report statuses from a runtime session; distinguish measured observation, interpretation and pending recheck |
| Campaign emergency count | This scene is routine and has no emergency beat | Confirm authored campaign still contains exactly its one planned emergency; do not add a timer, incident, fault or scheduled emergency to this chapter |

Source compilation and model tests alone do not establish the KSP IVA
presentation, live port-axis signs, stock-container behavior in flight, or
co-op synchronization. No measured values are supplied in this proposal; a
playable version must use the values and identifiers actually observed in its
runtime session.

## Supplies, workload and choices

The readings consume no Food, Water or Glykerol. Real background consumers
continue to draw ElectricCharge; the scene does not inject charge or assume
cryo endurance. Port inspection needs two actual loaded vessels with docking
ports; science comparison needs actual retained ScienceData. Missing prerequisites
leave the corresponding scene stage unavailable.

Allow roughly 3–5 minutes of active port selection/readback, 2–4 minutes to
inspect equipment and complete an uninterrupted power survey, and 3–5 minutes
for retained-report review and the handover note. These are authoring targets,
not progression timers or a full awake month. If an observation needs repeating,
the player chooses a reason and records the new interval rather than waiting
for a fixed duration to earn credit.

Hamish can ask to repeat the configuration comparison now, or leave a clearly
labelled pending recheck for the next watch. Basil can prioritize resolving the
port-ID mismatch before comparing science. Neither choice changes trust or
mission state until an authored state owner implements and persists that branch.
A future save/reconnect record must preserve the chosen pending task and the
original observation identities/times, while requiring fresh live readings
before the resumed crew uses them. The present manual log is local single-player
notes and does not establish that shared story persistence.
