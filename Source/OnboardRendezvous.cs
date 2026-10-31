using System;
using System.Collections.Generic;
using UnityEngine;

namespace MegaCrew
{
    /// <summary>Read-only same-body orbit snapshot and short-horizon linear proximity estimate.</summary>
    internal sealed class OnboardRendezvous
    {
        const double SampleIntervalSeconds = 1.0;
        const double HorizonSeconds = 60.0;

        Guid activeGuid, selectedGuid;
        CelestialBody referenceBody;
        string saveFolder = "";
        Vector2 vesselScroll;
        double nextSampleUT, sampleUT, lastObservedUT;
        bool hasObservedUT;
        bool targetLookupFailed;
        RendezvousEstimate estimate;
        string status = "Choose an orbiting vessel to inspect.";
        readonly OnboardDockingInstruments dockingInstruments = new OnboardDockingInstruments();

        public void Reset(bool resetDocking = true)
        {
            activeGuid = Guid.Empty;
            selectedGuid = Guid.Empty;
            referenceBody = null;
            saveFolder = "";
            hasObservedUT = false;
            lastObservedUT = 0;
            ClearEstimate();
            status = "Choose an orbiting vessel to inspect.";
            if (resetDocking) dockingInstruments.Reset();
        }

        void ClearEstimate()
        {
            estimate = null;
            sampleUT = 0;
            nextSampleUT = 0;
        }

        static bool Finite(double value)
        {
            return !Double.IsNaN(value) && !Double.IsInfinity(value);
        }

        public void Update(Vessel active, string save, double now, bool paused, bool warped)
        {
            dockingInstruments.Update(active, save, HighLogic.CurrentGame, now, paused,
                warped || TimeWarp.CurrentRateIndex != 0 || TimeWarp.CurrentRate != 1.0f);
            if (!Finite(now) || now < 0)
            {
                ClearEstimate();
                status = "Universal time is invalid; no snapshot taken.";
                return;
            }

            if (active == null || active.orbit == null || active.orbit.referenceBody == null)
            {
                Reset(false);
                status = "Active vessel orbit is unavailable.";
                return;
            }

            var body = active.orbit.referenceBody;
            if (activeGuid != active.id || referenceBody != body || saveFolder != (save ?? ""))
            {
                activeGuid = active.id;
                referenceBody = body;
                saveFolder = save ?? "";
                selectedGuid = Guid.Empty;
                ClearEstimate();
                status = "Vessel, body, or save changed. Choose a target again.";
            }

            if (hasObservedUT && now < lastObservedUT)
            {
                ClearEstimate();
                status = "Universal time moved backwards; waiting for a fresh snapshot.";
            }
            lastObservedUT = now;
            hasObservedUT = true;

            if (paused || warped)
            {
                ClearEstimate();
                status = paused ? "Sampling paused; resume for a fresh snapshot." : "Time warp active; return to 1x for a fresh snapshot.";
                return;
            }

            // The active vessel must be physically available for this onboard readout.
            // A packed/unloaded target remains eligible and is sampled from its orbit.
            if (!active.loaded || active.packed)
            {
                ClearEstimate();
                status = "Active vessel is packed or unloaded; no snapshot taken.";
                return;
            }
            if (active.situation != Vessel.Situations.ORBITING)
            {
                ClearEstimate();
                status = "Active vessel is not orbiting; rendezvous snapshot unavailable.";
                return;
            }

            Vessel target = FindSelectedTarget(active);
            if (targetLookupFailed)
            {
                ClearEstimate();
                nextSampleUT = now + SampleIntervalSeconds;
                status = "Vessel list changed during lookup; waiting before retry.";
                return;
            }
            if (target == null)
            {
                if (selectedGuid != Guid.Empty)
                {
                    selectedGuid = Guid.Empty;
                    ClearEstimate();
                    status = "Selected target is no longer an eligible same-body orbiting vessel.";
                }
                else if (estimate == null) status = "Choose an orbiting vessel to inspect.";
                return;
            }

            if (now < nextSampleUT) return;

            try
            {
                if (target.orbit == null || target.orbit.referenceBody != body)
                    throw new InvalidOperationException("Target orbit reference body changed.");

                // Both orbital states use exactly the same UT. This is a local linear
                // estimate only; it does not create a stock target or alter a trajectory.
                Vector3d ownPosition = active.orbit.getRelativePositionAtUT(now);
                Vector3d ownVelocity = active.orbit.getOrbitalVelocityAtUT(now);
                Vector3d targetPosition = target.orbit.getRelativePositionAtUT(now);
                Vector3d targetVelocity = target.orbit.getOrbitalVelocityAtUT(now);
                double rx = targetPosition.x - ownPosition.x;
                double ry = targetPosition.y - ownPosition.y;
                double rz = targetPosition.z - ownPosition.z;
                double vx = targetVelocity.x - ownVelocity.x;
                double vy = targetVelocity.y - ownVelocity.y;
                double vz = targetVelocity.z - ownVelocity.z;
                estimate = RendezvousModel.Estimate(rx, ry, rz, vx, vy, vz, HorizonSeconds);
                sampleUT = now;
                nextSampleUT = now + SampleIntervalSeconds;
                status = "";
            }
            catch (Exception)
            {
                ClearEstimate();
                nextSampleUT = now + SampleIntervalSeconds;
                status = "Orbit snapshot unavailable; waiting before retry.";
            }
        }

