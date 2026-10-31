# Hermes acceptance gates — work remains incomplete

This is an evidence map for the full owner request, not a reduced definition of
completion. Source/model checks never substitute for gameplay verification.

| Requirement | Current evidence | Remaining acceptance |
| --- | --- | --- |
| Separate scientific Hermes KSP setup | KSP 1.12.5 clone; pinned SSPX/IVAs, FreeIVA, DeepFreeze, EVS and EVE/BoulderCo installed; original trainer previously loaded into flight | Current candidate startup/load; stability, traversable rooms, power/thermal and recovery qualification |
| Orbital practice and usable interiors | Existing trainer flight log; room-based controls and tasks compile | Complete end-to-end maintenance practice, input locks, room/crew gating, actual physical interactions |
| Food, water, ordinary sleep, hygiene, exercise, social care, injuries | Finite supply adapter, needs/care, daily routine and crew snapshot code; automated tests | Current gameplay activities, real resource/save/revert behavior, animations and balanced workload |
| 24-month human/NPC watches | Actual UT scenario clock; 540x/172800x target math and six boundaries tested | Actual custom warp rates, braking, encounters, daily coverage and complete expedition execution |
| Genuine cryosleep and wake handovers | Actual DeepFreeze bridge; frozen-state gates compile | Full sequential handover, pod power/temperature, resources, background/eclipse behavior and thaw safety |
| Finite NPC upkeep and busy human watches | Resource-accounted daily crew routine candidate | Substantial varied multi-step maintenance, durable work log and NPC maintenance without free repairs |
| Exactly one undisclosed planned emergency | Requirement recorded; normal care/training distinguished | Persistent event lifecycle, single issuance/resolution, integration test without spoiling timing |
| Programmed burns with two checks | Basil draft #8 numerical/approval kernels reviewed; bounded numerical tests pass | Full trajectory/node planner, runtime frame/patched-conic verification, awake scheduling, actual fuel burn, immutable approvals and failure handling |
| No shift-clock pause during burns | UT-based clock and timing arithmetic tests | Live burn-through-watch test and accurate play-time estimate from validated plan |
| Lander storage and recovery | Basil draft #6 loader/structural evidence | Resolve fixture/part-retention ambiguity; physical clearance, undock, landing, EVA, ascent/redock, save/reload |
| Planet graphics | Host EVE/BoulderCo installed; prior startup initialization | Current flight/surface rendering and Mac performance; no unqualified prototype profile assumed portable |
| Luna multiplayer after single player | Existing reference 0.29.2 and server fix preserved; not in MegaCrew clone | Authoritative shared custom state, distinct Hamish/Basil principals, same Sandbox with shared vessel/Kerbal observed by both clients |
| Custom cooperative story campaign | Story workshop #7 and feature requests #9 read; base proposal available | Authored playable story system, locations, dialogue, scripted/animated handoffs and onboarding; no sabotage/murders |
| Publish/coordinate with Basil | Source packets through 0.3.3; concise #1 coordination | Reviewed source integration, current documented package, reproducible setup on both Macs and final joint verification |

Preserve original saves, server Universe, IPv4 fix and backup. Do not expose
credentials. Draft PRs remain unmerged until their gates pass. Current runtime
work is deferred while the Mac is locked; numerical/source/integration work can
continue independently. Full completion is not established.
