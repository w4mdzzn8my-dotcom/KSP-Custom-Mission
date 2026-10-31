using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MegaCrew
{
    /// <summary>Read-only world-space measurements between two loaded stock docking ports.</summary>
    internal sealed class OnboardDockingInstruments
    {
        const double SampleIntervalSeconds = 0.5;
        const double MaximumClosingGapSeconds = 3.0;

        sealed class PortRef
        {
            public ModuleDockingNode Module;
            public Part Part;
            public int ModuleIndex;
            public string Key;
            public string Label;
        }

        Guid ownVesselId, targetVesselId;
        object gameContext;
        string saveFolder = "", ownStructure = "", targetStructure = "";
        string ownPortKey = "", targetPortKey = "";
        CelestialBody body;
        double nextSampleUT, sampleUT, previousRangeUT, lastObservedUT;
        double rangeMeters, axialMeters, lateralMeters, angleDegrees, closingMetersPerSecond;
        bool hasGeometry, hasPreviousRange, hasClosing, hasObservedUT;
        Vector2 targetScroll, ownPortScroll, targetPortScroll;
        string status = "Choose two loaded same-body docking ports.";

        static bool Finite(double value) { return !Double.IsNaN(value) && !Double.IsInfinity(value); }

        public void Reset()
        {
            ownVesselId = targetVesselId = Guid.Empty;
            gameContext = null;
            saveFolder = ownStructure = targetStructure = "";
            ownPortKey = targetPortKey = "";
            body = null;
            ClearReadings();
            hasObservedUT = false;
            lastObservedUT = 0;
            status = "Choose two loaded same-body docking ports.";
        }

        void ClearReadings()
        {
            hasGeometry = hasPreviousRange = hasClosing = false;
            rangeMeters = axialMeters = lateralMeters = angleDegrees = closingMetersPerSecond = 0;
            sampleUT = previousRangeUT = nextSampleUT = 0;
        }

        void ClearSelection(string message)
        {
            targetVesselId = Guid.Empty;
            ownPortKey = targetPortKey = "";
            ownStructure = targetStructure = "";
            ClearReadings();
            status = message;
        }

        void Invalidate(string message)
        {
            ClearSelection(message);
            ownVesselId = Guid.Empty;
            gameContext = null;
            saveFolder = "";
            body = null;
        }

        public void Update(Vessel active, string save, object game, double now, bool paused, bool warped)
        {
            try
            {
                if (!Finite(now) || now < 0)
                {
                    Invalidate("Universal time is invalid; docking readings and selections cleared.");
                    return;
                }
                if (active == null || active.mainBody == null)
                {
                    Invalidate("Active vessel or body is unavailable.");
                    return;
                }

                if (ownVesselId != active.id || body != active.mainBody || saveFolder != (save ?? "") || !object.ReferenceEquals(gameContext, game))
                {
                    ClearSelection("Vessel, body, save, or game changed; select ports again.");
                    ownVesselId = active.id;
                    body = active.mainBody;
                    saveFolder = save ?? "";
                    gameContext = game;
                    hasObservedUT = false;
                }
                if (hasObservedUT && now < lastObservedUT)
                    ClearSelection("Universal time moved backwards; select ports again.");
                lastObservedUT = now;
                hasObservedUT = true;
                if (paused || warped)
                {
                    ClearSelection(paused ? "Paused; select ports again after resuming." : "Time warp active; select ports again at 1x.");
                    return;
                }
                if (!FlightGlobals.ready)
                {
                    ClearReadings();
                    status = "Flight vessel data is not ready; waiting before reading ports.";
                    return;
                }
                if (!active.loaded || active.packed)
                {
                    ClearSelection("Active vessel is packed or unloaded; select ports when loaded and unpacked.");
                    return;
                }

                string currentOwnStructure = StructureSignature(active);
                if (ownStructure.Length != 0 && ownStructure != currentOwnStructure)
                    ClearSelection("Active vessel parts changed; select ports again.");
                ownStructure = currentOwnStructure;

                if (targetVesselId == Guid.Empty || ownPortKey.Length == 0 || targetPortKey.Length == 0)
                    return;

                Vessel target = FindTarget(active, targetVesselId);
                if (target == null || target.mainBody != active.mainBody || !target.loaded || target.packed)
                {
                    ClearSelection("Target vessel changed, left this body, or became packed/unloaded; select again.");
                    return;
                }
                string currentTargetStructure = StructureSignature(target);
                if (targetStructure.Length != 0 && targetStructure != currentTargetStructure)
                    ClearSelection("Target vessel parts changed or merged; select ports again.");
                targetStructure = currentTargetStructure;

                if (now < nextSampleUT) return;
                PortRef ownPort = FindPort(active, ownPortKey);
                PortRef targetPort = FindPort(target, targetPortKey);
                if (ownPort == null || targetPort == null)
                {
                    ClearSelection("Selected docking port changed or disappeared; select ports again.");
                    return;
                }
                Transform ownTransform = ownPort.Module.GetTransform();
                Transform targetTransform = targetPort.Module.GetTransform();
                if (ownTransform == null || targetTransform == null)
                    throw new InvalidOperationException("Docking port transform is unavailable.");

                // DPAI's KSP1 port-target implementation uses the ModuleDockingNode
                // ITargetable.GetTransform().forward as the outward port axis.
                var ownObservation = new DockingPortObservation(
                    new DockingGeometryVector(ownTransform.position.x, ownTransform.position.y, ownTransform.position.z),
                    new DockingGeometryVector(ownTransform.forward.x, ownTransform.forward.y, ownTransform.forward.z));
                var targetObservation = new DockingPortObservation(
                    new DockingGeometryVector(targetTransform.position.x, targetTransform.position.y, targetTransform.position.z),
                    new DockingGeometryVector(targetTransform.forward.x, targetTransform.forward.y, targetTransform.forward.z));
                DockingGeometryResult geometry = DockingGeometryModel.Measure(ownObservation, targetObservation);

                if (hasPreviousRange)
                {
                    try
                    {
                        var previous = new DockingRangeSample(previousRangeUT, rangeMeters);
                        var current = new DockingRangeSample(now, geometry.SeparationMeters);
                        closingMetersPerSecond = DockingClosingModel.Measure(previous, current, MaximumClosingGapSeconds).ClosingMetersPerSecond;
                        hasClosing = true;
                    }
                    catch (Exception) { hasClosing = false; }
                }
                rangeMeters = geometry.SeparationMeters;
                axialMeters = geometry.AxialDistanceMeters;
                lateralMeters = geometry.LateralOffsetMeters;
                angleDegrees = geometry.OpposingFaceAngleDegrees;
                previousRangeUT = now;
                hasPreviousRange = true;
                sampleUT = now;
                nextSampleUT = now + SampleIntervalSeconds;
                hasGeometry = true;
                status = "";
            }
            catch (Exception)
            {
                ClearReadings();
                status = "Port snapshot unavailable; readings cleared.";
            }
        }

        static Vessel FindTarget(Vessel active, Guid id)
        {
            if (FlightGlobals.Vessels == null) return null;
            foreach (Vessel candidate in FlightGlobals.Vessels)
                if (candidate != null && candidate != active && candidate.id == id) return candidate;
            return null;
        }

        static string StructureSignature(Vessel vessel)
        {
            if (vessel == null || vessel.parts == null) return "";
            var ids = new List<uint>();
            foreach (Part part in vessel.parts) if (part != null) ids.Add(part.flightID);
            ids.Sort();
            var builder = new StringBuilder();
            for (int i = 0; i < ids.Count; i++) builder.Append(ids[i]).Append(',');
            return builder.ToString();
        }

        static List<PortRef> Ports(Vessel vessel)
        {
            var result = new List<PortRef>();
            if (vessel == null || !vessel.loaded || vessel.packed || vessel.parts == null) return result;
            foreach (Part part in vessel.parts)
            {
                if (part == null || part.Modules == null) continue;
                for (int i = 0; i < part.Modules.Count; i++)
                {
                    ModuleDockingNode module = part.Modules[i] as ModuleDockingNode;
                    if (module == null) continue;
                    string transformName = module.nodeTransformName ?? "";
                    string key = part.flightID + ":" + i + ":" + transformName;
                    string partName = part.partInfo == null ? part.name : part.partInfo.title;
                    string portState = module.state ?? "state unavailable";
                    result.Add(new PortRef { Module = module, Part = part, ModuleIndex = i, Key = key,
                        Label = partName + " [part " + part.flightID + "] / port " + (transformName.Length == 0 ? "unnamed" : transformName) + " / " + portState });
                }
            }
            result.Sort(delegate(PortRef a, PortRef b) { return StringComparer.Ordinal.Compare(a.Key, b.Key); });
            return result;
        }

        static PortRef FindPort(Vessel vessel, string key)
        {
            foreach (PortRef port in Ports(vessel)) if (port.Key == key) return port;
            return null;
        }

        List<Vessel> Targets(Vessel active)
        {
            var result = new List<Vessel>();
            if (FlightGlobals.Vessels == null || active == null || active.mainBody == null) return result;
            foreach (Vessel vessel in FlightGlobals.Vessels)
                if (vessel != null && vessel.id != active.id && vessel.mainBody == active.mainBody && vessel.loaded && !vessel.packed && Ports(vessel).Count > 0)
                    result.Add(vessel);
            result.Sort(delegate(Vessel a, Vessel b)
            {
                int byName = StringComparer.OrdinalIgnoreCase.Compare(a.vesselName ?? "", b.vesselName ?? "");
                return byName != 0 ? byName : a.id.CompareTo(b.id);
            });
            return result;
        }

        void SelectTarget(Vessel target)
        {
            Guid id = target == null ? Guid.Empty : target.id;
            if (targetVesselId == id) return;
            targetVesselId = id;
            targetStructure = target == null ? "" : StructureSignature(target);
            targetPortKey = "";
            ClearReadings();
            status = id == Guid.Empty ? "Choose a loaded same-body target vessel." : "Target changed; choose a target port.";
        }

        static string PortPicker(string title, List<PortRef> ports, ref string selectedKey, ref Vector2 scroll)
        {
            GUILayout.Label(title);
            if (ports.Count == 0) { selectedKey = ""; GUILayout.Label("No loaded docking ports available."); return ""; }
            int selectedIndex = -1;
            string[] labels = new string[ports.Count];
            for (int i = 0; i < ports.Count; i++)
            {
                labels[i] = ports[i].Label;
                if (ports[i].Key == selectedKey) selectedIndex = i;
            }
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(90));
            int chosen = GUILayout.SelectionGrid(selectedIndex, labels, 1, GUILayout.MinHeight(26 * ports.Count));
            GUILayout.EndScrollView();
            if (chosen >= 0 && chosen < ports.Count) selectedKey = ports[chosen].Key;
            return selectedKey;
        }

        public void Draw(Vessel active, string save, object game, double now)
        {
            try { DrawCore(active, save, game, now); }
            catch (Exception)
            {
                ClearReadings();
                status = "Vessel or port list changed during display; readings cleared.";
                GUILayout.Label(status);
            }
        }

        void DrawCore(Vessel active, string save, object game, double now)
        {
            bool matches = false;
            try { matches = active != null && active.id == ownVesselId && active.mainBody == body && (save ?? "") == saveFolder && object.ReferenceEquals(gameContext, game) && Finite(now) && now >= sampleUT && (!hasPreviousRange || now >= previousRangeUT); }
            catch (Exception) { }
            if ((!matches || (hasObservedUT && now < lastObservedUT)) && ownVesselId != Guid.Empty)
                Invalidate("Context or sample time changed; choose ports again.");
            if (FlightDriver.Pause || TimeWarp.CurrentRateIndex != 0 || TimeWarp.CurrentRate != 1.0f)
                ClearSelection(FlightDriver.Pause ? "Paused; select ports again after resuming." : "Time warp active; select ports again at 1x.");

            GUILayout.Label("LOADED DOCKING PORT INSTRUMENTS / READ-ONLY");
            GUILayout.Label("Uses live loaded port transforms. No stock target, controls, docking actions, nodes, or thresholds are changed or inferred.");
            if (FlightDriver.Pause || TimeWarp.CurrentRateIndex != 0 || TimeWarp.CurrentRate != 1.0f)
            {
                GUILayout.Label(FlightDriver.Pause ? "Paused; resume before selecting ports." : "Time warp active; return to 1x before selecting ports.");
                return;
            }
            if (active == null || !active.loaded || active.packed || active.mainBody == null)
            {
                ClearSelection("Active vessel must be loaded and unpacked.");
                GUILayout.Label("Active vessel must be loaded and unpacked.");
                return;
            }
            if (!FlightGlobals.ready)
            {
                ClearReadings();
                GUILayout.Label("Flight vessel data is not ready; docking transforms are unavailable.");
                return;
            }
            List<PortRef> ownPorts = Ports(active);
            string previousOwnKey = ownPortKey;
            PortPicker("Own vessel ports", ownPorts, ref ownPortKey, ref ownPortScroll);
            if (ownPortKey != previousOwnKey) ClearReadings();

            List<Vessel> targets = Targets(active);
            if (targets.Count == 0)
            {
                SelectTarget(null);
                GUILayout.Label("No other loaded, unpacked same-body vessel with a docking port is available.");
                return;
            }
            int targetIndex = -1;
            string[] targetLabels = new string[targets.Count];
            for (int i = 0; i < targets.Count; i++)
            {
                Vessel vessel = targets[i];
                targetLabels[i] = (vessel.vesselName ?? "Unnamed vessel") + " [" + vessel.id + "]";
                if (vessel.id == targetVesselId) targetIndex = i;
            }
            targetScroll = GUILayout.BeginScrollView(targetScroll, GUILayout.Height(82));
            int chosenTarget = GUILayout.SelectionGrid(targetIndex, targetLabels, 1, GUILayout.MinHeight(26 * targets.Count));
            GUILayout.EndScrollView();
            if (chosenTarget >= 0 && chosenTarget < targets.Count) SelectTarget(targets[chosenTarget]);

            Vessel target = FindTarget(active, targetVesselId);
            if (target == null || target.mainBody != active.mainBody || !target.loaded || target.packed)
            {
                ClearReadings();
                GUILayout.Label(status.Length == 0 ? "Choose a loaded target vessel." : status);
                return;
            }
            GUILayout.Label("Target: " + (target.vesselName ?? "Unnamed vessel") + " [" + target.id + "] | " + (target.mainBody.bodyName ?? "body unavailable"));
            List<PortRef> targetPorts = Ports(target);
            string previousTargetKey = targetPortKey;
            PortPicker("Target vessel ports", targetPorts, ref targetPortKey, ref targetPortScroll);
            if (targetPortKey != previousTargetKey) ClearReadings();

            string currentOwnStructure = StructureSignature(active);
            string currentTargetStructure = StructureSignature(target);
            if ((ownStructure.Length != 0 && currentOwnStructure != ownStructure) ||
                (targetStructure.Length != 0 && currentTargetStructure != targetStructure))
            {
                ClearSelection("Vessel parts changed; select ports again.");
                GUILayout.Label(status);
                return;
            }
            // A freshly reselected controller may not have captured its own-vessel
            // structure yet. Bind the current structure instead of treating that
            // uninitialized value as a change that cancels the just-made selection.
            ownStructure = currentOwnStructure;
            targetStructure = currentTargetStructure;

            if (!String.IsNullOrEmpty(status)) GUILayout.Label(status);
            if (!hasGeometry) { GUILayout.Label("Waiting for a live port snapshot at 1x..."); return; }
            if (!Finite(now) || now < sampleUT)
            {
                ClearReadings();
                GUILayout.Label("Universal time changed; waiting for a fresh port snapshot.");
                return;
            }
            PortRef currentOwnPort = FindPort(active, ownPortKey);
            PortRef currentTargetPort = FindPort(target, targetPortKey);
            if (currentOwnPort == null || currentTargetPort == null)
            {
                ClearSelection("Selected port changed; readings cleared.");
                GUILayout.Label(status);
                return;
            }
            GUILayout.Label("Own part " + currentOwnPort.Part.flightID + " / " + (currentOwnPort.Module.state ?? "state unavailable") +
                " | target part " + currentTargetPort.Part.flightID + " / " + (currentTargetPort.Module.state ?? "state unavailable"));
            GUILayout.Label("Separation " + rangeMeters.ToString("F3") + " m | axial offset " + axialMeters.ToString("+0.000;-0.000;0.000") + " m (positive along own port outward axis) | lateral " + lateralMeters.ToString("F3") + " m");
            GUILayout.Label("Opposing-face angle " + angleDegrees.ToString("F2") + " deg (0 means outward axes oppose)");
            GUILayout.Label(hasClosing ? "Observed closing " + closingMetersPerSecond.ToString("+0.000;-0.000;0.000") + " m/s (positive means measured range decreased)" : "Observed closing rate unavailable until two valid samples within 3 s.");
            GUILayout.Label("Sample UT " + sampleUT.ToString("F2") + " | age " + Math.Max(0, now - sampleUT).ToString("F2") + " s. Measurements only; no docking clearance or success determination.");
        }
    }
}