        List<Vessel> EligibleTargets(Vessel active)
        {
            var result = new List<Vessel>();
            targetLookupFailed = false;
            try
            {
                if (active == null || active.orbit == null || active.orbit.referenceBody == null || FlightGlobals.Vessels == null) return result;
                var body = active.orbit.referenceBody;
                foreach (Vessel vessel in FlightGlobals.Vessels)
                {
                    if (vessel == null || vessel.id == active.id || vessel.situation != Vessel.Situations.ORBITING || vessel.orbit == null || vessel.orbit.referenceBody != body) continue;
                    result.Add(vessel);
                }
                result.Sort(delegate(Vessel a, Vessel b)
                {
                    int byName = StringComparer.OrdinalIgnoreCase.Compare(a.vesselName ?? "", b.vesselName ?? "");
                    return byName != 0 ? byName : a.id.CompareTo(b.id);
                });
            }
            catch (Exception)
            {
                result.Clear();
                targetLookupFailed = true;
                status = "Vessel list unavailable; retrying.";
            }
            return result;
        }

        Vessel FindSelectedTarget(Vessel active)
        {
            targetLookupFailed = false;
            if (selectedGuid == Guid.Empty) return null;
            try
            {
                if (active == null || active.orbit == null || active.orbit.referenceBody == null || FlightGlobals.Vessels == null) return null;
                CelestialBody body = active.orbit.referenceBody;
                foreach (Vessel target in FlightGlobals.Vessels)
                    if (target != null && target.id == selectedGuid && target.situation == Vessel.Situations.ORBITING && target.orbit != null && target.orbit.referenceBody == body)
                        return target;
            }
            catch (Exception) { targetLookupFailed = true; }
            return null;
        }

        void Select(Guid id)
        {
            if (selectedGuid == id) return;
            selectedGuid = id;
            ClearEstimate();
            status = id == Guid.Empty ? "Choose an orbiting vessel to inspect." : "Target changed; waiting for a fresh snapshot.";
        }

