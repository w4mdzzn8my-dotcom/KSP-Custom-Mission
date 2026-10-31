# Pre-start epoch planner integration review

## Source review

The additive `Hermes-Epoch-Planner.patch` at commit `591ca74` was reviewed against `CampaignTiming.CoversBurn` from cumulative 0.3.5 (`4e86cc0`). Let `[a,b]` be the exact duty interval: `a = ignitionUT - alignSeconds`, `b = ignitionUT + burnSeconds + verifySeconds`. `CoversBurn` chooses the watch containing `a` using `floor((a-epoch)/WatchSeconds)`, accepts human watch indexes 0, 2, 4, and requires `b <= epoch + (h+1)W`. Rearranging gives `b-(h+1)W <= epoch <= a-hW`, which is exactly the planner's inclusive interval. No endpoint mismatch found.

The feasibility intersection does not alter solver ignition epochs. It rejects invalid/default duty intervals and counts over 128. Returned candidate ranges are merged only when overlapping/touching, which preserves their union. Existing issue #1 comment asks that the patch be additive after 0.3.5, preserve physical burn UTs, retain separate Hamish/Basil approval, avoid active campaign epoch rebinding or UT changes, and revalidate after plan changes.

## Integration requirements

- Build each duty interval from the immutable physical plan, preserving the solver's actual ignition `BurnUT`; include complete pre-alignment and post-burn verification durations and a defensible burn-duration estimate.
- Bind the preview to plan revision, vessel, reference body, and relevant orbit/trajectory snapshot. Recompute after any plan, vessel/body, or trajectory change.
- Any future adapter must read authoritative campaign-start state at both selection and dispatch, and reject an already-started campaign. It must provide no path that rewrites an active epoch.
- Existing campaign start sets the active epoch to current `Planetarium.GetUniversalTime()`, and `WatchAt` rejects a current UT earlier than the stored epoch. Therefore a future selected candidate is only a pre-start proposal. At dispatch, recompute feasibility with current UT as the earliest permitted epoch and set the active campaign epoch once to the actual current UT. If the fixed physical burns are no longer covered, reject/review another trajectory; do not move the burns or write a future active epoch.
- Keep two distinct human approvals bound to the same unchanged plan revision, selected epoch, vessel/body identity, and coverage result. The adapter itself has no approval/warp/burn authority.
- The planner validates schedule only. It cannot establish route feasibility, node creation, actual propulsion, fuel, power, background operation, or maneuver success.


## Delivery status

This is a source review of the upstream helper and a contract for its future
navigation adapter. It does not add a production UI, activate a campaign, bind
a real trajectory or establish two-human approval. The temporary adapter
experiment has not been integrated into the mod. The existing planner's tests
remain schedule-model evidence, with flight/trajectory acceptance still pending.
