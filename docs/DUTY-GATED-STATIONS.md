# Shared readiness for onboard station commands

The station source now uses cumulative campaign 0.3.6's public
`MissionConsole.CanPerformDuty(crewName, vesselId, out reason)` query. Select a
named Engineer for solar/radiator commands, a Pilot for SAS, or a Scientist for
the panel’s stock experiment Run action. Selection is bound to the current vessel/save; an
unavailable selected operator does not silently switch to another crew member.

The panel shows the shared readiness result, then queries it again immediately
before each normal hardware/flight/science mutation after checking selection,
trait, vessel context and hardware. The campaign owns awake/frozen, rest, needs,
injury and uncertain-state policy. The addon does not duplicate those thresholds
or feed, wake, treat or modify crew in order to obtain eligibility. Ordinary
recovery must happen through the existing gameplay.

Read-only retained-data captures, inventory, orbital/rendezvous instruments and
supply readings stay available independently. Native stock actions and emergency controls outside these panels are not
intercepted or gated by this addon. The query supplies local normal-duty readiness; it proves
no physical room occupancy, authenticated player identity, Luna authority or
two-person burn approval.

## Source compatibility and build baseline

The source branch retains its older expanded base. The bridge resolves only the
exact public static query signature on the actual MissionConsole type. Missing,
incompatible, throwing or denied queries refuse commands with a reason; there
is no trait-only fallback and no cached eligibility result. This lets the addon
source compile against an older base while commands correctly remain unavailable
there. Use a reconstructed current combined build for testing.

Current recipe: pristine 0.3.0 ZIP plus cumulative 0.3.6 ONCE, then reviewed addon
sources. The 0.3.6 packet already includes the earlier epoch/state/readiness
work. Do not stack 0.3.5, epoch or the earlier supplementary patches onto it.
Build to a disposable output without replacing the installed play package.

## Verification and pending runtime checks

The bridge's recording-console tests check live ready-to-denied changes, exact
name/vessel forwarding, unavailable/incompatible APIs, exceptions and malformed
results. They do not execute Unity GUI or real device commands.

| Live case | Required evidence |
| --- | --- |
| Healthy selected operator | Supported command invokes its actual stock device/action |
| Operator becomes unfit/resting/frozen | Button/dispatch refuses without invoking device; read-only views still work |
| Recovery | Eligibility returns through actual care/rest; no automatic command runs |
| Selection changes | A rendered command never runs for a different implicit operator |
| Vessel/save/game reload | Old selection cannot authorize another context; pending crew status refuses |
| Pause, non-1x, packed/unloaded | No normal command dispatches |
| Hardware removed/unsupported | Refusal at dispatch, without applying a different command |
| Missing current core | Command unavailable; roster trait alone never authorizes |
| Emergency controls | Stock controls and recovery care remain independently accessible |

These checks remain pending in a disposable game. Compilation and recording
boundary tests do not establish runtime readiness or multiplayer authority.
