using System;
using UnityEngine;

namespace MegaCrew
{
    // Separate addon: no changes to the trainer's objective or medical state.
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class OnboardEngineering : MonoBehaviour
    {
        bool visible;
        Rect window = new Rect(40, 100, 560, 480);
        Vector2 scroll;
        string notice = "Select an individual array or radiator to operate it.";
        string selectedEngineer = "";
        Guid selectionVesselId;
        string selectionSave = "";
        const string LockName = "MegaCrew.OnboardEngineering";

        bool Mission(Vessel vessel)
        {
            return HighLogic.LoadedSceneIsFlight && vessel != null && !vessel.isEVA &&
                (HighLogic.SaveFolder ?? "").StartsWith("MegaCrew", StringComparison.OrdinalIgnoreCase);
        }

        bool HasRole(Vessel vessel, string name, string role)
        {
            if (vessel == null || String.IsNullOrWhiteSpace(name)) return false;
            foreach (var member in vessel.GetVesselCrew())
                if (member != null && member.name == name && member.experienceTrait != null && member.experienceTrait.TypeName == role) return true;
            return false;
        }

        bool ContextReady(Vessel vessel, out string reason)
        {
            reason = "";
            if (!Mission(vessel) || vessel != FlightGlobals.ActiveVessel || selectionVesselId != vessel.id || selectionSave != HighLogic.SaveFolder) reason = "Active mission vessel or save changed.";
            else if (FlightDriver.Pause) reason = "Duty controls are unavailable while paused.";
            else if (TimeWarp.CurrentRateIndex != 0) reason = "Duty controls are unavailable during time warp.";
            return reason.Length == 0;
        }

        bool DutyReady(Vessel vessel, string name, string role, out string reason)
        {
            if (!ContextReady(vessel, out reason)) return false;
            if (!HasRole(vessel, name, role)) { reason = "The selected " + role.ToLowerInvariant() + " is no longer aboard with that trait."; return false; }
            return DutyReadinessBridge.CanPerformDuty(name, vessel.id, out reason);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.E) &&
                (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) &&
                (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))) visible = !visible;
            var vessel = FlightGlobals.ActiveVessel;
            if (!Mission(vessel)) visible = false;
            if (selectionVesselId != Guid.Empty && (vessel == null || selectionVesselId != vessel.id))
            {
                selectionVesselId = vessel == null ? Guid.Empty : vessel.id;
                selectedEngineer = "";
                selectionSave = HighLogic.SaveFolder ?? "";
            }
            if (!visible) InputLockManager.RemoveControlLock(LockName);
        }

        void OnDestroy() { InputLockManager.RemoveControlLock(LockName); }

        void OnGUI()
        {
            if (!Mission(FlightGlobals.ActiveVessel)) return;
            GUI.skin = HighLogic.Skin;
            if (GUI.Button(new Rect(180, Screen.height - 95, 190, 34), "ENGINEERING / Ctrl+Alt+E")) visible = !visible;
            if (!visible) return;
            window.x = Mathf.Clamp(window.x, 0, Math.Max(0, Screen.width - window.width));
            window.y = Mathf.Clamp(window.y, 0, Math.Max(0, Screen.height - 100));
            window = GUILayout.Window(719315, window, Draw, "ONBOARD ENGINEERING", GUILayout.Width(560));
            var mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if (window.Contains(mouse)) InputLockManager.SetControlLock(ControlTypes.ALL_SHIP_CONTROLS, LockName);
            else InputLockManager.RemoveControlLock(LockName);
        }

        void Draw(int id)
        {
            if (GUILayout.Button("Close")) visible = false;
            var vessel = FlightGlobals.ActiveVessel;
            if (!Mission(vessel)) return;
            if (selectionVesselId != vessel.id || selectionSave != HighLogic.SaveFolder) { selectionVesselId = vessel.id; selectionSave = HighLogic.SaveFolder ?? ""; selectedEngineer = ""; }
            GUILayout.Label(vessel.vesselName + " | power and cooling");
            bool engineerReady = OperatorPicker(vessel);
            string operatorName = selectedEngineer;
            double ec = 0, capacity = 0;
            foreach (var part in vessel.parts)
                foreach (PartResource resource in part.Resources)
                    if (resource.resourceName == "ElectricCharge") { ec += resource.amount; capacity += resource.maxAmount; }
            GUILayout.Label("ElectricCharge: " + ec.ToString("F0") + " / " + capacity.ToString("F0"));
            GUILayout.Label("Individual controls operate actual hardware; check clearance before deployment.");
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(250));
            int count = 0;
            foreach (var part in vessel.parts)
                foreach (PartModule module in part.Modules)
                {
                    if (module.moduleName != "ModuleDeployableSolarPanel" && module.moduleName != "ModuleDeployableRadiator") continue;
                    count++;
                    GUILayout.BeginVertical(GUI.skin.box);
                    var state = module.GetType().GetField("deployState");
                    GUILayout.Label(part.partInfo.title + " [" + part.flightID + "] | " +
                        (state == null ? "State unavailable" : Convert.ToString(state.GetValue(module))));
                    DrawCommand(vessel, module, "Extend", "Deploy", engineerReady, operatorName);
                    DrawCommand(vessel, module, "Retract", "Stow", engineerReady, operatorName);
                    GUILayout.EndVertical();
                }
            if (count == 0) GUILayout.Label("No supported deployable solar arrays or radiators on this vessel.");
            GUILayout.EndScrollView();
            GUILayout.Label(notice);
            GUILayout.Label("Available in IVA. Local single-player controls; shared LMP authority is not implemented.");
            GUI.DragWindow(new Rect(0, 0, 10000, 24));
        }

        bool OperatorPicker(Vessel vessel)
        {
            GUILayout.Label("Selected Engineer: " + (String.IsNullOrEmpty(selectedEngineer) ? "none" : selectedEngineer));
            string reason;
            bool ready = DutyReady(vessel, selectedEngineer, "Engineer", out reason);
            GUILayout.Label(ready ? "Duty readiness: available." : "Duty readiness: " + reason);
            foreach (var member in vessel.GetVesselCrew())
                if (member != null && member.experienceTrait != null && member.experienceTrait.TypeName == "Engineer")
                    if (GUILayout.Button((member.name == selectedEngineer ? "Selected: " : "Select: ") + member.name))
                    {
                        if (vessel != FlightGlobals.ActiveVessel || !Mission(vessel)) notice = "Selection refused: active vessel changed.";
                        else { selectedEngineer = member.name; notice = "Selected Engineer: " + selectedEngineer; }
                    }
            if (GUILayout.Button("Clear Engineer selection")) selectedEngineer = "";
            return ready;
        }

        void DrawCommand(Vessel vessel, PartModule module, string name, string label, bool engineerReady, string operatorName)
        {
            var command = module == null || module.Events == null ? null : module.Events[name];
            bool enabled = engineerReady && module != null && module.vessel == vessel && command != null && command.active;
            GUI.enabled = enabled;
            if (GUILayout.Button(label))
            {
                string reason;
                // Check captured operator and hardware first; shared live readiness is the final gate.
                if (selectedEngineer != operatorName) notice = "Command refused: selected Engineer changed.";
                else if (vessel != FlightGlobals.ActiveVessel || !Mission(vessel) || FlightDriver.Pause || TimeWarp.CurrentRateIndex != 0 || module == null || module.vessel != vessel || module.Events == null || module.Events[name] == null || !module.Events[name].active)
                    notice = "Command refused: vessel, context or hardware state changed.";
                else if (!DutyReady(vessel, operatorName, "Engineer", out reason)) notice = "Command refused: " + reason;
                else
                {
                    try { module.Events[name].Invoke(); notice = label + " requested. Verify the hardware state above."; }
                    catch (Exception ex) { notice = "Hardware command failed; see KSP.log."; Debug.LogError("[MegaCrew Engineering] " + ex); }
                }
            }
            GUI.enabled = true;
        }
    }
}
