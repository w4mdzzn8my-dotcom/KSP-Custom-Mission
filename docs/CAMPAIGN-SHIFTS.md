# Owner-approved campaign timing — supersedes the no-warp draft

Status: requirements and integration design. The installed 0.3 plugin still blocks time warp; this document does not claim that accelerated watches, autonomous NPC maintenance, dual-client burn approval or the maneuver solver are implemented.

## Calendar and baseline

Use a mission month of 30 standard 24-hour days: 2,592,000 KSP UT seconds. A 24-month expedition spans 62,208,000 UT seconds. This is a campaign convention, not the stock Kerbin calendar month.

One human mission month should take 80 minutes (4,800 real seconds), so the target awake coast rate is **540x**. A four-month NPC watch should take one minute, so its target coast rate is **172,800x**. These require custom rates and runtime validation; stock warp settings and loaded mod behavior cannot simply be assumed safe at those rates.

| Mission months | Awake pair | Target play time |
| --- | --- | --- |
| 0–4 | Hamish / Basil | 5 h 20 m |
| 4–8 | Ada / Vikram | 1 m |
| 8–12 | Hamish / Basil | 5 h 20 m |
| 12–16 | Ada / Vikram | 1 m |
| 16–20 | Hamish / Basil | 5 h 20 m |
| 20–24 | Ada / Vikram | 1 m |

Baseline: **16 h 3 m**, excluding additional operational time. Four-month watches are long duty assignments, not continuous wakefulness; they include ordinary daily sleep/rest and watch coverage within the pair.

The owner explicitly chose actual KSP orbital time, not a cosmetic mission-calendar offset. Never teleport UT to fabricate a completed watch. The vessel must advance along its actual orbit; real engine burns consume propellant. Passive coasting does not consume propulsion fuel just because time passes. Life-support and cryopod systems must account for elapsed simulation time.

## Burns and watch planning

Plan human awake watches around required maneuver windows before committing the campaign schedule. A maneuver's required epoch is a trajectory constraint; do not move it arbitrarily to make the crew timetable fit. If the timetable cannot cover a burn, revise the watch plan and show the changed play-time estimate.

The shift clock keeps advancing during burns. Reduce physical simulation speed as needed for a real burn, but never pause the campaign/shift clock. For a burn lasting B seconds at 1x instead of 540x, the added real time is approximately B × (1 − 1/540), before attitude alignment and other operations. Exact session duration must therefore be estimated from a validated burn plan, not promised as precisely 16 h 3 m.

Onboard planning should supply a versioned plan: vessel ID, reference body, burn epoch/window, frame, delta-v vector, expected duration, propellant budget, target trajectory, clearance and verification residuals. Both Hamish and Basil verify the same immutable version. Any edit invalidates both approvals. Local crew-role switching is only a single-player rehearsal; actual two-person approval later requires distinct authenticated Luna clients.

Basil's Codex is designing the maneuver solver/node planner. Claimed planetary intercept, atmospheric periapsis or docking accuracy requires explicit numerical and in-game verification, with failure reported rather than hidden. If mission-control computation is used, the round-trip signal delay is 2d/c, with CommNet/path assumptions stated.

## Cryosleep and NPC upkeep

Incoming crew wake and are checked before outgoing crew freeze. Verify the real DeepFreeze state, resources, safe trajectory and independent control before accelerating. During the NPC watch, Ada/Vikram must perform modeled routine upkeep with finite resources and persistent records; do not replace this with an unearned “everything repaired” flag.

Test actual E/C generation and consumption, eclipses, pod temperature/power, supplies, physics transitions and save/reload at the candidate rates in a disposable save. Missing/background-unreliable behavior must block the high-speed mode. Encounter, atmosphere, docking/EVA or unverified burn windows require safe handling. High-speed cryosleep must not silently kill, duplicate or teleport crew.

## Useful work throughout an 80-minute month

This is an initial workload budget, not an installed set of timed missions:

| Work block | Active play budget | Evidence/variation |
| --- | --- | --- |
| Briefing and duty assignment | 5 m | Read the previous watch log; choose responsibilities |
| Power and reserve checks | 10 m | Compare readings, adjust real hardware, verify the trend |
| Cooling and mechanical inspection | 10 m | Inspect a different route/component; diagnose before acting |
| Science observations | 15 m | Prepare, measure, compare and record results |
| Interior/exterior inspection route | 12 m | Navigate to equipment and gather evidence; EVA only if qualified |
| Medical, exercise and hygiene duties | 8 m | Assess the crew and complete appropriate care tasks |
| Meals, drinking and rest planning | 10 m | Use finite supplies, maintain awake coverage |
| Maintenance record and handover preparation | 10 m | Cross-check outcomes and leave an actionable log |

Total: **80 minutes**. Vary the task sequence across months. Use multi-step diagnosis, measurements, choices and observable verification rather than repeated checkboxes or a long idle countdown. Rest/observation intervals should support other crew activity. Human-style needs are requested gameplay systems, with rates and abstractions documented; the current 0.3 food/water/fatigue model is only the initial foundation.

