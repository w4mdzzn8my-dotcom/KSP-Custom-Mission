# Onboard docking instruments candidate

The Operations docking panel is a read-only comparison of two selected docking
ports on loaded, unpacked vessels around the same body. Port selection stays
inside this panel. It does not set KSP's target, control attitude or engines,
create nodes, or call docking actions.

## What the figures mean

The panel reads both port transforms in one sample. Separation is the distance
between their world positions. Axial offset is target-minus-own position
projected onto the own port's outward axis; positive means the target lies ahead
along that axis. Lateral offset is the remaining perpendicular distance. The
opposing-face angle is zero when the two outward axes point against each other.
These describe geometry only; they do not determine node compatibility,
clearance, capture range, collision risk or docking success.

Closing speed comes from the measured change in separation between two samples,
not an instantaneous velocity estimate. Positive means separation decreased.
It is unavailable until two samples are valid and no more than three seconds
apart. Each geometry snapshot is refreshed at 0.5-second intervals; its UT and
age are shown.

The axis choice follows the KSP1 [Docking Port Alignment Indicator (DPAI)](https://github.com/bfishman/Docking-Port-Alignment-Indicator/blob/5f6e4e45632fa2e84d77294690d2b7324366f1cb/Source/DockingPortAlignmentIndicator/DockingPortAlignmentIndicator.cs),
which reads `ModuleDockingNode.GetTransform()` position and forward direction
for port geometry. The [KSP modding API reference](https://kspmoddinglibs.github.io/KSPDocsSite/class_module_docking_node.html)
also documents the docking transform and `GetFwdVector`; its available page is
for 1.12.4 and does not itself establish the outward sign. The DPAI implementation
provides that port-specific sign evidence. Generic vessel target axes are not
used as a substitute.

## When readings clear

The panel requires the active and target vessels to be loaded and unpacked,
same-body, and to have selected docking ports. It clears selections or readings
when vessel, body, save/game context or time changes; time moves backwards;
pause or warp begins; a vessel's parts/selected port change; or port transforms
become unavailable. Select the ports again and wait for a fresh sample.

## Practice note

In a noncanonical watch drill, Hamish reads the signed axial offset while Basil
checks the separation and sample time. They agree that an aligned picture is
still only a measurement, then hand over the figures with the port identifiers.
This drill adds no emergency; the campaign keeps exactly its one planned
emergency.

## Candidate status and flight checks

The panel and geometry model compile and their selection/measurement behavior
has source-level tests. No KSP 1.12.5 runtime observation has been made, so the
feature is not runtime-qualified. In a disposable save, verify the axis signs
and angle against stock ports, compare range changes with measured closing sign,
and confirm target selection, part changes, pause, warp, save/vessel changes and
scene teardown clear stale readings. Also confirm the stock target and docking
controls remain untouched. Non-stock ports and unavailable transforms should
show no measurement rather than a guessed fallback.
