using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace MegaCrew
{
    /// <summary>Transient, read-only comparison of ScienceData currently retained aboard the active vessel.</summary>
    internal sealed class OnboardScienceObservation
    {
        sealed class ScienceDataReferenceComparer : IEqualityComparer<ScienceData>
        {
            public bool Equals(ScienceData left, ScienceData right) { return System.Object.ReferenceEquals(left, right); }
            public int GetHashCode(ScienceData value) { return RuntimeHelpers.GetHashCode(value); }
        }

        sealed class VesselSnapshot
        {
            public ScienceObservationSnapshot Data;
            public Dictionary<string, int> CardCounts;
            public double UT;
            public Guid VesselId;
            public string VesselName, BodyName;
        }

        static readonly IEqualityComparer<ScienceData> DataReferenceComparer = new ScienceDataReferenceComparer();
        Guid vesselId;
        CelestialBody body;
        object saveGame;
        string saveFolder = "";
        double lastUT;
        bool hasLastUT;
        VesselSnapshot baseline, current;
        ScienceObservationComparison comparison;
        string status = "Capture a baseline of retained data to begin.";

        public void Reset()
        {
            vesselId = Guid.Empty;
            body = null;
            saveGame = null;
            saveFolder = "";
            lastUT = 0;
            hasLastUT = false;
            baseline = current = null;
            comparison = null;
            status = "Capture a baseline of retained data to begin.";
        }

        void ClearSnapshots(string message)
        {
            baseline = current = null;
            comparison = null;
            status = message;
        }

        static bool Finite(double value) { return !Double.IsNaN(value) && !Double.IsInfinity(value); }

        public void UpdateContext(Vessel vessel, string save, object game, double now)
        {
            if (!Finite(now) || now < 0)
            {
                ClearSnapshots("Universal time is invalid; capture baseline again when valid.");
                hasLastUT = false;
                return;
            }

            CelestialBody activeBody = null;
            try { if (vessel != null) activeBody = vessel.mainBody; }
            catch (Exception) { }
            if (vessel == null || activeBody == null)
            {
                ClearSnapshots("Active vessel or body is unavailable.");
                hasLastUT = false;
                return;
            }

            string folder = save ?? "";
            bool changed = vesselId != vessel.id || body != activeBody || saveFolder != folder || !System.Object.ReferenceEquals(saveGame, game);
            if (changed)
            {
                vesselId = vessel.id;
                body = activeBody;
                saveFolder = folder;
                saveGame = game;
                ClearSnapshots("Vessel, body, save, or game changed; capture a new baseline.");
                hasLastUT = false;
            }

            if (hasLastUT && now < lastUT)
            {
                ClearSnapshots("Universal time moved backwards; capture a new baseline.");
            }
            lastUT = now;
            hasLastUT = true;
        }

        bool CaptureAllowed(Vessel vessel, double now, bool paused, bool warped, out string reason)
        {
            reason = "";
            if (paused) reason = "Resume the game before capturing.";
            else if (warped) reason = "Return to 1x before capturing.";
            else if (!Finite(now) || now < 0) reason = "Universal time is invalid.";
            else if (vessel == null || vessel.isEVA || !vessel.loaded || vessel.packed) reason = "Active vessel must be loaded, unpacked, and not EVA.";
            else if (vessel.id != vesselId || vessel.mainBody != body || saveFolder != (HighLogic.SaveFolder ?? "") || !System.Object.ReferenceEquals(saveGame, HighLogic.CurrentGame)) reason = "Vessel, body, save, or game changed; wait for context refresh.";
            return reason.Length == 0;
        }

        bool TryCapture(Vessel vessel, double now, out VesselSnapshot snapshot, out string error)
        {
            snapshot = null;
            error = "";
            try
            {
                var seen = new HashSet<ScienceData>(DataReferenceComparer);
                var observations = new List<ScienceDataObservation>();
                var counts = new Dictionary<string, int>(StringComparer.Ordinal);
                if (vessel.parts == null) throw new InvalidOperationException("Vessel part list is unavailable.");
                foreach (Part part in vessel.parts)
                {
                    if (part == null || part.Modules == null) throw new InvalidOperationException("Vessel module list is unavailable.");
                    foreach (PartModule module in part.Modules)
                    {
                        var container = module as IScienceDataContainer;
                        if (container == null) continue;
                        // GetData is observational. In KSP a null collection means empty;
                        // DumpData is deliberately never called.
                        ScienceData[] stored = container.GetData();
                        if (stored == null) continue;
                        foreach (ScienceData data in stored)
                        {
                            if (data == null) throw new InvalidOperationException("Science container returned a null data card.");
                            if (!seen.Add(data)) continue;
                            string subject = data.subjectID;
                            double amount = data.dataAmount;
                            if (String.IsNullOrWhiteSpace(subject) || !Finite(amount) || amount < 0)
                                throw new InvalidOperationException("Science container returned an invalid subject or data amount.");
                            observations.Add(new ScienceDataObservation(subject, amount));
                            int count;
                            counts.TryGetValue(subject, out count);
                            counts[subject] = count + 1;
                        }
                    }
                }

                snapshot = new VesselSnapshot
                {
                    Data = ScienceObservationSnapshot.Create(observations),
                    CardCounts = counts,
                    UT = now,
                    VesselId = vessel.id,
                    VesselName = vessel.vesselName ?? "Unnamed vessel",
                    BodyName = vessel.mainBody.bodyName
                };
                return true;
            }
            catch (Exception ex)
            {
                error = "Retained data snapshot unavailable: " + ex.Message;
                return false;
            }
        }

        public void Draw(Vessel vessel, string save, object game, double now, bool paused, bool warped)
        {
            UpdateContext(vessel, save, game, now);
            string reason;
            bool canCapture = CaptureAllowed(vessel, now, paused, warped, out reason);

            GUILayout.Label("Retained science data observation (read-only; session only)");
            GUILayout.Label("Captures currently stored science reports and groups measured data units by subject. Repeated views recognized as the same report are counted once.");
            try
            {
                if (vessel == null || vessel.mainBody == null) throw new InvalidOperationException();
                ExperimentSituations situation = ScienceUtil.GetExperimentSituation(vessel);
                string biome = ScienceUtil.GetExperimentBiome(vessel.mainBody, vessel.latitude, vessel.longitude);
                string altitude = Finite(vessel.altitude) ? vessel.altitude.ToString("F0") + " m" : "unavailable";
                GUILayout.Label("Science context: " + vessel.mainBody.bodyName + " | " + situation + " | altitude " + altitude + " | biome " + (String.IsNullOrWhiteSpace(biome) ? "unavailable" : biome));
            }
            catch (Exception)
            {
                GUILayout.Label("Science context unavailable from the active vessel.");
            }
            GUI.enabled = canCapture;
            if (GUILayout.Button("Capture retained-data baseline"))
            {
                baseline = current = null;
                comparison = null;
                VesselSnapshot captured;
                string error;
                if (TryCapture(vessel, now, out captured, out error))
                {
                    baseline = captured;
                    current = null;
                    comparison = null;
                    status = "Baseline captured; no experiment completion or science reward is inferred.";
                }
                else status = error;
            }
            GUI.enabled = canCapture && baseline != null;
            if (GUILayout.Button("Compare current retained data to baseline"))
            {
                current = null;
                comparison = null;
                VesselSnapshot captured;
                string error;
                if (TryCapture(vessel, now, out captured, out error))
                {
                    current = captured;
                    try
                    {
                        comparison = ScienceObservationModel.Compare(baseline.Data, current.Data);
                        status = "Comparison sampled; changes describe retained data amounts only.";
                    }
                    catch (Exception ex)
                    {
                        comparison = null;
                        status = "Science comparison unavailable: " + ex.Message;
                    }
                }
                else status = error;
            }
            GUI.enabled = true;

            if (!canCapture) GUILayout.Label(reason);
            if (baseline != null)
            {
                GUILayout.Label("Baseline: UT " + baseline.UT.ToString("F1") + " | " + baseline.VesselName + " | " + baseline.VesselId + " | " + baseline.BodyName);
                if (current != null) GUILayout.Label("Comparison capture: UT " + current.UT.ToString("F1") + " | " + current.VesselName + " | " + current.VesselId + " | " + current.BodyName);
            }
            if (comparison != null)
            {
                GUILayout.Label("Retained data units: " + comparison.BeforeTotalDataUnits.ToString("G6") + " → " + comparison.AfterTotalDataUnits.ToString("G6") + " | change " + comparison.NetChangeDataUnits.ToString("G6"));
                foreach (ScienceSubjectDelta delta in comparison.Subjects)
                {
                    int beforeCount = CountFor(baseline, delta.SubjectId);
                    int afterCount = CountFor(current, delta.SubjectId);
                    GUILayout.Label(delta.SubjectId + " | " + delta.BeforeDataUnits.ToString("G6") + " → " + delta.AfterDataUnits.ToString("G6") + " data units | change " + delta.DeltaDataUnits.ToString("G6") + " | reported entries " + beforeCount + " → " + afterCount + " | " + delta.Change);
                }
                if (comparison.Subjects.Count == 0) GUILayout.Label("No retained science data in either snapshot.");
            }
            GUILayout.Label(status);
            GUILayout.Label("These captures measure data retained at their recorded times; they do not verify experiment completion, science points, transmission, or recovery. Some third-party containers may expose duplicate-looking reports that cannot be reliably distinguished.");
        }

        static int CountFor(VesselSnapshot snapshot, string subject)
        {
            int count;
            return snapshot != null && snapshot.CardCounts.TryGetValue(subject, out count) ? count : 0;
        }
    }
}
