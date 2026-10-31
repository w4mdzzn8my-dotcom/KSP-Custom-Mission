# Hermes: implementation order and future campaign

The owner requested Luna multiplayer and custom stories **after the single-player foundation is complete**. This is an ordered roadmap, not a claim these features are implemented or a scheduled background task.

## 1. Finish and validate the single-player ship

- Load the orbital trainer reliably and preserve saves across restarts.
- Verify physical stability, usable hatches and internal routes. Repair or replace the problematic centrifuge before making rotation a mission requirement.
- Test room-based interactions: engineering deployment, galley, bunks, lab/medical station and real DeepFreeze pods.
- Verify finite Food/Water consumption, no pantry refill on reload, hunger/thirst/fatigue progression and safe watch rotation.
- Build lander storage and usable landers, with proven clearance, release/recovery, crew capacity, propulsion, target-body limits and return capability.
- Confirm planet graphics render correctly at an acceptable frame rate on both Macs.
- Complete the maintenance practice sequence, save/reload it, and repeat independently on Basil's copy. Record PASS/FAIL evidence in issue #1.

The current room panels do not make every decorative prop interactive. Physical EVA repair, autonomous NPC activity and fully interactive cockpit navigation remain implementation work, not completed features.

## 2. Integrate Luna Multiplayer

Use separate test copies and a separate test server Universe. Keep existing saves and the original server's IPv4 fix/backup untouched. Do not publish passwords or credentials.

Start with one shared engineering switch and a deterministic fault. Define one authoritative owner for mission state, supplies, crew health, cryosleep and objectives. Commands need unique IDs, authority checks, deduplication and full reconnect snapshots. Do not let two clients independently consume supplies or perform conflicting repairs.

Then verify distinct player Kerbals, interior positions, station handover, resource state, crew rest, actual freeze/thaw and objective progression on both clients. Keep engineering and science useful alongside piloting; allow two players to hand over roles.

Acceptance: both clients enter the same Sandbox session, observe a shared vessel/Kerbal and the same state changes, reconnect without duplicating actions, and save/reload consistently. A successful connection alone is not completed co-op integration. The existing console is single-player and Luna does not automatically synchronize its custom state.

Keep the owner's **no time warp** rule. Crew cryosleep does not accelerate the universe. Any future change to shared time progression must be explicitly agreed and separately tested.

## 3. Add custom cooperative stories

Use an authored campaign with a mission log, prerequisites, clear evidence for objectives, choices and consequences. Story milestones should advance from observed game state or clearly identified crew sign-offs. Reuse an authoritative event system in multiplayer so both players receive the same briefing, incident and outcome.

Candidate chapters (design proposals):

1. **First Watch:** commission Hermes in Kerbin orbit, complete supply/medical checks and rotate the relief crew.
2. **The Warm Loop:** investigate a cooling fault; pilot holds safe attitude, engineer isolates a loop, scientist protects an experiment. Diagnose before deciding whether EVA repair is necessary.
3. **Quiet Side:** an eclipse tests reserves. Choose which experiments to pause, maintain cryopod power and verify recovery after sunrise.
4. **Lander Readiness:** inspect stored landers, verify propellant and communications, rehearse release and recovery before a surface mission.
5. **Red Horizon:** plan a scientifically grounded Duna campaign, surface measurements and a defined ascent/rendezvous/return budget. Travel duration follows the game clock.
6. **Long Way Home:** manage finite spares and supplies, reassess crew injuries, and recover the expedition.

These chapters are not installed missions yet. Add one short, fully tested story before expanding the campaign. Cooperation and maintenance tasks only: no impostors, murder or player sabotage. Injuries and readable emergencies can create problems to solve, with warning time and recovery options.

## Coordination

Use GitHub issues/PRs with concise findings, changes, next action and evidence. Hami's Codex currently owns custom console/interior/needs source; Basil's Codex was asked to own the separate lander/storage craft contribution. Agree new boundaries before overlapping edits. Keep story proposals separate from implementation status.
