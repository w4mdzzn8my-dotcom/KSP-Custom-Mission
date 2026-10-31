using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MegaCrew
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class OnboardOperations : MonoBehaviour
    {
        const string Lock = "MegaCrew.OnboardOperations";
        readonly string[] resources = { "ElectricCharge", "Food", "Water", "Glykerol" };
        readonly Dictionary<string, ResourceTrend> trends = new Dictionary<string, ResourceTrend>();
        readonly List<string> log = new List<string>();
        readonly OnboardRendezvous rendezvous = new OnboardRendezvous();
        readonly OnboardScienceObservation scienceObservation = new OnboardScienceObservation();
        readonly OnboardPowerSurvey powerSurvey = new OnboardPowerSurvey();
        Rect window = new Rect(610, 100, 590, 560);
        Vector2 scroll;
        bool visible;
        int tab;
        string selectedPilot = "", selectedScientist = "";
        Guid operatorVesselId;
        string operatorSave = "";
        Guid vesselId;
        string save = "", status = "", loadedPath = "";
        double nextSample;
        bool Allowed { get { return HighLogic.LoadedSceneIsFlight && FlightGlobals.ActiveVessel != null && !FlightGlobals.ActiveVessel.isEVA && (HighLogic.SaveFolder ?? "").StartsWith("MegaCrew", StringComparison.OrdinalIgnoreCase); } }
        Vessel Ship { get { return FlightGlobals.ActiveVessel; } }
        string PathName { get { return Path.Combine(KSPUtil.ApplicationRootPath, "saves", HighLogic.SaveFolder, "MegaCrewOperations.cfg"); } }
        void Update()
        {
            if (!Allowed) { visible = false; rendezvous.Reset(); scienceObservation.Reset(); powerSurvey.Abort("Survey stopped because the active mission vessel became unavailable."); InputLockManager.RemoveControlLock(Lock); return; }
            if (save != HighLogic.SaveFolder || vesselId != Ship.id)
            {
                save = HighLogic.SaveFolder; vesselId = Ship.id; trends.Clear(); nextSample = 0;
                if (operatorVesselId != Ship.id || operatorSave != HighLogic.SaveFolder) { operatorVesselId = Ship.id; operatorSave = HighLogic.SaveFolder ?? ""; selectedPilot = selectedScientist = ""; }
                foreach (var name in resources) trends[name] = new ResourceTrend();
                if (loadedPath != PathName) { log.Clear(); loadedPath = PathName; LoadLog(); }
            }
            if (Input.GetKeyDown(KeyCode.O) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))) visible = !visible;
            double now = Planetarium.GetUniversalTime();
            rendezvous.Update(Ship, save, now, FlightDriver.Pause, TimeWarp.CurrentRateIndex != 0);
            scienceObservation.UpdateContext(Ship, save, HighLogic.CurrentGame, now);
            powerSurvey.Update(Ship, save, HighLogic.CurrentGame, now, FlightDriver.Pause, TimeWarp.CurrentRateIndex != 0 || TimeWarp.CurrentRate != 1.0f);
            if (now < nextSample - 2) { foreach (var trend in trends.Values) trend.Reset(); nextSample = now; }
            if (now >= nextSample)
            {
                foreach (var name in resources) { double amount, cap; Resource(name, out amount, out cap); trends[name].Sample(now, amount); }
                nextSample = now + 2;
            }
            if (!visible) InputLockManager.RemoveControlLock(Lock);
        }
        void OnDestroy() { InputLockManager.RemoveControlLock(Lock); }
        bool HasRole(Vessel vessel, string name, string role)
        {
            if (vessel == null || String.IsNullOrWhiteSpace(name)) return false;
            foreach (var member in vessel.GetVesselCrew())
                if (member != null && member.name == name && member.experienceTrait != null && member.experienceTrait.TypeName == role) return true;
            return false;
        }
        bool DutyReady(Vessel vessel, string name, string role, out string reason)
        {
            reason = "";
            if (!Allowed || vessel == null || vessel != FlightGlobals.ActiveVessel) reason = "Active mission vessel changed.";
            else if (operatorVesselId != vessel.id || operatorSave != HighLogic.SaveFolder) reason = "Operator selection belongs to a different vessel or save.";
            else if (FlightDriver.Pause) reason = "Duty controls are unavailable while paused.";
            else if (TimeWarp.CurrentRateIndex != 0) reason = "Duty controls are unavailable during time warp.";
            else if (!HasRole(vessel, name, role)) reason = "The selected " + role.ToLowerInvariant() + " is no longer aboard with that trait.";
            if (reason.Length != 0) return false;
            return DutyReadinessBridge.CanPerformDuty(name, vessel.id, out reason);
        }
        bool OperatorPicker(Vessel vessel, string role, ref string selected)
        {
            if (operatorVesselId != vessel.id || operatorSave != HighLogic.SaveFolder) { operatorVesselId = vessel.id; operatorSave = HighLogic.SaveFolder ?? ""; selectedPilot = selectedScientist = ""; }
            GUILayout.Label("Selected " + role + ": " + (String.IsNullOrEmpty(selected) ? "none" : selected));
            string reason;
            bool ready = DutyReady(vessel, selected, role, out reason);
            GUILayout.Label(ready ? "Duty readiness: available." : "Duty readiness: " + reason);
            foreach (var member in vessel.GetVesselCrew())
                if (member != null && member.experienceTrait != null && member.experienceTrait.TypeName == role)
                    if (GUILayout.Button((member.name == selected ? "Selected: " : "Select: ") + member.name))
                    {
                        if (vessel != FlightGlobals.ActiveVessel || !Allowed) status = "Selection refused: active vessel changed.";
                        else { selected = member.name; status = "Selected " + role + ": " + selected; }
                    }
            if (GUILayout.Button("Clear " + role + " selection")) selected = "";
            return ready;
        }
        void Resource(string name, out double amount, out double cap)
        {
            amount = cap = 0;
            foreach (var part in Ship.parts) foreach (PartResource r in part.Resources) if (r.resourceName == name) { amount += r.amount; cap += r.maxAmount; }
        }
        void OnGUI()
        {
            if (!Allowed) return;
            GUI.skin = HighLogic.Skin;
            if (GUI.Button(new Rect(375, Screen.height - 95, 190, 34), "OPERATIONS / Ctrl+Alt+O")) visible = !visible;
            if (!visible) return;
            window.x = Mathf.Clamp(window.x, 0, Math.Max(0, Screen.width - window.width));
            window.y = Mathf.Clamp(window.y, 0, Math.Max(0, Screen.height - 100));
            window = GUILayout.Window(719316, window, Draw, "HERMES / ONBOARD OPERATIONS", GUILayout.Width(590));
            if (window.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y))) InputLockManager.SetControlLock(ControlTypes.ALL_SHIP_CONTROLS, Lock);
            else InputLockManager.RemoveControlLock(Lock);
        }
        void Draw(int id)
        {
            if (GUILayout.Button("Close")) visible = false;
            tab = GUILayout.Toolbar(tab, new[] { "Pilot", "Rendezvous", "Science", "Supplies", "Cryo checks", "Log" });
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(410));
            GUILayout.Label(Ship.vesselName + " | " + Ship.mainBody.bodyName + " | " + Ship.situation);
            if (tab == 0) Pilot(); else if (tab == 1) rendezvous.Draw(Ship); else if (tab == 2) Science(); else if (tab == 3) Supplies(); else if (tab == 4) Cryo(); else Journal();
            GUILayout.EndScrollView();
            GUILayout.Label(status);
            GUILayout.Label("Selected crew and shared duty readiness govern station commands. Physical room occupancy and player identity remain separate.");
            GUI.DragWindow(new Rect(0, 0, 10000, 24));
        }
        void Pilot()
        {
            var v = Ship; var o = v.orbit;
            GUILayout.Label("Altitude " + v.altitude.ToString("F0") + " m | surface speed " + v.srfSpeed.ToString("F1") + " m/s | vertical " + v.verticalSpeed.ToString("F1") + " m/s");
            GUILayout.Label("Pe " + (o.PeA / 1000).ToString("F1") + " km | Ap " + (o.ApA / 1000).ToString("F1") + " km | inclination " + o.inclination.ToString("F2") + " deg");
            GUILayout.Label("Time to Pe " + o.timeToPe.ToString("F0") + " s | time to Ap " + o.timeToAp.ToString("F0") + " s");
            bool pilotReady = OperatorPicker(v, "Pilot", ref selectedPilot);
            Vessel commandVessel = v;
            string pilotName = selectedPilot;
            bool sasHardware = v.ActionGroups != null && v.Autopilot != null;
            GUI.enabled = pilotReady && sasHardware;
            if (GUILayout.Button(sasHardware && v.ActionGroups[KSPActionGroup.SAS] ? "Disable SAS" : "Enable SAS"))
            {
                string reason;
                if (selectedPilot != pilotName || commandVessel != FlightGlobals.ActiveVessel || !Allowed || FlightDriver.Pause || TimeWarp.CurrentRateIndex != 0 || commandVessel.ActionGroups == null || commandVessel.Autopilot == null) status = "SAS command refused: selection, vessel, context or control hardware changed.";
                else if (!DutyReady(commandVessel, pilotName, "Pilot", out reason)) status = "SAS command refused: " + reason;
                else { commandVessel.ActionGroups.ToggleGroup(KSPActionGroup.SAS); Record("Pilot " + pilotName + " toggled SAS"); }
            }
            foreach (var mode in new[] { VesselAutopilot.AutopilotMode.StabilityAssist, VesselAutopilot.AutopilotMode.Prograde, VesselAutopilot.AutopilotMode.Retrograde, VesselAutopilot.AutopilotMode.Normal, VesselAutopilot.AutopilotMode.Antinormal, VesselAutopilot.AutopilotMode.RadialIn, VesselAutopilot.AutopilotMode.RadialOut, VesselAutopilot.AutopilotMode.Maneuver })
            {
                bool modeAvailable = v.ActionGroups != null && v.Autopilot != null && v.ActionGroups[KSPActionGroup.SAS] && v.Autopilot.CanSetMode(mode);
                GUI.enabled = pilotReady && modeAvailable;
                if (GUILayout.Button("SAS: " + mode))
                {
                    string reason;
                    if (selectedPilot != pilotName || commandVessel != FlightGlobals.ActiveVessel || !Allowed || FlightDriver.Pause || TimeWarp.CurrentRateIndex != 0 || commandVessel.ActionGroups == null || commandVessel.Autopilot == null || !commandVessel.ActionGroups[KSPActionGroup.SAS] || !commandVessel.Autopilot.CanSetMode(mode)) status = "SAS mode refused: selection, vessel, context or control hardware changed.";
                    else if (!DutyReady(commandVessel, pilotName, "Pilot", out reason)) status = "SAS mode refused: " + reason;
                    else { commandVessel.Autopilot.SetMode(mode); Record("Pilot " + pilotName + " set SAS mode " + mode); }
                }
            }
            GUI.enabled = true;
            if (v.patchedConicSolver != null)
                foreach (var node in v.patchedConicSolver.maneuverNodes) GUILayout.Label("Planned burn: T" + (node.UT - Planetarium.GetUniversalTime()).ToString("+0;-0;0") + " s | delta-v " + node.DeltaV.magnitude.ToString("F1") + " m/s");
            GUILayout.Label("Existing maneuver nodes are read only. No burn, staging or trajectory planning is automated.");
        }
        void Science()
        {
            Vessel scienceVessel = Ship;
            bool scientistReady = OperatorPicker(scienceVessel, "Scientist", ref selectedScientist);
            string scientistName = selectedScientist;
            bool found = false;
            foreach (var part in Ship.parts) foreach (PartModule module in part.Modules)
            {
                string partTitle = part.partInfo == null ? "Unknown part" : (part.partInfo.title ?? "Unknown part");
                var container = module as IScienceDataContainer;
                ScienceData[] stored = null;
                bool inventoryAvailable = container != null;
                if (container != null)
                {
                    try
                    {
                        stored = container.GetData();
                        if (stored != null)
                            foreach (ScienceData data in stored)
                            {
                                if (data == null) { GUILayout.Label("Stored report unavailable on " + partTitle + "."); continue; }
                                GUILayout.Label("Reported inventory: " + partTitle + " #" + part.flightID + " | " + data.title + " | " + data.dataAmount.ToString("G6") + " data units");
                            }
                    }
                    catch (Exception ex)
                    {
                        inventoryAvailable = false;
                        GUILayout.Label("Stored reports unavailable on " + partTitle + ".");
                        Debug.LogWarning("[MegaCrew Science] " + module.GetType().Name + ": " + ex.Message);
                    }
                }
                var experiment = module as ModuleScienceExperiment;
                if (experiment != null)
                {
                    found = true;
                    GUILayout.Label(partTitle + " #" + part.flightID + " | " + experiment.experimentID + " | stored results " + (inventoryAvailable ? (stored == null ? "0" : stored.Length.ToString()) : "unavailable"));
                    bool experimentAvailable = !experiment.Inoperable && !experiment.Deployed;
                    GUI.enabled = scientistReady && experimentAvailable;
                    if (GUILayout.Button("Run experiment"))
                    {
                        string reason;
                        if (selectedScientist != scientistName || scienceVessel != FlightGlobals.ActiveVessel || !Allowed || FlightDriver.Pause || TimeWarp.CurrentRateIndex != 0 || experiment.vessel != scienceVessel || experiment.Inoperable || experiment.Deployed) status = "Experiment refused: selection, vessel, context or experiment state changed.";
                        else if (!DutyReady(scienceVessel, scientistName, "Scientist", out reason)) status = "Experiment refused: " + reason;
                        else
                        {
                            try { experiment.DeployExperiment(); Record("Scientist " + scientistName + " requested: " + experiment.experimentID); }
                            catch (Exception ex) { status = "Experiment refused: " + ex.Message; }
                        }
                    }
                    GUI.enabled = true;
                }
            }
            if (!found) GUILayout.Label("Fit a stock science experiment to this vessel to operate it here.");
            GUILayout.Label("Reported inventory lists currently retained reports by container. Repeated views do not necessarily represent new progress or unique reports.");
            scienceObservation.Draw(Ship, HighLogic.SaveFolder, HighLogic.CurrentGame, Planetarium.GetUniversalTime(), FlightDriver.Pause, TimeWarp.CurrentRateIndex != 0);
            GUILayout.Label("Run uses the stock experiment and its result dialog. No science points are fabricated; results are never automatically discarded or transmitted.");
        }
        void Supplies()
        {
            foreach (var name in resources)
            {
                double amount, cap; Resource(name, out amount, out cap);
                GUILayout.Label(name + ": " + amount.ToString("F2") + " / " + cap.ToString("F2"));
                ResourceTrend trend;
                if (trends.TryGetValue(name, out trend) && trend.Ready)
                {
                    double seconds = trend.SecondsRemaining(amount);
                    GUILayout.Label("Recent net rate " + trend.Rate.ToString("+0.000;-0.000;0") + "/s | " + (double.IsInfinity(seconds) ? "no measured depletion" : "at this rate: " + (seconds / 60).ToString("F1") + " min remaining"));
                }
                else GUILayout.Label("Collecting trend samples...");
                if (cap > 0 && amount / cap < .2) GUILayout.Label("LOW RESERVE: " + name);
            }
            GUILayout.Label("Rates use two-second samples. Sunlight, equipment, meals and freeze/thaw change demand. These estimates are not guaranteed endurance.");
            powerSurvey.Draw(Ship, HighLogic.SaveFolder, HighLogic.CurrentGame, Planetarium.GetUniversalTime(), FlightDriver.Pause, TimeWarp.CurrentRateIndex != 0 || TimeWarp.CurrentRate != 1.0f);
        }
        void Cryo()
        {
            int awake = Ship.GetVesselCrew().Count;
            double amount, cap; Resource("ElectricCharge", out amount, out cap);
            GUILayout.Label((Ship.situation == Vessel.Situations.ORBITING ? "PASS" : "CHECK") + " | vessel orbiting (does not guarantee a safe trajectory)");
            GUILayout.Label((awake >= 2 ? "PASS" : "CHECK") + " | at least two roster crew before freezing: " + awake);
            GUILayout.Label((cap > 0 && amount / cap >= .2 ? "PASS" : "CHECK") + " | charge reserve above 20%");
            Resource("Glykerol", out amount, out cap); GUILayout.Label("Glykerol aboard: " + amount.ToString("F2") + " (pod costs must be checked separately)");
            GUILayout.Label("DeepFreeze: " + CryoBridge.Status);
            GUILayout.Label("Confirm no pending burn, adequate cooling, pod-local resources and an awake relief watch. Use the cryobay I panel to freeze/thaw real crew. This checklist does not grant clearance or advance time.");
            if (GUILayout.Button("Log readiness inspection (manual sign-off)")) Record("Crew manually recorded cryosleep readiness inspection; not an automatic safety certification");
        }
        void Journal()
        {
            if (GUILayout.Button("Log current orbit and reserves")) Record("Survey: " + Ship.mainBody.bodyName + "; Pe " + Ship.orbit.PeA.ToString("F0") + " m; Ap " + Ship.orbit.ApA.ToString("F0") + " m");
            for (int i = log.Count - 1; i >= 0; i--) GUILayout.Label(log[i]);
            GUILayout.Label("Last 100 entries persist per save in MegaCrewOperations.cfg; stock vessel state remains in the game save.");
        }
        void Record(string text)
        {
            if (!Allowed) return;
            string entry = "UT " + Planetarium.GetUniversalTime().ToString("F0") + " | " + Ship.vesselName + " | " + text;
            log.Add(entry); if (log.Count > 100) log.RemoveAt(0);
            try { var node = new ConfigNode("MEGACREW_OPERATIONS"); foreach (var line in log) node.AddValue("entry", line); node.Save(PathName); status = text; }
            catch (Exception ex) { status = "Action recorded in memory; log save failed."; Debug.LogError("[MegaCrew Operations] " + ex); }
        }
        void LoadLog()
        {
            try { if (!File.Exists(PathName)) return; var node = ConfigNode.Load(PathName); if (node == null) return; log.AddRange(node.GetValues("entry")); while (log.Count > 100) log.RemoveAt(0); }
            catch (Exception ex) { status = "Could not read operations log."; Debug.LogError("[MegaCrew Operations] " + ex); }
        }
    }
}
