# Shared local crew duty query

Public API in cumulative host source 0.3.6:

```csharp
string reason;
bool fit = MissionConsole.CanPerformDuty(member.name, vessel.id, out reason);
```

Call for the specific operator immediately before a normal mutating custom
command, after checking the crew trait and any room/station restrictions. A
disabled button is insufficient; recheck at dispatch. Iterate candidate roster
members if the operation allows any qualified operator. No mutable CrewRecord
is exposed, and querying does not feed, rest, thaw, treat or authorize a burn.

The query requires a live enabled console, loaded/unpacked non-EVA mission
vessel, matching loaded save, actual Game instance and vessel ID, ready unpaused flight at actual 1x,
known unfrozen status, actual onboard membership and NeedsModel.Fit. An active
campaign must have its crew snapshot bound to that vessel; rejected campaign
data or a quarantined supply history denies normal duty. Healthy replenished
crew can recover eligibility through existing gameplay; depletion alone is
not a permanent block when the crew's actual meters remain below thresholds.

NeedsModel.Fit now also rejects a recorded injury until recovery clears it.
NeedsModel.CareReady stays available for recovery activities when basic needs
allow them. This also makes injury restrict the I panel's normal custom duties.
It does not alter stock emergency flight controls or emergency treatment.

Basil owns wiring the standalone engineering/operations mutating controls.
Read-only retained-data/rendezvous views do not need an operator-duty gate. The
query does not enforce a role, physical room, skill training, authenticated
Hamish/Basil identities, shared Luna authority or two-party burn approval;
those remain separate callers' requirements. Do not use it as authorization
for multiplayer commands.

Production adapter tests use game boundary doubles and verify rest/injury,
needs thresholds, same-name game replacement, pending/duplicate campaign modules, stale/unbound data, unknown cryo, wrong vessel, pause/rate
and disabled console rejection, plus read-only behavior. Needs tests and full
KSP compile pass. Unity instance lifecycle, live scene/quicksave rebinding and
actual dispatch refusal/recovery remain disposable runtime gates.


## Observed medical recovery (0.3.7 candidate)

Treated injuries retain the existing fictional five-minute recovery period.
Recovery advances during observed, loaded/unpacked, unpaused 1x flight even if
the practice watch tracker is stopped. It neither pauses the campaign clock nor
uses wall-clock/offline catch-up. Genuinely frozen crew do not heal; unknown
cryo state, active transfers, scene gaps, stale game/vessel bindings, and
uncertain campaign history suspend recovery. Untreated injuries never heal via
this timer. Completing recovery clears only the recorded injury/treatment;
food, water, fatigue and care fitness still apply independently.

`tests/run_crew_recovery_tests.py` compiles actual production recovery and duty
query code together against recording KSP boundaries. It checks treatment,
refusal states, five-minute completion and subsequent duty eligibility. There
is no practice running flag in that fixture. This is source/model evidence;
actual flight/save/reload and UI refusal/recovery remain pending.
