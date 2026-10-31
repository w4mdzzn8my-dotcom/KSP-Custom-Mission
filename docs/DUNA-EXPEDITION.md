# Hermes Duna expedition — planning candidate 0.1

This adds a real ephemeris-based round-trip search and an in-game itinerary preview to consolidated PR12 base `6e7ac04f1582c7b7ab65366e146bf3d73536a9c4`. It is the first Duna-specific implementation, not a playable or flight-qualified expedition. The current practice game is not changed or converted.

## In-game planner

In a MegaCrew-prefixed save, enter a loaded, unpaused, 1x near-circular Kerbin orbit above the atmosphere. Open **DUNA EXPEDITION**, or Control–Option–D on Mac, and choose **Search round trip**. The window is bounded and scrollable. Searches yield one attempted leg per iterator step, with at most two steps per frame; cancellation is explicit. Search inputs/results are transient snapshots. Scene/save/Game/vessel changes invalidate them. An already-started campaign is refused because this tool chooses a pre-start epoch and cannot rebase an active clock.

The adapter reads Kerbin/Duna's actual configured Orbit ephemerides around their shared parent and uses the existing short-way zero-revolution Lambert kernel. It rejects retrograde arcs. The default finite grid samples outbound departures over 360 standard days and transfer durations of 50–110 standard days. The eight lowest summed-v-infinity outbound candidates are searched for return legs over the remaining calendar. This beam/grid search does not find all arcs or establish a global optimum. An empty result means no candidate found within this search, not that a mission is physically impossible.

The initial propulsion adapter supports same-Isp, single-propellant engines, appropriate to a simple LV-N vehicle. It reads current wet mass, installed nominal vacuum Isp/thrust, thrust limits and unlocked propellant inventory. Mixed/staged/multimode engines are refused. It retains 15% of that propellant as reserve in the dry-mass constraint; fuel connectivity, future staging, vehicle reconfiguration, supplies consumed during cruise and changing propulsion conditions still require stock validation. No resources are consumed or fabricated. The adapter does not ignite engines.

Four propulsive parking-orbit costs are calculated from each heliocentric excess speed: Kerbin ejection, Duna circular capture, Duna ejection and Kerbin circular capture. Duna parking altitude is at least 100 km and above its atmosphere. The rocket equation carries changing mass through all four burns and estimates their durations at nominal constant vacuum performance. Atmospheric braking is not assumed. Surface/ascent, corrections, docking, plane/landing-site targeting and final crew recovery are additional budgets, not silently included in these four burns or certified by the 15% reserve.

## Calendar and mission sequence

Each mission month is 30 standard 24-hour days. The 24-month campaign has six 120-day watches:

| Watch | Crew awake | Intended work |
| --- | --- | --- |
| 1, months 0–4 | Hamish / Basil | Commission, depart Kerbin, outbound tasks, Duna capture |
| 2, months 4–8 | Ada / Vikram | Duna orbital upkeep while humans are actually frozen |
| 3, months 8–12 | Hamish / Basil | Duna surface/science operations and ship maintenance |
| 4, months 12–16 | Ada / Vikram | Orbital upkeep, genuine human cryosleep |
| 5, months 16–20 | Hamish / Basil | Duna departure and Kerbin orbital capture |
| 6, months 20–24 | Ada / Vikram | Kerbin-orbit upkeep; all-crew wake/recovery after completion |

The route is evaluated first; pre-start epoch ranges are intersected afterward. The complete provisional duty envelopes must fit human watches, with the return in watch5. Encounter epochs are never moved to fit a shift. Each envelope includes estimated finite burn duration centered on the encounter, one hour of alignment, one hour of verification, and a two-day encounter timing reserve on both sides. **The planet-centre encounter time is not an executable ignition UT.** The two-day margin is an explicit planning allowance, not proof of SOI/finite-burn coverage. A fitted stock patched-conic route must replace these provisional envelopes before approval. Surface/ascent/recovery burns must get their own verified human duty windows; the final post-campaign recovery sequence is not yet scheduled.

The middle human watch is intentionally retained for surface work. A dedicated Duna atmospheric/descent/ascent vehicle is required; the vacuum Finch candidate is not silently qualified as a Duna lander. Its separate clearance/release/redocking gates remain.

Human coast target: one month per80 minutes, 540x. Full NPC watch target:60 real seconds,172800x. All are still blocked until real cryopod/background accounting and runtime coast limits are qualified. The coast-only baseline is16h03m, plus actual slow burns/operations. The preview estimates only the extra time for its four nominal burns; alignment, tasks, landing, handovers and the one undisclosed emergency add time. Mission UT and watch clocks may never pause for a burn.

## Evidence and limits

`tests/run_duna_route_tests.py` compiles the actual model with the existing Lambert/timing/epoch code. Tests cover independent parking-orbit reference calculations, sequential rocket mass/time, unchanged physical event dates, whole-envelope watch intersections, third-human-watch return, insufficient fuel/stay/calendar rejection, bounded search and an analytical circular-planet fixture. An independent Cartesian RK4 propagator checks both leg endpoints/arrival velocities instead of trusting Lambert's own time residual. All608 assertions pass. These synthetic-ephemeris tests are not an actual stock Duna route or flight.

The full consolidated38 + Duna2 source candidate compiles against local KSP1.12.5 assemblies. It is not installed or launched in the play copy. No stock SOI/node/burn/resource/GUI/crew test follows from that compile.

Remaining implementation: actual KSP ephemeris/vehicle adapter observation, fitted SOI ejection/capture and burn vectors, course/surface/ascent/recovery maneuvers, propulsion/supplies/thermal qualification, complete separate mission fixture, measured objectives, maintenance workload and crew handover execution, one private planned emergency, per-plan dual approval and eventual Luna authority. `Missions/Duna/mission.json` records the intended requirements; it is not an executable mission loader or a claim of completion.

## Rebuild and review

Apply the additive Duna patch to the documented PR12 base or review its new files against a newer compatible consolidated branch. Do not overwrite the host core or install any older branch DLL. The existing recursive build helper compiles Source/**/*.cs; build to a new external output with --output. Run the Duna runner with your local --runtime and --csc. The main game, its saves, the Luna server, IPv4 fix and backups remain untouched.

## Consolidated review follow-up

Integrated additively from the pinned v0.1 packet at upstream a946a74345afec74c75b003a5e0c91f09bab5de5
(SHA-256 `22e47e301e22baf0336b8a3dd13ae2b1e3319978f01753777b4eb47d7449f72f`).
Independent review reran 608 model assertions. The integration intentionally
refines only the adapter panel: fixed GUI.Window with bounded scroll content,
always-visible controls, balanced GUI scopes, explicit eight-outbound search
limit and snapshot labeling. The pure planner/test/mission bytes retain packet
provenance. These UI refinements compile but await in-game verification.
