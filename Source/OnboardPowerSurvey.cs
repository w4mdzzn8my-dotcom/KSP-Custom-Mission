using System;
using System.Collections.Generic;
using UnityEngine;

namespace MegaCrew
{
    /// <summary>Transient, read-only measurement of total ElectricCharge on one unchanged vessel.</summary>
    internal sealed class OnboardPowerSurvey
    {
        const double SurveyDurationSeconds = 60.0;
        const double SampleIntervalSeconds = 2.0;
        const double MaximumSampleGapSeconds = 5.0;

        PowerSurveySession session;
        Guid vesselId;
        string save = "";
        string vesselName = "";
        object gameIdentity;
        CelestialBody body;
        uint[] partIds;
        double nextSampleUT;
        double lastClockUT = Double.NaN;
        string statusMessage = "No power survey has been run.";

        bool Running
        {
            get
            {
                if (session == null) return false;
                PowerSurveyStatus state = session.Snapshot().Status;
                return state == PowerSurveyStatus.AwaitingFirstSample || state == PowerSurveyStatus.Collecting;
            }
        }

        public void Update(Vessel active, string currentSave, object currentGame, double ut, bool paused, bool warped)
        {
            if (!Running) return;
            string reason;
            if (!ContextMatches(active, currentSave, currentGame, out reason)) { Abort(reason); return; }
            if (paused) { Abort("Survey aborted because the game was paused."); return; }
            if (warped) { Abort("Survey aborted because time warp was active."); return; }
            if (!IsFinite(ut) || ut < 0) { Abort("Survey aborted because universal time became invalid."); return; }
            if (!Double.IsNaN(lastClockUT) && ut < lastClockUT) { Abort("Survey aborted because universal time moved backward."); return; }
            lastClockUT = ut;

            uint[] currentIds;
            if (!TryPartIds(active, out currentIds, out reason)) { Abort(reason); return; }
            if (!SameIds(partIds, currentIds)) { Abort("Survey aborted because vessel part IDs or structure changed."); return; }
            if (ut < nextSampleUT) return;

            double amount, capacity;
            if (!TryReadCharge(active, out amount, out capacity, out reason)) { Abort(reason); return; }
            PowerSurveySnapshot result = session.Observe(ut, amount, capacity);
            if (result.Status == PowerSurveyStatus.Invalid)
            {
                statusMessage = "Survey invalid: " + result.Reason;
                return;
            }
            if (result.IsComplete) statusMessage = "60-second observation complete.";
            nextSampleUT = ut + SampleIntervalSeconds;
        }

        public void Abort(string reason)
        {
            if (session != null && Running) session.Cancel();
            statusMessage = String.IsNullOrWhiteSpace(reason) ? "Survey cancelled." : reason;
        }

        public void Draw(Vessel active, string currentSave, object currentGame, double ut, bool paused, bool warped)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("POWER SURVEY | read-only ElectricCharge observation");
            bool running = Running;
            if (GUILayout.Button(running ? "Cancel survey" : "Start 60-second survey"))
            {
                if (running) Abort("Survey cancelled by operator; partial measurements are not a completed survey.");
                else Start(active, currentSave, currentGame, ut, paused, warped);
            }

            if (session == null)
            {
                GUILayout.Label(statusMessage);
                GUILayout.Label("Measures stored ElectricCharge and capacity only. This is not an endurance or cryosleep-readiness certification.");
                GUILayout.EndVertical();
                return;
            }

            PowerSurveySnapshot snapshot = session.Snapshot();
            GUILayout.Label("Status: " + snapshot.Status + (String.IsNullOrEmpty(statusMessage) ? "" : " | " + statusMessage));
            if (!String.IsNullOrEmpty(snapshot.Reason)) GUILayout.Label("Survey note: " + snapshot.Reason);
            GUILayout.Label("Recorded on vessel: " + (String.IsNullOrWhiteSpace(vesselName) ? "unavailable" : vesselName) + " | GUID: " + vesselId + " | save: " + save + " | body: " + (body == null ? "unavailable" : body.bodyName));
            GUILayout.Label("Displayed values belong to the UT window above; completed or cancelled results are retained historical observations.");
            GUILayout.Label("Sample window UT: " + (snapshot.HasSamples ? snapshot.Samples[0].UniversalTime.ToString("F1") + " → " + snapshot.Samples[snapshot.Samples.Count - 1].UniversalTime.ToString("F1") : "not captured") +
                " | observed coverage: " + snapshot.ObservedElapsedSeconds.ToString("F1") + " / " + SurveyDurationSeconds.ToString("F0") + " s" +
                " | samples: " + snapshot.SampleCount);
            if (snapshot.HasSamples)
            {
                GUILayout.Label("ElectricCharge amount: " + snapshot.InitialAmount.ToString("G6") + " → " + snapshot.FinalAmount.ToString("G6") +
                    " | capacity: " + snapshot.InitialCapacity.ToString("G6"));
                GUILayout.Label("Observed net stored-charge change: " + snapshot.NetChange.ToString("+0.######;-0.######;0") + " EC" +
                    (snapshot.HasRate ? " | observed rate: " + snapshot.NetRatePerSecond.ToString("+0.######;-0.######;0") + " EC/s" : " | rate not yet measured"));
                GUILayout.Label("Sampled amount range: " + snapshot.MinimumAmount.ToString("G6") + " – " + snapshot.MaximumAmount.ToString("G6") + " EC");
                GUILayout.Label("Recent samples (UT | amount / capacity):");
                int first = Math.Max(0, snapshot.Samples.Count - 5);
                for (int i = first; i < snapshot.Samples.Count; i++)
                {
                    PowerSurveySample sample = snapshot.Samples[i];
                    GUILayout.Label(sample.UniversalTime.ToString("F1") + " | " + sample.Amount.ToString("G6") + " / " + sample.Capacity.ToString("G6") + " EC");
                }
            }
            GUILayout.Label("Zero stored-charge change does not prove generation and demand are balanced; a full or empty store can mask it. Sampling stops on pause, warp, time gap, vessel/save/body change, or part-structure change. No future rate is projected.");
            GUILayout.EndVertical();
        }

