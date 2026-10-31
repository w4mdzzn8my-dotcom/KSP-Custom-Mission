# Onboard rendezvous instruments candidate

The Operations rendezvous tab is a read-only instrument for local cooperative
flight planning. It selects another orbiting vessel around the same body without
opening the map. Selection belongs only to this panel; it does not change the
stock target, SAS, engines, maneuver nodes or warp.

## What the figures mean

Both vessels' orbit position and velocity are sampled at the same Universal Time
in the same body-relative coordinate convention. Range is their separation;
relative speed is the magnitude of their velocity difference. Positive closing
speed means decreasing separation, while negative means opening. At coincident
positions the instantaneous range-rate direction is undefined.

The closest-approach display minimizes separation assuming constant relative
velocity over the 60 seconds after the displayed snapshot epoch. It is a short linear projection. Orbital
curvature, thrust, terrain, colliders and docking-port alignment are excluded;
a small projected distance is never docking clearance or a collision guarantee.
A minimum at the horizon is only the minimum within the displayed interval.

Unloaded targets use their orbit snapshots and are labelled accordingly. The
active vessel must be loaded/unpacked and orbiting. Pause, warp, a different
vessel/body/save or an unavailable target discards stale readings. The sample
UT is shown so the crew can tell which observation they are discussing.

## Verification and outstanding flight gates

Pure geometry tests and assembly compilation provide source-level evidence only.
Use disposable saves after the runtime restriction clears. These game checks
remain pending:

| Case | Required observation |
| --- | --- |
| Two same-body vessels | Range and relative speed agree with stock target instruments at the same epoch within documented display precision |
| Approach/opening | Closing sign agrees with observed range change |
| Internal selection | Stock target and vessel controls remain unchanged |
| Different bodies | Target is excluded or rejected; no mixed-frame result appears |
| Target load/unload | Snapshot label changes correctly; no stale loaded-state claim |
| Dock/remove/change target | Old measurements disappear and selection recovers |
| Pause/warp/revert | Results clear and resume only from fresh observations |
| Input and lifecycle | Operations hotkey/window/scrolling coexist with room and engineering panels; all locks release |
| Projection interpretation | Horizon and linear assumption remain visible; no interception or clearance success is awarded |

This is a local single-player overlay. It establishes neither shared LMP state
nor authenticated pilot occupancy, and it performs no automatic rendezvous.