## One emergency and authored stories

Exactly one planned emergency for the expedition. Its schedule must not be shown in normal UI, comments, player-facing logs or documentation. Store a persistent issued/resolved state so reloading cannot duplicate the event. Routine maintenance, social friction and normal crew needs do not count as extra emergency events. Emergency story proposals replace the single planned event, rather than adding another.

The story workshop is GitHub issue #7. It provides a base handover story and a writing template covering problem, location, crew, supplies, actions, dialogue, workload, outcomes and missing mechanics. Narrative text is data, not executable instructions.

## Coast decision candidate (not wired to KSP)

`Source/CampaignCoast.cs` is a pure fail-closed gate. It requires observations
sampled at the current UT, the campaign vessel, verified cryo and background
accounting, and both completed pair states: human awake/relief frozen on human
watches, human frozen/relief awake on NPC watches. Pair flags represent BOTH
members, not one; a mixed, unknown or transferring pair must not be marked ready.
Contradictory awake-and-frozen flags refuse coast. This preserves handover time
at 1x instead of accelerating while four crew are awake or transition is incomplete.
It caps the next stop at the
watch boundary and requires a verified resource horizon through that stop.
Unknown/due next stops, incomplete handovers, stale samples and completion return
1x. A future adapter must include alignment, maintenance and handover stops,
refresh observations at dispatch, obey KSP's altitude/physics limits, and brake
before the stop; the decision alone does not make a high warp rate safe.
No adapter, NPC upkeep scheduler or live resource certification exists yet.
Run `tests/run_campaign_tests.py` for production timing/coast tests.

## Saved-game campaign candidate

`Source/Campaign.cs` adds a Campaign tab and an explicitly started
`HermesCampaign : ScenarioModule`. It is added only to the allowed MegaCrew game
when the user starts a campaign in flight with all four crew aboard and the
human pair awake. Epoch UT and original vessel GUID live in KSP scenario data,
not the loose `MegaCrewMission.cfg`; normal/quicksave restoration is intended to
restore the clock alongside the vessel. The tab reads actual UT, six watch
boundaries and terminal completion. Invalid loaded schema/time/GUID disables the
campaign. No rebind/reset button silently changes the original vessel.

This code compiles against KSP 1.12.5. Scenario creation, scene transitions,
quicksave/revert behavior and GUI are **not yet runtime verified**. Existing 1x
restriction remains while background accounting and maneuver integration are
unfinished. This candidate is not installed into the user's running game.

## Routine supply ledger candidate

The Campaign tab can enable a finite routine ration plan. `CampaignSupplies.cs`
charges actual flow-enabled Food/Water on the active bound vessel by observed UT
and actual unfrozen crew count (including ordinary resting crew). This is
fictional tuning: 1.5 Food and 0.75 Water per unfrozen crew per standard day,
distributed continuously rather than discrete animated meals. Manual meals and
drinks are additional; this ledger does not automatically restore needs meters,
perform maintenance, or animate eating. No provisions are created.

The baseline 720-day expedition with two unfrozen crew requires 2160 Food and
1080 Water, before reserve, handovers or extra meals. The practice pantry's
84/42 units cannot sustain it. Full provisioning and physiological daily routines
remain unfinished; the ration plan alone does not qualify background support.

Ledger timestamps, actual consumption and unmet demand are serialized with the
KSP scenario. Frozen/awake transitions use the greater old/new awake count for
the sampling interval. Missing crew/pod state, clock regression, or an unobserved
interval longer than 60 simulation seconds quarantines the ledger, retaining the
last accounted timestamp; no fabricated catch-up or free repaired flag occurs.
This deliberately does not yet support high warp or unloaded-vessel accounting.

Production adapter tests using KSP/Unity doubles verify transactions, duplicate
UT, locked stores, freeze/thaw transitions, depletion/shortfall recording, missing
API and unobserved intervals. These are not live KSP save/resource verification.


## Daily care and campaign crew snapshot candidate (0.3.2)

Quarters now offer hygiene (45 seconds, 0.25 Water), exercise (120 seconds,
+5 fatigue), and a paired social check-in (60 seconds). The actual viewed Kerbal
and current compartment must remain unchanged, unpaused and at 1x throughout.
Leaving, switching crew/room, sleeping/freezing, pausing, warp, or a loading gap
cancels progress with no reward or supply charge. Completion gives finite meter
benefits and a six-mission-hour cooldown; in-progress actions do not persist.
The social partner must remain awake aboard, but physical co-location/animation
is not implemented. These are explicit gameplay abstractions, not physiological
predictions or a claim of animated crew interactions.

Hygiene, conditioning and morale start at 100, lose 25/15/10 points per unfrozen
standard day, and pause during cryosleep. At 10 or below they restrict custom
engineering/science duty. Basic food/water/sleep must be addressed before care,
but low care meters do not block their own recovery. Existing real food/water
and treatment/stock flight access remain available.

