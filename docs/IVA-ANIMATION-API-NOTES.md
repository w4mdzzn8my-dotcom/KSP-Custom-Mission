# IVA Kerbal animation API findings

**Result:** the local KSP 1.12.5 managed assembly exposes a way to identify the
live Kerbal model attached to an internal seat and a generic Unity `Animator[]`
on that model. It does not expose a KSP gesture request/play API for a seated
IVA crew member. No supported, verified path was found to play a named
handover gesture and then restore the prior animation safely. Do not wire a
gesture to the EVA animation manager or call `InternalSeat.SpawnCrew()`/
`DespawnCrew()` as a workaround.

## Confirmed KSP 1.12.5 members

These names and behaviors were checked by reading metadata/decompilation of
`KSP.app/Contents/Resources/Data/Managed/Assembly-CSharp.dll` from the local
KSP 1.12.5 install. The decompiled output is diagnostic only and is not copied
into this document.

- `InternalModel.seats` is a `List<InternalSeat>`. A seat exposes `crew`,
  `taken`, `seatTransform` and `kerbalRef`.
- `InternalSeat.SpawnCrew()` calls `ProtoCrewMember.Spawn(crew)`, parents the
  model under `seatTransform` and assigns it to `kerbalRef`. It creates a visual
  object when called; it is not a read-only lookup. `DespawnCrew()` destroys
  that object.
- `Kerbal` exposes `protoCrewMember`, `InPart`, `eyeTransform`, `headTransform`
  and `Animators` (`Animator[]`). The class has no public `PlayGesture` or
  equivalent method. Its animation component references are generic Unity
  animators; the KSP assembly does not document an animation-state contract for
  seated NPCs.
- `KerbalAnimationManager` provides states such as `idle_a`, `controlpanel_a`,
  `rockSample` and `weld`, but its public `Start(KerbalEVA evaController)` binds
  them to a `KerbalEVA` legacy `Animation` component. `KerbalEVA` is the EVA
  character controller, not the `Kerbal` object parented into an `InternalSeat`.
  This API is not evidence that those animations can be requested for a seated
  IVA NPC.

The published [KSP 1.12.4 `InternalSeat` reference](https://kspmoddinglibs.github.io/KSPDocsSite/class_internal_seat.html)
lists `crew`, `kerbalRef`, `seatTransform`, `SpawnCrew()` and `DespawnCrew()`.
The [Kerbal reference](https://kspmoddinglibs.github.io/KSPDocsSite/class_kerbal.html)
documents the model/crew-facing members and its animation update methods as
protected. The [KerbalAnimationManager reference](https://kspmoddinglibs.github.io/KSPDocsSite/class_kerbal_animation_manager.html)
shows its `KerbalEVA` parameter and EVA state list. The local 1.12.5 assembly
was checked directly because the available API site identifies itself as 1.12.4.

## Clip-name evidence is not a binding

Strings in the local Unity asset files include IVA animation asset names such
as `idle_animA` through `idle_animK`, `idle_animJ_yo` and `true_noddingA`.
`wavesToOtherKerbal` also appears as an Animator state string. This establishes
that the game assets contain animation/Animator names, not that any one of
these names is a playable state on the particular `Animator` returned by a
seated crew member. Some strings are in mission-control character assets. Do
not treat an asset string as a crew gesture API or map it to every IVA model.

Unity's `Animator.Play` is a generic engine operation, not a KSP crew API. It
would bypass any KSP controller policy and may compete with idle, expression,
portrait or IVA updates. A clip name alone cannot establish a safe prior state
to restore. Avoid using it for production handovers until a disposable runtime
probe proves a specific seated model's controller, state, transition duration,
restoration behavior and scene/lifecycle behavior.

## FreeIVA boundary

The installed copy reports FreeIVA **0.2.20.2**. Its pinned upstream
[0.2.20.2 release source](https://github.com/FirstPersonKSP/FreeIva/tree/c013cb43ae19a8e0486cf472a68287d18f2f85f4/FreeIva)
uses `InternalSeat.kerbalRef` for its buckling and movement flow. Its
`KerbalIvaAddon` manages the active IVA camera Kerbal, unbuckling, movement and
seat return; it does not provide a seated-NPC gesture catalog or play request.
The pinned [license](https://github.com/FirstPersonKSP/FreeIva/blob/c013cb43ae19a8e0486cf472a68287d18f2f85f4/LICENSE)
is GPL-2.0, while its README says bundled sound, model and texture assets are
All Rights Reserved. This research only cites the source; it does not copy or
redistribute FreeIVA code or assets.

## Safe next probe

Use a disposable save and a read-only catalog first. Resolve an already-awake
seat by exact `InternalSeat.crew.name` and matching
`InternalSeat.kerbalRef.protoCrewMember`/seat association; require the same
active vessel, internal model and seat references on every sample. Never call
`SpawnCrew`, `DespawnCrew`, change `seatIdx`, alter `taken`, change camera mode,
or access private fields. Inspect only public `Kerbal.Animators`, each
animator's enabled/controller identity, `runtimeAnimatorController.animationClips`
and current animator state. Record the exact seat/crew/model/controller and
clip/state names seen. Do not call `Play`, `CrossFade` or change parameters in
this catalog pass.

Only if the runtime catalog finds a state clearly associated with a gesture on
the selected seated model should a separate opt-in disposable QA test consider
playing it. That later test must check before/after pose, prior-state restoration,
target/camera/vessel changes, part/scene destruction and KSP update interference.
No gesture runtime test has been run for this research, and no animation is
qualified for handover use.