        public void Draw(Vessel active)
        {
            bool contextMatches = false;
            try { contextMatches = active != null && active.orbit != null && active.orbit.referenceBody != null && active.id == activeGuid && active.orbit.referenceBody == referenceBody && (HighLogic.SaveFolder ?? "") == saveFolder; }
            catch (Exception) { }
            if (!contextMatches)
            {
                Reset(false);
                status = "Vessel, body, or save changed; waiting for a fresh snapshot.";
            }

            dockingInstruments.Draw(active, HighLogic.SaveFolder, HighLogic.CurrentGame, Planetarium.GetUniversalTime());
            GUILayout.Label("RENDEZVOUS / READ-ONLY ORBIT SNAPSHOT");
            GUILayout.Label("Select another same-reference-body vessel in orbit. No stock target, controls, or trajectory are changed.");
            List<Vessel> targets = EligibleTargets(active);
            if (targets.Count == 0)
            {
                if (!targetLookupFailed) Select(Guid.Empty);
                GUILayout.Label(targetLookupFailed ? status : "No other same-body orbiting vessels are available.");
                return;
            }

            int selectedIndex = -1;
            var labels = new string[targets.Count];
            try
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    Vessel target = targets[i];
                    labels[i] = (target.vesselName ?? "Unnamed vessel") + (target.loaded ? " [loaded]" : " [unloaded]") + (target.packed ? " [packed]" : "");
                    if (target.id == selectedGuid) selectedIndex = i;
                }
            }
            catch (Exception)
            {
                ClearEstimate();
                Select(Guid.Empty);
                status = "Vessel list changed during display; retrying.";
                GUILayout.Label(status);
                return;
            }
            vesselScroll = GUILayout.BeginScrollView(vesselScroll, GUILayout.Height(150));
            int chosen = GUILayout.SelectionGrid(selectedIndex, labels, 1, GUILayout.MinHeight(28 * targets.Count));
            GUILayout.EndScrollView();
            if (chosen >= 0 && chosen < targets.Count) Select(targets[chosen].id);

            Vessel selected = FindSelectedTarget(active);
            if (selected == null)
            {
                if (targetLookupFailed)
                {
                    ClearEstimate();
                    status = "Vessel list changed during display; retrying.";
                }
                GUILayout.Label(status);
                return;
            }
            try
            {
                GUILayout.Label("Target: " + (selected.vesselName ?? "Unnamed vessel") + " | " + selected.id);
                GUILayout.Label("Body: " + referenceBody.bodyName + " | target state: " + (selected.loaded ? "loaded" : "unloaded") + (selected.packed ? " / packed" : " / unpacked"));
            }
            catch (Exception)
            {
                ClearEstimate();
                Select(Guid.Empty);
                status = "Target vessel changed during display; retrying.";
                GUILayout.Label(status);
                return;
            }
            if (estimate == null)
            {
                GUILayout.Label(status.Length == 0 ? "Waiting for orbit snapshot..." : status);
                return;
            }

            double now = Planetarium.GetUniversalTime();
            if (!Finite(now) || now < 0 || now < sampleUT || (hasObservedUT && now < lastObservedUT))
            {
                ClearEstimate();
                status = "Universal time changed or became invalid; waiting for a fresh snapshot.";
                GUILayout.Label(status);
                return;
            }
            double age = now - sampleUT;
            GUILayout.Label("Snapshot UT " + sampleUT.ToString("F1") + " | age " + age.ToString("F1") + " s");
            GUILayout.Label("Range " + estimate.RangeMetres.ToString("F0") + " m | relative speed " + estimate.RelativeSpeedMetresPerSecond.ToString("F2") + " m/s");
            GUILayout.Label(estimate.ClosingSpeedDefined
                ? "Closing speed " + estimate.ClosingSpeedMetresPerSecond.ToString("+0.00;-0.00;0.00") + " m/s (positive = closing, negative = opening)"
                : "Closing speed undefined at zero separation.");
            string bound = estimate.ClosestApproachTimeSeconds <= 0 ? " (now boundary)" : estimate.ClosestApproachTimeSeconds >= HorizonSeconds ? " (60 s boundary)" : "";
            GUILayout.Label("Straight-line minimum within 60 s of snapshot: " + estimate.ClosestApproachSeparationMetres.ToString("F0") + " m at t+" + estimate.ClosestApproachTimeSeconds.ToString("F1") + " s" + bound);
            GUILayout.Label("Local linear estimate only; not an orbital intercept, live docking guidance, or docking-clearance check.");
            if (!String.IsNullOrEmpty(status)) GUILayout.Label(status);
        }
    }
}
