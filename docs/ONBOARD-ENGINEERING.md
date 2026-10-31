# Onboard engineering controls

The engineering panel operates individual stock deployable solar panels and
radiators on the active ship. Open it from flight or IVA with **Ctrl+Alt+E**
(Control+Option+E on Mac), or the **ENGINEERING** button near the bottom left.
Select an Engineer from the vessel's actual crew roster; the shared duty query
must also confirm eligibility at dispatch. The panel is available
only in saves whose names begin `MegaCrew`, and excludes EVA vessels.

Each device shows its part title, flight ID and deployment state. **Deploy** and
**Stow** invoke the stock module's active Extend/Retract events. Fixed panels and
non-retractable or broken hardware retain stock restrictions. Buttons do not
force an animation or invent a repaired state. Watch device clearance before
operating it. The existing practice objective still observes actual deployment.

The panel includes total vessel ElectricCharge. It does not predict net power,
cooling capacity or clearance. It can be used in IVA without opening an external
part menu, but is a screen overlay rather than a physical clickable interior prop.
It consults the campaign's awake/fit/injury policy for the selected Engineer,
without claiming which player occupies a station. It has no multiplayer authority
or networking and must not be used as proof of synchronized LMP engineering.

## Build and verify

`tools/build.py` automatically includes `Source/OnboardEngineering.cs`. The changed
DLL compiled successfully against local KSP 1.12.5 assemblies. No in-game control
or objective pass has been observed for this change yet. Restart the isolated test
copy after installing the new DLL; do not replace a running assembly.

In the isolated practice save:

1. Open the panel in IVA with an engineer aboard. Confirm all deployable arrays
   and radiators appear, with IDs that distinguish matching parts.
2. Deploy two arrays and a radiator individually. Confirm visible hardware changes
   and the existing practice deployment objective advances without part menus.
3. Retract an eligible device. Confirm state tracks the animation; fixed/non-
   retractable/broken modules must retain stock limitations.
4. Change active vessel; verify controls affect only its devices. Open a non-
   MegaCrew save and verify this panel is unavailable.
5. Remove the engineer in a disposable test and confirm commands disable.
6. While hovering over the panel, verify clicks do not fire ship controls. Move
   away, close the panel, switch scenes and confirm normal controls return.

The build is a review candidate pending these runtime checks. The existing ZIP
continues to contain 0.2.0 without the new panel; use the checked-out source/DLL
for this candidate. Do not extract the old ZIP over this checkout after building.


The current source requires explicit Engineer selection and the shared 0.3.6
crew-duty query immediately before deploy/stow dispatch. Missing or denied
shared readiness blocks the command without choosing another operator. See
[DUTY-GATED-STATIONS.md](DUTY-GATED-STATIONS.md) for current scope and live gates.