        void Start(Vessel active, string currentSave, object currentGame, double ut, bool paused, bool warped)
        {
            ClearContext();
            session = new PowerSurveySession(SurveyDurationSeconds, MaximumSampleGapSeconds);
            statusMessage = "Preparing first sample.";
            if (paused || warped) { Abort("Survey not started: pause and time warp must be off."); return; }
            if (!IsFinite(ut) || ut < 0) { Abort("Survey not started: universal time is invalid."); return; }
            if (!Eligible(active, out string reason)) { Abort("Survey not started: " + reason); return; }
            if (String.IsNullOrWhiteSpace(currentSave) || currentGame == null) { Abort("Survey not started: save or game identity is unavailable."); return; }

            uint[] ids;
            if (!TryPartIds(active, out ids, out reason)) { Abort("Survey not started: " + reason); return; }
            vesselId = active.id;
            vesselName = active.vesselName ?? "";
            save = currentSave;
            gameIdentity = currentGame;
            body = active.mainBody;
            partIds = ids;
            double amount, capacity;
            if (!TryReadCharge(active, out amount, out capacity, out reason)) { Abort("Survey not started: " + reason); return; }
            PowerSurveySnapshot first = session.Observe(ut, amount, capacity);
            if (first.Status == PowerSurveyStatus.Invalid)
            {
                statusMessage = "Survey invalid: " + first.Reason;
                return;
            }
            lastClockUT = ut;
            nextSampleUT = ut + SampleIntervalSeconds;
            statusMessage = "Survey running; waiting for the next sample.";
        }

        bool ContextMatches(Vessel active, string currentSave, object currentGame, out string reason)
        {
            reason = "";
            if (!Eligible(active, out reason)) return false;
            if (active.id != vesselId) { reason = "Survey aborted because the active vessel changed."; return false; }
            if (currentSave != save) { reason = "Survey aborted because the save changed."; return false; }
            if (!System.Object.ReferenceEquals(gameIdentity, currentGame)) { reason = "Survey aborted because the loaded game changed."; return false; }
            if (!System.Object.ReferenceEquals(body, active.mainBody)) { reason = "Survey aborted because the vessel body changed."; return false; }
            return true;
        }

        static bool Eligible(Vessel vessel, out string reason)
        {
            reason = "";
            if (vessel == null || vessel != FlightGlobals.ActiveVessel) reason = "the active vessel is unavailable or changed";
            else if (vessel.isEVA) reason = "EVA vessels are not supported";
            else if (!vessel.loaded || vessel.packed) reason = "the vessel must be loaded and unpacked";
            else if (vessel.mainBody == null) reason = "the vessel body is unavailable";
            return reason.Length == 0;
        }

        static bool TryPartIds(Vessel vessel, out uint[] ids, out string reason)
        {
            ids = null;
            reason = "";
            try
            {
                if (vessel.parts == null || vessel.parts.Count == 0) { reason = "vessel parts are unavailable"; return false; }
                ids = new uint[vessel.parts.Count];
                for (int i = 0; i < vessel.parts.Count; i++)
                {
                    if (vessel.parts[i] == null) { reason = "vessel contains a null part"; return false; }
                    ids[i] = vessel.parts[i].flightID;
                }
                Array.Sort(ids);
                for (int i = 1; i < ids.Length; i++)
                    if (ids[i] == ids[i - 1]) { reason = "vessel contains duplicate part IDs"; ids = null; return false; }
                return true;
            }
            catch (Exception ex) { reason = "part structure could not be read: " + ex.Message; return false; }
        }

        static bool SameIds(uint[] left, uint[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
            return true;
        }

        static bool TryReadCharge(Vessel vessel, out double amount, out double capacity, out string reason)
        {
            amount = capacity = 0;
            reason = "";
            try
            {
                foreach (Part part in vessel.parts)
                {
                    if (part == null || part.Resources == null) { reason = "part resource inventory is unavailable"; return false; }
                    foreach (PartResource resource in part.Resources)
                    {
                        if (resource == null) { reason = "a part returned a null resource entry"; return false; }
                        if (resource.resourceName != "ElectricCharge") continue;
                        double resourceAmount = resource.amount;
                        double resourceCapacity = resource.maxAmount;
                        if (!IsFinite(resourceAmount) || resourceAmount < 0.0 || !IsFinite(resourceCapacity) || resourceCapacity < 0.0 || resourceAmount > resourceCapacity)
                        { reason = "an ElectricCharge resource has invalid amount or capacity"; return false; }
                        amount += resourceAmount;
                        capacity += resourceCapacity;
                        if (!IsFinite(amount) || !IsFinite(capacity)) { reason = "ElectricCharge totals became invalid"; return false; }
                    }
                }
                return true;
            }
            catch (Exception ex) { reason = "ElectricCharge inventory could not be read: " + ex.Message; return false; }
        }

        void ClearContext()
        {
            vesselId = Guid.Empty;
            save = "";
            vesselName = "";
            gameIdentity = null;
            body = null;
            partIds = null;
            nextSampleUT = 0;
            lastClockUT = Double.NaN;
        }

        static bool IsFinite(double value) { return !Double.IsNaN(value) && !Double.IsInfinity(value); }
    }
}
