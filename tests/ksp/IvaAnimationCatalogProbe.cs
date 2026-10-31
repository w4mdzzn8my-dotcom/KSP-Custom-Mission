using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace MegaCrew.IvaAnimationQA
{
    // Disposable, one-shot catalog only. It never requests or changes an animation.
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class IvaAnimationCatalogProbe : MonoBehaviour
    {
        const string ScopeFile = "IvaAnimationQA.scope";
        const string ScopeContents = "MEGACREW_DISPOSABLE_IVA_ANIMATION_QA_V1\n";
        const string EnableFile = "IvaAnimationQA.enable";
        const string EnableContents = "AUTHORIZE ONE READ-ONLY IVA ANIMATION CATALOG FOR SAVE: MegaCrew Background QA\n";
        const string RunningFile = "IvaAnimationQA.running";
        const string DoneFile = "IvaAnimationQA.done";
        const string FailedFile = "IvaAnimationQA.failed";
        const string ReportFile = "IvaAnimationQA.report.txt";
        const string ExpectedRootName = "KSP-Hermes-QA";
        const string ExpectedSave = "MegaCrew Background QA";
        static readonly string[] CandidateNames = { "wavesToOtherKerbal", "true_noddingA" };

        bool finished;
        bool authorizedAtStartup;
        bool claimed;
        string qaRoot;

        sealed class CaptureResult
        {
            public bool Complete;
            public readonly StringBuilder Text = new StringBuilder();
            public int OccupiedSeats;
            public int RenderedCrew;
            public int AnimatorCount;
            public int FrozenCrewExcluded;
        }

        void Start()
        {
            try
            {
                qaRoot = Path.GetFullPath(KSPUtil.ApplicationRootPath);
                authorizedAtStartup = String.Equals(new DirectoryInfo(qaRoot).Name, ExpectedRootName, StringComparison.Ordinal) &&
                    ReadExact(Path.Combine(qaRoot, ScopeFile), ScopeContents);
            }
            catch { authorizedAtStartup = false; }
            if (!authorizedAtStartup) finished = true;
        }

        void Update()
        {
            if (finished || !authorizedAtStartup) return;
            try
            {
                Vessel vessel;
                string save;
                Game game;
                if (!Eligible(qaRoot, out vessel, out save, out game)) return;

                // Capture the context identity before claiming the one-shot run.
                Guid vesselId = vessel.id;
                double captureUt = Planetarium.GetUniversalTime();
                if (!Finite(captureUt)) return;
                var result = new CaptureResult();
                if (!TryClaim(qaRoot)) { finished = true; return; }
                claimed = true;
                finished = true;
                result.Text.AppendLine("scope=MEGACREW_DISPOSABLE_IVA_ANIMATION_QA_V1");
                result.Text.AppendLine("save=" + save);
                result.Text.AppendLine("vessel=" + Safe(vessel.vesselName));
                result.Text.AppendLine("vessel_id=" + vesselId.ToString("D"));
                result.Text.AppendLine("capture_ut=" + captureUt.ToString("R", CultureInfo.InvariantCulture));

                bool contextStillValid = SameEligibleContext(qaRoot, vessel, vesselId, save, game);
                if (contextStillValid) { result.Complete = true; Enumerate(vessel, result); }
                contextStillValid = contextStillValid && SameEligibleContext(qaRoot, vessel, vesselId, save, game);
                result.Complete = contextStillValid && result.Complete && result.OccupiedSeats > 0 &&
                    result.RenderedCrew == result.OccupiedSeats;
                result.Text.AppendLine("context_stable=" + contextStillValid.ToString().ToLowerInvariant());
                result.Text.AppendLine("occupied_seats=" + result.OccupiedSeats.ToString(CultureInfo.InvariantCulture));
                result.Text.AppendLine("rendered_crew=" + result.RenderedCrew.ToString(CultureInfo.InvariantCulture));
                result.Text.AppendLine("frozen_crew_excluded=" + result.FrozenCrewExcluded.ToString(CultureInfo.InvariantCulture));
                result.Text.AppendLine("animators=" + result.AnimatorCount.ToString(CultureInfo.InvariantCulture));
                result.Text.AppendLine("probe_completed=" + result.Complete.ToString().ToLowerInvariant());
                result.Text.AppendLine("animation_qualification=false");
                result.Text.AppendLine(result.Complete ? "result=CATALOG_COMPLETE_ONLY" : "result=INCONCLUSIVE");

                WriteNew(Path.Combine(qaRoot, ReportFile), result.Text.ToString());
                File.Move(Path.Combine(qaRoot, RunningFile), Path.Combine(qaRoot, DoneFile));
                claimed = false;
                Debug.Log("[MegaCrew IVA QA] One-shot read-only catalog finished: " +
                    (result.Complete ? "catalog complete" : "INCONCLUSIVE") + ". See " + ReportFile + ".");
            }
            catch (Exception ex)
            {
                finished = true;
                if (claimed)
                {
                    try {
                        if (!File.Exists(Path.Combine(qaRoot, ReportFile)))
                        WriteNew(Path.Combine(qaRoot, ReportFile), "probe_completed=false\nanimation_qualification=false\n" +
                            "result=INCONCLUSIVE\nerror=" + Safe(ex.GetType().Name + ": " + ex.Message) + "\n");
                    } catch { }
                    try
                    {
                        File.Move(Path.Combine(qaRoot, RunningFile), Path.Combine(qaRoot, FailedFile));
                    }
                    catch { }
                }
                Debug.LogWarning("[MegaCrew IVA QA] Catalog is INCONCLUSIVE; no animation was requested. " + ex.GetType().Name);
            }
        }

        bool TryClaim(string root)
        {
            if (!String.Equals(new DirectoryInfo(root).Name, ExpectedRootName, StringComparison.Ordinal) ||
                !ReadExact(Path.Combine(root, ScopeFile), ScopeContents) ||
                !ReadExact(Path.Combine(root, EnableFile), EnableContents) || ExistsAnyMarker(root)) return false;
            try
            {
                // Atomic move consumes the explicit one-shot enable token.
                File.Move(Path.Combine(root, EnableFile), Path.Combine(root, RunningFile));
                return true;
            }
            catch { return false; }
        }

        static bool ExistsAnyMarker(string root)
        {
            return File.Exists(Path.Combine(root, RunningFile)) || File.Exists(Path.Combine(root, DoneFile)) ||
                File.Exists(Path.Combine(root, FailedFile)) || File.Exists(Path.Combine(root, ReportFile));
        }

        static bool ReadExact(string path, string expected)
        {
            if (!File.Exists(path)) return false;
            byte[] actual = File.ReadAllBytes(path);
            byte[] expectedBytes = new UTF8Encoding(false).GetBytes(expected);
            if (actual.Length != expectedBytes.Length) return false;
            for (int i = 0; i < actual.Length; i++) if (actual[i] != expectedBytes[i]) return false;
            return true;
        }

        static bool Eligible(string root, out Vessel vessel, out string save, out Game game)
        {
            vessel = FlightGlobals.ActiveVessel;
            save = HighLogic.SaveFolder ?? "";
            game = HighLogic.CurrentGame;
            return String.Equals(Path.GetFullPath(KSPUtil.ApplicationRootPath), root, StringComparison.Ordinal) &&
                String.Equals(save, ExpectedSave, StringComparison.Ordinal) && game != null &&
                FlightGlobals.ready && HighLogic.LoadedSceneIsFlight && vessel != null && vessel.loaded && !vessel.packed && !vessel.isEVA &&
                !FlightDriver.Pause && TimeWarp.CurrentRateIndex == 0 && TimeWarp.CurrentRate == 1.0f &&
                CameraManager.Instance != null && CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA &&
                Finite(Planetarium.GetUniversalTime());
        }

        static bool SameEligibleContext(string root, Vessel expected, Guid vesselId, string save, Game game)
        {
            Vessel current;
            string currentSave;
            Game currentGame;
            return Eligible(root, out current, out currentSave, out currentGame) &&
                System.Object.ReferenceEquals(current, expected) && current.id == vesselId &&
                String.Equals(currentSave, save, StringComparison.Ordinal) && System.Object.ReferenceEquals(currentGame, game);
        }

        static void Enumerate(Vessel vessel, CaptureResult result)
        {
            result.Text.AppendLine("catalog_begin=true");
            foreach (Part part in vessel.parts)
            {
                if (part == null) { result.Complete = false; continue; }
                if (part.protoModuleCrew == null || part.protoModuleCrew.Count == 0) continue;
                InternalModel model = part.internalModel;
                if (model == null)
                {
                    result.Text.AppendLine("part=" + part.flightID.ToString(CultureInfo.InvariantCulture) + "; internal_model=unavailable");
                    result.Complete = false;
                    continue;
                }
                List<InternalSeat> seats = model.seats;
                if (seats == null) { result.Complete = false; continue; }
                foreach (ProtoCrewMember occupant in part.protoModuleCrew)
                {
                    if (occupant == null) { result.Complete = false; continue; }
                    bool frozen;
                    bool freezeStateKnown = TryFrozen(occupant.name, out frozen);
                    if (!freezeStateKnown) { result.Complete = false; result.Text.AppendLine("crew=" + Safe(occupant.name) + "; freeze_state=unknown"); }
                    if (freezeStateKnown && frozen) { result.FrozenCrewExcluded++; continue; }
                    int seatIndex = -1;
                    InternalSeat seat = null;
                    for (int i = 0; i < seats.Count; i++)
                    {
                        if (seats[i] != null && System.Object.ReferenceEquals(seats[i].crew, occupant))
                        { seatIndex = i; seat = seats[i]; break; }
                    }
                    result.OccupiedSeats++;
                    Kerbal rendered = seat == null ? null : seat.kerbalRef;
                    if (seat == null || !seat.taken || rendered == null || rendered.protoCrewMember == null ||
                        !System.Object.ReferenceEquals(rendered.protoCrewMember, occupant) ||
                        !System.Object.ReferenceEquals(rendered.InPart, part) ||
                        !System.Object.ReferenceEquals(occupant.seat, seat))
                    {
                        result.Text.AppendLine("seat=part:" + part.flightID.ToString(CultureInfo.InvariantCulture) +
                            ",index:" + (seatIndex < 0 ? "unknown" : seatIndex.ToString(CultureInfo.InvariantCulture)) + "; crew=" + Safe(occupant.name) +
                            "; rendered_kerbal=unavailable_or_mismatched");
                        result.Complete = false;
                        continue;
                    }

                    result.RenderedCrew++;
                    result.Text.AppendLine("seat=part:" + part.flightID.ToString(CultureInfo.InvariantCulture) +
                        ",index:" + seatIndex.ToString(CultureInfo.InvariantCulture) + "; crew=" + Safe(occupant.name) +
                        "; rendered_kerbal=" + Safe(rendered.name) + "; model=" + Safe(rendered.GetType().FullName));
                    Animator[] animators = rendered.Animators;
                    if (animators == null) { result.Complete = false; result.Text.AppendLine("animators=unavailable"); continue; }
                    if (animators.Length == 0) result.Complete = false;
                    for (int animatorIndex = 0; animatorIndex < animators.Length; animatorIndex++)
                        CaptureAnimator(animators[animatorIndex], animatorIndex, result);
                }
            }
            result.Text.AppendLine("catalog_end=true");
        }

        static bool TryFrozen(string crewName, out bool frozen)
        {
            frozen = false;
            try
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type bridge = assembly.GetType("MegaCrew.CryoBridge", false);
                    if (bridge == null) continue;
                    MethodInfo query = bridge.GetMethod("TryFrozen", BindingFlags.Public | BindingFlags.Static,
                        null, new[] { typeof(string), typeof(bool).MakeByRefType() }, null);
                    if (query == null || query.ReturnType != typeof(bool)) return false;
                    object[] arguments = { crewName, false };
                    bool known = (bool)query.Invoke(null, arguments);
                    if (!known || !(arguments[1] is bool)) return false;
                    frozen = (bool)arguments[1];
                    return true;
                }
            }
            catch { }
            return false;
        }

        static void CaptureAnimator(Animator animator, int index, CaptureResult result)
        {
            if (animator == null) { result.Complete = false; result.Text.AppendLine("animator=" + index + "; unavailable=true"); return; }
            result.AnimatorCount++;
            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            result.Text.AppendLine("animator=" + index.ToString(CultureInfo.InvariantCulture) +
                "; name=" + Safe(animator.name) + "; enabled=" + animator.enabled.ToString().ToLowerInvariant() +
                "; controller=" + Safe(controller == null ? "unavailable" : controller.GetType().FullName + ":" + controller.name));
            if (controller == null) { result.Complete = false; return; }
            AnimationClip[] clips = controller.animationClips;
            if (clips == null) { result.Complete = false; result.Text.AppendLine("clips=unavailable"); }
            else foreach (AnimationClip clip in clips)
            {
                if (clip == null) { result.Complete = false; continue; }
                result.Text.AppendLine("clip=" + Safe(clip.name) + "; length=" + clip.length.ToString("R", CultureInfo.InvariantCulture));
            }
            int layers = animator.layerCount;
            if (layers <= 0) { result.Complete = false; result.Text.AppendLine("layers=unavailable"); return; }
            for (int layer = 0; layer < layers; layer++)
            {
                string layerName = animator.GetLayerName(layer) ?? "";
                AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(layer);
                bool transitioning = animator.IsInTransition(layer);
                result.Text.AppendLine("layer=" + layer.ToString(CultureInfo.InvariantCulture) +
                    "; name=" + Safe(layerName) + "; current_hash=" + current.fullPathHash.ToString(CultureInfo.InvariantCulture) +
                    "; current_short_hash=" + current.shortNameHash.ToString(CultureInfo.InvariantCulture) +
                    "; in_transition=" + transitioning.ToString().ToLowerInvariant());
                if (transitioning)
                {
                    AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(layer);
                    result.Text.AppendLine("layer_next=" + layer.ToString(CultureInfo.InvariantCulture) +
                        "; next_hash=" + next.fullPathHash.ToString(CultureInfo.InvariantCulture) +
                        "; next_short_hash=" + next.shortNameHash.ToString(CultureInfo.InvariantCulture));
                }
                foreach (string candidate in CandidateNames)
                {
                    CheckState(animator, layer, layerName, candidate, result);
                    if (!String.IsNullOrEmpty(layerName)) CheckState(animator, layer, layerName, layerName + "." + candidate, result);
                }
            }
        }

        static void CheckState(Animator animator, int layer, string layerName, string candidate, CaptureResult result)
        {
            int hash = Animator.StringToHash(candidate);
            bool has = animator.HasState(layer, hash);
            result.Text.AppendLine("has_state=layer:" + layer.ToString(CultureInfo.InvariantCulture) +
                ",layer_name:" + Safe(layerName) + ",candidate:" + Safe(candidate) +
                ",hash:" + hash.ToString(CultureInfo.InvariantCulture) + ",value:" + has.ToString().ToLowerInvariant());
        }

        static void WriteNew(string path, string contents)
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(contents);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                stream.Write(bytes, 0, bytes.Length);
        }

        static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
        static string Safe(string value) { return (value ?? "").Replace("\r", " ").Replace("\n", " ").Replace(";", ","); }
    }
}
