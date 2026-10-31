# Schedule watches around physical burns before campaign start

CampaignEpochPlanner is a pure pre-start planning helper. The caller supplies
actual solver-produced burn ignition UTs and their alignment/burn/verification
durations, plus a permissible earliest/latest campaign start. It returns all
start-time windows that put every complete duty interval in one of the three
four-month human watches. It does not change any burn epoch or duration.

For duty interval [a,b] and human watch h in {0,2,4}, epoch e must satisfy:

    b - (h + 1) * WatchSeconds <= e <= a - h * WatchSeconds

The helper intersects these unions for every burn with the permissible start
range. Empty results report a schedule conflict; the navigation planner must
review another physical trajectory or surface the conflict. Do not relabel a
burn time, pause a shift, or rewrite an already started campaign epoch to fit.
Duty intervals longer than a watch cannot fit. Inputs are finite, nonnegative,
positive-duration intervals; invalid/default intervals and unbounded input lists
are rejected. End-at-watch-boundary matches the existing CoversBurn contract.

This does not add a trajectory solver or UI. Basil's navigation integration must
build the intervals from a reviewed immutable trajectory snapshot before the
owner starts the campaign, then revalidate the selected epoch and actual burn
windows at dispatch. The model result is not burn approval, cryopod readiness,
a wake command, a warp permission or a guarantee of multiplayer authority.
If the physical plan changes, recompute/review; preserve separate Hamish/Basil
checks. An active campaign's actual epoch remains immutable.

Production tests compare 20,100 candidate epochs with the existing independent
CoversBurn predicate, and check returned endpoints, conflicts, long burns and
invalid inputs. Full KSP compile passes. No live gameplay or actual Duna route
is qualified by these schedule-only checks.