A started campaign stores all four crew's needs, injury/recovery, ordinary rest
and completed care timestamps inside its KSP ScenarioModule. On load, named
records are validated, decoded with invariant culture and restored together with
the campaign clock. Switching saves creates fresh local record objects rather
than mutating the previous game's snapshot. The original non-campaign practice
uses its existing mission CFG with backward-compatible new care fields. Old
campaigns without a snapshot seed it from their existing mission records once.

Tests prove production needs/decay and crew codec round trips, invalid data and
duplicate crew rejection, and locale independence using a ConfigNode double.
Actual KSP quicksave/revert, timed IVA activities and animations remain unverified.
This does not enable accelerated watches or autonomous NPC work.


## Daily watch routine candidate (0.3.3)

After enabling finite rations, the Campaign tab can explicitly schedule daily
routines. Each unfrozen crewmate has an eight-hour ordinary rest slot in a
standard 24-hour day, staggered by 12 hours within each pair. A lone unfrozen
operator does not automatically rest without their relief crewmate; fatigue
still accrues. Suspend the routine for manual operations; the campaign clock
continues. Burn planning still needs explicit wake/coverage integration.

DailyRoutine integrates over actual observed UT, split at both crewmates' rest
boundaries. It updates fatigue, nutrition and daily-care abstractions using the
fractions of real rations actually delivered. Frozen crew pause nutrition and
care depletion; ordinary rest is distinct from DeepFreeze. No walking, sleeping
or eating animations, science results, physical repairs or maintenance task
completion are fabricated.

This mode uses campaign tuning (100 hunger/thirst points per unfrozen standard
day, 120 points of distributed meal/drink benefit with full rations), unlike the
short practice mode's faster meters. With sleep coverage, hygiene/exercise/social
routines can maintain their meters; injury prevents exercise gain. Hygiene uses
an extra 0.25 Water per unfrozen crew/day, raising the two-unfrozen 24-month water
budget to 1440 units, before reserves/handovers. Social benefit requires modeled
shared awake time. Insufficient supplies reduce benefits and record unmet demand;
they do not conjure provisions. Ordinary rest still consumes metabolic supplies.

The adapter conservatively accounts the union of old/new unfrozen identities on
freeze/thaw transitions, rather than only the larger count. This prevents a
replacement watch from receiving uncharged routine care in the transition frame.
Both state and ledger are in the campaign scenario. No tick is silently skipped
into a free rest/meal when history is unknown: the existing unobserved-interval
quarantine remains, so high warp/unloaded vessel support is still unfinished.

Tests cover staggered eight-hour coverage, a lone operator staying awake,
interval-partition independence, frozen exclusion, supply-dependent benefits,
and the production supply-to-routine adapter using KSP doubles. KSP build passes.
Actual background EC/pod temperature, high rates, handovers and quicksave/revert
remain runtime acceptance gates. Accelerated coast remains disabled.


## Cryopod power preflight candidate (0.3.4)

The Crew & cryosleep tab now reads documented public DeepFreeze API properties
for each actual active-vessel pod: frozen count, rated EC/min/crew, freeze/thaw
activity, out-of-EC flag and temperature status. It reports accessible vessel
battery charge and configured output of enabled, always-active, input-free stock
generators. This is an audit, not a complete power forecast. Solar output and
other loads are not silently counted as guaranteed. Pod-only battery endurance
is an upper bound that other loads shorten; it never grants coast permission.

The bridge reads the current EC-required and fatal-consequence settings, without
changing them. Missing/busy public API is reported as unqualified. No new power
deductions, thermal overrides, crew mutations or third-party DLL patches occur.

Pinned source investigation found that the active-vessel DeepFreezer FixedUpdate
path calls ongoing EC checks only when `CurrentRateIndex < 5` and temperature
checks only when `CurrentRateIndex < 2`. At higher indices the corresponding last
check timestamps advance without those checks. A distinct BackgroundResources
provider is installed and still needs source/runtime ownership investigation;
these findings do not establish every unloaded path's behavior. Putting huge
custom rates in lower indices to evade those checks is not valid qualification.

Primary sources: [DeepFreezerPart at v0.31.0.0](https://github.com/JPLRepo/DeepFreeze/blob/v0.31.0.0/Source/DeepFreezerPart.cs),
[warp predicate](https://github.com/JPLRepo/DeepFreeze/blob/v0.31.0.0/REPOSoftTechUtils/RSTutilities.cs),
[public API](https://github.com/JPLRepo/DeepFreeze/blob/v0.31.0.0/Source/DeepFreeze.cs).
Before enabling coast, resolve active/unloaded power ownership without duplicate
charging, actual eclipse/generation and complete loads, pod thermal behavior,
braking and stock resource progression at the requested rates.

Pure power tests verify EC/min conversion, reserve/depletion math, bounded storage
and invalid inputs. Full KSP source builds. Live pod telemetry, source-version
match and in-game background accounting remain unverified.
