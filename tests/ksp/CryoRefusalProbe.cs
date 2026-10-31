// Disposable QA assembly. Never include this source in the normal MegaCrew plugin.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using MegaCrew;

[KSPAddon(KSPAddon.Startup.MainMenu, false)]
public sealed class CryoRefusalProbe : MonoBehaviour
{
    const string SaveName = "MegaCrew Background QA";
    const string ScopeFile = "CryoRefusalQA.scope";
    const string ScopeText = "MEGACREW_DISPOSABLE_CRYO_REFUSAL_QA_V1\n";
    const string EnableFile = "CryoRefusalQA.enable";
    const string EnableText = "AUTHORIZE ONE ORIGINAL-PROVIDER CRYO REFUSAL QA FOR SAVE: MegaCrew Background QA\n";
    const string RunningFile = "CryoRefusalQA.running";
    const string DoneFile = "CryoRefusalQA.done";
    const string ReportFile = "CryoRefusalQA-result.cfg";
    const string ExpectedRoot = "KSP-Hermes-QA";
    const string ExpectedSaveSha = "6fd1a826062dafcdf02fa485c89ca818819ea5352bcfc5eca5f1bfde2c1bb4b9";
    const string ExpectedMegaCrewSha = "2d6fd46d353b1cac3b971deff623a2f7f24d2d52cb44cc605e01e8548c5ca986";
    const string ExpectedProviderSha = "ed956b9ec2cc5f414586a52a9086423e4c9125fa72097bce2721ea41eb075bc9";
    const int ExpectedPartCount = 17;
    const int ExpectedCrewCount = 4;

    string root;
    bool scoped;
    bool fixtureLoaded;
    bool flightObserved;
    bool requestCalled;
    bool requestAccepted;
    bool requestViolation;
    Guid intendedVessel;
    Game loadedGame;
    string pinnedSaveSha;
    ConfigNode report = new ConfigNode("CRYO_REFUSAL_QA");

    sealed class InconclusiveException : Exception
    {
        public InconclusiveException(string message) : base(message) { }
    }

    IEnumerator Start()
    {
        try
        {
            root = Path.GetFullPath(KSPUtil.ApplicationRootPath);
            if (!AuthorizedBeforeRun()) yield break;
            File.Move(Path.Combine(root, EnableFile), Path.Combine(root, RunningFile));
            scoped = true;
            DontDestroyOnLoad(this);
            Record("schema", 1);
            Record("run_state", "RUNNING");
            Record("probe_completed", false);
            Record("flight_qualification", false);
            Record("refusal_scope", "direct crew-not-colocated-with-pod");
            Record("ui_gate_tested", false);
            Record("transfer_busy_tested", false);
            Record("stage", "preflight_hashes");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[CryoRefusalQA] Not authorized or unable to claim the one-shot enable token: " + ex.GetType().Name);
            yield break;
        }

        IEnumerator steps = Run();
        while (true)
        {
            object wait = null;
            bool more = false;
            Exception failure = null;
            try
            {
                ValidateContext();
                more = steps.MoveNext();
                if (more) wait = steps.Current;
            }
            catch (Exception ex) { failure = ex; }

            if (failure != null)
            {
                bool inconclusive = failure is InconclusiveException && !requestViolation;
                Record("freeze_request_called", requestCalled);
                Record("run_state", inconclusive ? "INCONCLUSIVE" : "FAILED");
                Record("result", requestAccepted ? "UNEXPECTED_FREEZE_ACCEPTANCE" : requestViolation ? "UNEXPECTED_DISPATCH_MUTATION" : inconclusive ? "INCONCLUSIVE" : "FAILED");
                Record("failure", failure.GetType().Name + ": " + failure.Message);
                Record("probe_completed", false);
                Finish();
                yield break;
            }
            if (!more)
            {
                Finish();
                yield break;
            }
            yield return wait;
        }
    }

    bool AuthorizedBeforeRun()
    {
        if (!String.Equals(new DirectoryInfo(root).Name, ExpectedRoot, StringComparison.Ordinal)) return false;
        if (!ExactFile(Path.Combine(root, ScopeFile), ScopeText) || !ExactFile(Path.Combine(root, EnableFile), EnableText)) return false;
        foreach (string name in new[] { RunningFile, DoneFile, ReportFile })
            if (File.Exists(Path.Combine(root, name))) return false;
        return true;
    }

    static bool ExactFile(string path, string expected)
    {
        if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return false;
        byte[] actual = File.ReadAllBytes(path);
        byte[] wanted = new System.Text.UTF8Encoding(false).GetBytes(expected);
        if (actual.Length != wanted.Length) return false;
        for (int i = 0; i < actual.Length; i++) if (actual[i] != wanted[i]) return false;
        return true;
    }

    IEnumerator Run()
    {
        RequireFixtureAndStaticInputs();
        Record("save_sha256_before", HashForObservation(SavePath, "pinned QA save"));
        Record("megacrew_sha256_before", HashForObservation(MegaCrewPath, "MegaCrew DLL"));
        Record("provider_sha256_before", HashForObservation(ProviderPath, "BackgroundResources DLL"));
        Record("provider_variant", "original only");

        RequireMenuContext();
        Game game = GamePersistence.LoadGame("persistent", SaveName, true, false);
        if (game == null || game.flightState == null || game.flightState.protoVessels == null || game.flightState.protoVessels.Count != 1)
            throw new InconclusiveException("Expected exactly one vessel in the pinned pristine QA fixture.");
        intendedVessel = game.flightState.protoVessels[0].vesselID;
        Record("fixture_vessel_guid", intendedVessel.ToString("D"));
        Record("stage", "loading_exact_fixture");
        RequireFixtureAndStaticInputs();
        HighLogic.CurrentGame = game;
        GamePersistence.UpdateScenarioModules(game);
        Record("scenario_module_update_called", true);
        Record("scenario_module_count_after_update", game.scenarios == null ? -1 : game.scenarios.Count);
        Record("scenario_module_names_after_update", ScenarioModuleNames(game));

        // This is the ordinary, native save step in KSP's load-game menu pipeline.
        // The exact disposable clone is the only target; the pristine template hash
        // was checked above and is intentionally not claimed unchanged afterward.
        GamePersistence.SaveGame(game, "persistent", SaveName, SaveMode.OVERWRITE);
        Record("native_lifecycle_save_performed", true);
        GameEvents.onGameStatePostLoad.Fire(game.config);
        Record("postload_event_fired", true);
        HighLogic.SaveFolder = SaveName;
        loadedGame = game;
        fixtureLoaded = true;
        game.Start();
        Record("game_start_called", true);

        float flightDeadline = Time.realtimeSinceStartup + 180f;
        while (!FlightGlobals.ready || !HighLogic.LoadedSceneIsFlight || FlightGlobals.ActiveVessel == null ||
               !FlightGlobals.ActiveVessel.loaded || FlightGlobals.ActiveVessel.packed)
        {
            if (Time.realtimeSinceStartup > flightDeadline)
                throw new InconclusiveException("Flight did not become ready before timeout.");
            yield return new WaitForSecondsRealtime(1f);
        }
        Record("flight_pause_at_readiness", FlightDriver.Pause);
        Record("flight_time_warp_rate_index_at_readiness", TimeWarp.CurrentRateIndex);
        Record("flight_time_warp_rate_at_readiness", TimeWarp.CurrentRate);
        flightObserved = true;
        ValidateContext();
        IEnumerator observation = ObserveLoadedFixture();
        while (observation.MoveNext()) yield return observation.Current;
    }

    IEnumerator ObserveLoadedFixture()
    {
        Vessel vessel = FlightGlobals.ActiveVessel;
        ValidateContext();
        if (vessel.id != intendedVessel) throw new InconclusiveException("The focused vessel did not match the pristine fixture GUID.");
        if (vessel.parts == null || vessel.parts.Count != ExpectedPartCount || vessel.GetCrewCount() != ExpectedCrewCount)
            throw new InconclusiveException("Loaded vessel did not match the 17-part, four-awake-crew fixture.");
        Record("loaded_parts", vessel.parts.Count);
        Record("loaded_awake_crew", vessel.GetCrewCount());

        Record("stage", "waiting_for_deepfreeze_ready_idle");
        float apiDeadline = Time.realtimeSinceStartup + 90f;
        while (CryoBridge.Status != "DeepFreeze connected" || CryoBridge.Busy)
        {
            ValidateContext();
            if (Time.realtimeSinceStartup > apiDeadline)
                throw new InconclusiveException("DeepFreeze API readiness or idle-state timeout.");
            Record("deepfreeze_status", CryoBridge.Status);
            Record("deepfreeze_busy", CryoBridge.Busy);
            yield return new WaitForSecondsRealtime(1f);
        }
        Record("deepfreeze_status", CryoBridge.Status);
        Record("deepfreeze_busy", CryoBridge.Busy);

        double startUt = RequireUt();
        float realDeadline = Time.realtimeSinceStartup + 30f;
        double lastUt = startUt;
        while (lastUt - startUt < 5.0)
        {
            ValidateContext();
            lastUt = RequireUt();
            if (lastUt < startUt) throw new InconclusiveException("Universal time moved backwards during 1x observation.");
            if (Time.realtimeSinceStartup > realDeadline)
                throw new InconclusiveException("Universal time did not advance five seconds during the real-time observation.");
            yield return new WaitForSecondsRealtime(0.5f);
        }
        Record("preflight_ut_start", startUt);
        Record("preflight_ut_after_five_seconds", lastUt);
        Record("preflight_ut_advance", lastUt - startUt);

        ValidateContext();
        RequireStableBinaries();
        if (vessel != FlightGlobals.ActiveVessel || vessel.id != intendedVessel || vessel.parts.Count != ExpectedPartCount || vessel.GetCrewCount() != ExpectedCrewCount)
            throw new InconclusiveException("Vessel or crew context changed before refusal request.");
        if (CryoBridge.Status != "DeepFreeze connected" || CryoBridge.Busy)
            throw new InconclusiveException("DeepFreeze was not connected and idle immediately before request.");

        ProtoCrewMember ada;
        Part adaPart;
        if (!FindAboardAdaOutsideFreezers(vessel, out ada, out adaPart))
            throw new InconclusiveException("Ada was not found exactly once aboard outside every DeepFreezer part.");
        bool frozen;
        bool frozenCrewKnown;
        bool allFourAwake;
        string frozenCrewBefore = FrozenCrewSnapshot(out frozenCrewKnown, out allFourAwake);
        bool adaFrozenKnown = CryoBridge.TryFrozen("Ada Kerman", out frozen);
        if (!adaFrozenKnown || !frozenCrewKnown || !allFourAwake)
            throw new InconclusiveException("The fixture does not have four crew with known unfrozen status.");
        if (frozen) throw new InconclusiveException("Ada was already recorded frozen before the request.");
        Record("ada_part_id_before", adaPart.flightID);
        Record("ada_crew_ref_before", ada.name);
        Record("ada_frozen_before_known", adaFrozenKnown);
        Record("ada_frozen_before", frozen);
        Record("parts_before", vessel.parts.Count);
        Record("awake_crew_before", vessel.GetCrewCount());
        string crewAssignmentsBefore = CrewAssignmentSnapshot(vessel);
        string freezerPartsBefore = FreezerPartSnapshot(vessel);
        int freezerCountBefore = CountFreezers(vessel);
        if (freezerCountBefore != 1)
            throw new InconclusiveException("Fixture did not contain the expected single real DeepFreezer module.");
        string resourcesBefore = ResourceSnapshot(vessel);
        SortedDictionary<string, double> resourceTotalsBefore = ResourceTotals(vessel);
        Record("crew_assignments_before", crewAssignmentsBefore);
        Record("deepfreezer_parts_before", freezerPartsBefore);
        Record("deepfreezer_count_before", freezerCountBefore);
        Record("frozen_crew_states_before", frozenCrewBefore);
        Record("deepfreeze_busy_immediately_before_request", CryoBridge.Busy);
        Record("resources_before_request", resourcesBefore);
        Record("stage", "single_refusal_request");

        // Exactly one call. Never retry or compensate if the guard unexpectedly accepts.
        ValidateContext();
        if (CryoBridge.Busy || CryoBridge.Status != "DeepFreeze connected")
            throw new InconclusiveException("DeepFreeze readiness changed immediately before request.");
        bool frozenImmediatelyBefore;
        if (!CryoBridge.TryFrozen("Ada Kerman", out frozenImmediatelyBefore) || frozenImmediatelyBefore)
            throw new InconclusiveException("Ada's unfrozen state could not be freshly confirmed before request.");
        double requestUt = RequireUt();
        requestCalled = true;
        Record("freeze_request_called", true);
        bool accepted = CryoBridge.Freeze("Ada Kerman");
        requestAccepted = accepted;
        Record("freeze_request_ut", requestUt);
        Record("freeze_request_returned_true", accepted);
        if (accepted)
        {
            requestViolation = true;
            Record("run_state", "FAILED");
            Record("result", "UNEXPECTED_FREEZE_ACCEPTANCE");
        }

        // Same-frame reads catch direct dispatch side effects; later resource deltas
        // are observational only because normal EC/needs consumption continues at 1x.
        string crewAssignmentsImmediate = CrewAssignmentSnapshot(vessel);
        string freezerPartsImmediate = FreezerPartSnapshot(vessel);
        int freezerCountImmediate = CountFreezers(vessel);
        string resourcesImmediate = ResourceSnapshot(vessel);
        bool frozenCrewImmediateKnown, allFourAwakeImmediate;
        string frozenCrewImmediate = FrozenCrewSnapshot(out frozenCrewImmediateKnown, out allFourAwakeImmediate);
        bool busyImmediate = CryoBridge.Busy;
        bool frozenImmediateKnown = CryoBridge.TryFrozen("Ada Kerman", out frozen);
        ProtoCrewMember adaAfterPlaceholder;
        Part adaPartPlaceholder;
        bool immediateChanged = !String.Equals(crewAssignmentsBefore, crewAssignmentsImmediate, StringComparison.Ordinal) ||
            !String.Equals(freezerPartsBefore, freezerPartsImmediate, StringComparison.Ordinal) ||
            freezerCountBefore != freezerCountImmediate ||
            !String.Equals(resourcesBefore, resourcesImmediate, StringComparison.Ordinal) ||
            vessel.parts.Count != ExpectedPartCount || vessel.GetCrewCount() != ExpectedCrewCount ||
            !FindAboardAda(vessel, out adaAfterPlaceholder, out adaPartPlaceholder) ||
            !System.Object.ReferenceEquals(adaAfterPlaceholder, ada) || adaPartPlaceholder.flightID != adaPart.flightID ||
            (frozenImmediateKnown && frozen) ||
            (frozenCrewImmediateKnown && (frozenCrewBefore != frozenCrewImmediate || !allFourAwakeImmediate));
        Record("crew_assignments_immediate_after", crewAssignmentsImmediate);
        Record("deepfreezer_parts_immediate_after", freezerPartsImmediate);
        Record("deepfreezer_count_immediate_after", freezerCountImmediate);
        Record("deepfreeze_busy_immediate_after", busyImmediate);
        Record("deepfreeze_status_immediate_after", CryoBridge.Status);
        Record("ada_frozen_immediate_known", frozenImmediateKnown);
        Record("ada_frozen_immediate_after", frozenImmediateKnown && frozen);
        Record("frozen_crew_states_immediate_after", frozenCrewImmediate);
        Record("frozen_crew_states_immediate_after_known", frozenCrewImmediateKnown);
        Record("resources_immediate_after", resourcesImmediate);
        Record("immediate_dispatch_state_changed", immediateChanged);
        bool immediateReadUnknown = !frozenImmediateKnown || !frozenCrewImmediateKnown || busyImmediate || CryoBridge.Status != "DeepFreeze connected";
        Record("immediate_read_state_unknown", immediateReadUnknown);
        if (immediateChanged)
        {
            requestViolation = true;
            if (!accepted)
            {
                Record("run_state", "FAILED");
                Record("result", "UNEXPECTED_DISPATCH_MUTATION");
            }
        }

        double observeStart = requestUt;
        realDeadline = Time.realtimeSinceStartup + 30f;
        lastUt = observeStart;
        while (lastUt - observeStart < 5.0)
        {
            ValidateContext();
            lastUt = RequireUt();
            if (lastUt < observeStart) throw new InconclusiveException("Universal time moved backwards during post-request observation.");
            if (Time.realtimeSinceStartup > realDeadline)
                throw new InconclusiveException("Universal time did not advance five seconds after the single request.");
            yield return new WaitForSecondsRealtime(0.5f);
        }

        Record("post_request_ut", lastUt);
        Record("post_request_ut_advance", lastUt - observeStart);
        ValidateContext();
        RequireStableBinaries();
        Part adaPartAfter;
        ProtoCrewMember adaAfter;
        bool adaAboardAfter = FindAboardAda(vessel, out adaAfter, out adaPartAfter);
        bool frozenAfterKnown = CryoBridge.TryFrozen("Ada Kerman", out frozen);
        bool frozenCrewAfterKnown, allFourAwakeAfter;
        string frozenCrewAfter = FrozenCrewSnapshot(out frozenCrewAfterKnown, out allFourAwakeAfter);
        bool busyAfter = CryoBridge.Busy;
        int finalParts = vessel.parts == null ? -1 : vessel.parts.Count;
        int finalCrew = vessel.GetCrewCount();
        string crewAssignmentsAfter = CrewAssignmentSnapshot(vessel);
        string freezerPartsAfter = FreezerPartSnapshot(vessel);
        int freezerCountAfter = CountFreezers(vessel);
        string resourcesAfter = ResourceSnapshot(vessel);
        SortedDictionary<string, double> resourceTotalsAfter = ResourceTotals(vessel);
        string saveShaAfter = HashForObservation(SavePath, "QA save after observation");
        string megaCrewShaAfter = HashForObservation(MegaCrewPath, "MegaCrew DLL after observation");
        string providerShaAfter = HashForObservation(ProviderPath, "BackgroundResources DLL after observation");
        Record("ada_aboard_after", adaAboardAfter);
        Record("ada_part_id_after", adaAboardAfter ? adaPartAfter.flightID.ToString(CultureInfo.InvariantCulture) : "");
        Record("ada_frozen_after_known", frozenAfterKnown);
        Record("ada_frozen_after", frozenAfterKnown && frozen);
        Record("parts_after", finalParts);
        Record("awake_crew_after", finalCrew);
        Record("crew_assignments_after", crewAssignmentsAfter);
        Record("deepfreezer_parts_after", freezerPartsAfter);
        Record("deepfreezer_count_after", freezerCountAfter);
        Record("deepfreeze_busy_after", busyAfter);
        Record("deepfreeze_status_after", CryoBridge.Status);
        Record("frozen_crew_states_after", frozenCrewAfter);
        Record("frozen_crew_states_after_known", frozenCrewAfterKnown);
        Record("resources_after_five_seconds", resourcesAfter);
        Record("resource_totals_delta_after_five_seconds", ResourceTotalsDelta(resourceTotalsBefore, resourceTotalsAfter));
        Record("resource_delta_cause_inferred", false);
        Record("save_sha256_after_observation", saveShaAfter);
        Record("save_unchanged", String.Equals(pinnedSaveSha, saveShaAfter, StringComparison.OrdinalIgnoreCase));
        Record("save_qualification", false);
        Record("megacrew_sha256_after", megaCrewShaAfter);
        Record("provider_sha256_after", providerShaAfter);

        bool adaAssignmentChanged = !adaAboardAfter || !System.Object.ReferenceEquals(adaAfter, ada) ||
            (adaAboardAfter && adaPartAfter.flightID != adaPart.flightID);
        bool actualStateChanged = adaAssignmentChanged || finalParts != ExpectedPartCount || finalCrew != ExpectedCrewCount ||
            crewAssignmentsBefore != crewAssignmentsAfter || freezerPartsBefore != freezerPartsAfter ||
            freezerCountBefore != freezerCountAfter || (frozenAfterKnown && frozen) ||
            (frozenCrewAfterKnown && (frozenCrewBefore != frozenCrewAfter || !allFourAwakeAfter));
        bool readUnknown = !frozenAfterKnown || !frozenCrewAfterKnown || busyAfter ||
            CryoBridge.Status != "DeepFreeze connected" || megaCrewShaAfter != ExpectedMegaCrewSha || providerShaAfter != ExpectedProviderSha;
        if (accepted || immediateChanged || actualStateChanged)
        {
            requestViolation = true;
            Record("run_state", "FAILED");
            Record("result", accepted ? "UNEXPECTED_FREEZE_ACCEPTANCE" : "POST_REQUEST_STATE_CHANGED");
            Record("probe_completed", true);
            yield break;
        }
        if (immediateReadUnknown || readUnknown)
            throw new InconclusiveException("Post-request cryo/readiness state could not be verified; no pass recorded.");

        Record("run_state", "COMPLETED");
        Record("result", "REFUSED_AS_EXPECTED");
        Record("stage", "procedure_finished");
        Record("probe_completed", true);
    }

    void RequireFixtureAndStaticInputs()
    {
        string saveSha = HashForObservation(SavePath, "pinned QA save");
        string crewSha = HashForObservation(MegaCrewPath, "MegaCrew DLL");
        string providerSha = HashForObservation(ProviderPath, "BackgroundResources DLL");
        if (saveSha != ExpectedSaveSha) throw new InconclusiveException("QA save SHA-256 mismatch; no cryo request made.");
        if (pinnedSaveSha != null && !String.Equals(pinnedSaveSha, saveSha, StringComparison.OrdinalIgnoreCase))
            throw new InconclusiveException("Pinned QA save changed before the fixture was loaded.");
        pinnedSaveSha = saveSha;
        if (crewSha != ExpectedMegaCrewSha) throw new InconclusiveException("MegaCrew provider SHA-256 mismatch; no cryo request made.");
        if (providerSha != ExpectedProviderSha) throw new InconclusiveException("BackgroundResources is not the pinned original provider; no cryo request made.");
        RequireStableBinaries();
    }

    void RequireStableBinaries()
    {
        string diskHash = HashForObservation(MegaCrewPath, "MegaCrew DLL");
        string providerHash = HashForObservation(ProviderPath, "BackgroundResources DLL");
        string loadedPath = typeof(CryoBridge).Assembly.Location;
        if (diskHash != ExpectedMegaCrewSha || providerHash != ExpectedProviderSha)
            throw new InconclusiveException("Pinned MegaCrew or original BackgroundResources hash changed.");
        if (String.IsNullOrEmpty(loadedPath) ||
            !String.Equals(Path.GetFullPath(loadedPath), Path.GetFullPath(MegaCrewPath), StringComparison.Ordinal) ||
            HashForObservation(loadedPath, "loaded MegaCrew assembly") != ExpectedMegaCrewSha)
            throw new InconclusiveException("The loaded CryoBridge assembly is not the pinned MegaCrew DLL.");
        Record("loaded_megacrew_assembly_path", loadedPath);
        Record("loaded_megacrew_assembly_sha256", HashForObservation(loadedPath, "loaded MegaCrew assembly"));
    }

    void RequireMenuContext()
    {
        if (HighLogic.LoadedScene != GameScenes.MAINMENU)
            throw new InconclusiveException("Probe must load the explicit QA fixture from the main menu.");
        Record("menu_pause_observed", FlightDriver.Pause);
        Record("menu_time_warp_rate_index_observed", TimeWarp.CurrentRateIndex);
        Record("menu_time_warp_rate_observed", TimeWarp.CurrentRate);
    }

    void ValidateContext()
    {
        if (!scoped || !fixtureLoaded) return;
        if (!String.Equals(Path.GetFullPath(KSPUtil.ApplicationRootPath), root, StringComparison.Ordinal) ||
            HighLogic.SaveFolder != SaveName || !System.Object.ReferenceEquals(HighLogic.CurrentGame, loadedGame))
            throw new InconclusiveException("QA root or save context changed.");
        // Scene load/FlightGlobals.ready may legitimately lag Game.Start().
        // The dedicated readiness loop has its own timeout; do not reject that transition.
        if (!flightObserved) return;
        if (FlightDriver.Pause || TimeWarp.CurrentRateIndex != 0)
            throw new InconclusiveException("Probe requires unpaused 1x with no time warp after flight initialization.");
        if (!HighLogic.LoadedSceneIsFlight || !FlightGlobals.ready || FlightGlobals.ActiveVessel == null ||
            !FlightGlobals.ActiveVessel.loaded || FlightGlobals.ActiveVessel.packed)
            throw new InconclusiveException("Flight readiness or loaded-vessel context was lost after initialization.");
        if (TimeWarp.CurrentRate != 1.0f || FlightGlobals.ActiveVessel.id != intendedVessel)
            throw new InconclusiveException("The loaded active vessel or actual time rate changed.");
        RequireUt();
    }

    static double RequireUt()
    {
        double ut = Planetarium.GetUniversalTime();
        if (Double.IsNaN(ut) || Double.IsInfinity(ut) || ut <= 0)
            throw new InconclusiveException("Universal time is not finite and positive.");
        return ut;
    }

    static bool FindAboardAdaOutsideFreezers(Vessel vessel, out ProtoCrewMember ada, out Part adaPart)
    {
        ada = null; adaPart = null; int occurrences = 0;
        foreach (Part part in vessel.parts)
        {
            if (part == null || part.protoModuleCrew == null) continue;
            foreach (ProtoCrewMember member in part.protoModuleCrew)
                if (member != null && member.name == "Ada Kerman")
                {
                    occurrences++; ada = member; adaPart = part;
                    if (part.Modules == null) throw new InconclusiveException("Ada's part module list is unavailable.");
                    foreach (PartModule module in part.Modules)
                        if (module != null && module.moduleName == "DeepFreezer") return false;
                }
        }
        return occurrences == 1 && ada != null && adaPart != null;
    }

    static bool FindAboardAda(Vessel vessel, out ProtoCrewMember ada, out Part adaPart)
    {
        ada = null; adaPart = null; int occurrences = 0;
        foreach (Part part in vessel.parts)
        {
            if (part == null || part.protoModuleCrew == null) continue;
            foreach (ProtoCrewMember member in part.protoModuleCrew)
                if (member != null && member.name == "Ada Kerman") { occurrences++; ada = member; adaPart = part; }
        }
        return occurrences == 1 && ada != null && adaPart != null;
    }

    static string CrewAssignmentSnapshot(Vessel vessel)
    {
        if (vessel == null || vessel.parts == null) throw new InconclusiveException("Crew manifest is unavailable.");
        var entries = new List<string>();
        foreach (Part part in vessel.parts)
        {
            if (part == null || part.protoModuleCrew == null) throw new InconclusiveException("A part crew manifest is unavailable.");
            foreach (ProtoCrewMember member in part.protoModuleCrew)
            {
                if (member == null) throw new InconclusiveException("A crew manifest entry is null.");
                entries.Add(Safe(member.name) + "=" + part.flightID.ToString(CultureInfo.InvariantCulture));
            }
        }
        entries.Sort(StringComparer.Ordinal);
        return String.Join("|", entries.ToArray());
    }

    static string ScenarioModuleNames(Game game)
    {
        if (game == null || game.scenarios == null) return "unknown";
        var types = new List<string>();
        foreach (ProtoScenarioModule scenario in game.scenarios)
            types.Add(scenario == null ? "null" : scenario.moduleName ?? "unknown");
        types.Sort(StringComparer.Ordinal);
        return String.Join("|", types.ToArray());
    }

    static string FreezerPartSnapshot(Vessel vessel)
    {
        if (vessel == null || vessel.parts == null) throw new InconclusiveException("Part list is unavailable for cryopod snapshot.");
        var entries = new List<string>();
        foreach (Part part in vessel.parts)
        {
            if (part == null || part.Modules == null) throw new InconclusiveException("Part modules are unavailable for cryopod snapshot.");
            for (int i = 0; i < part.Modules.Count; i++)
            {
                PartModule module = part.Modules[i];
                if (module != null && module.moduleName == "DeepFreezer")
                {
                    var crew = new List<string>();
                    if (part.protoModuleCrew == null) throw new InconclusiveException("Cryopod part crew is unavailable.");
                    foreach (ProtoCrewMember member in part.protoModuleCrew)
                        if (member != null) crew.Add(Safe(member.name));
                    crew.Sort(StringComparer.Ordinal);
                    entries.Add(part.flightID.ToString(CultureInfo.InvariantCulture) + ":" +
                        i.ToString(CultureInfo.InvariantCulture) + ":" + String.Join(",", crew.ToArray()));
                }
            }
        }
        entries.Sort(StringComparer.Ordinal);
        return String.Join("|", entries.ToArray());
    }

    static int CountFreezers(Vessel vessel)
    {
        if (vessel == null || vessel.parts == null) throw new InconclusiveException("Part list is unavailable for cryopod count.");
        int count = 0;
        foreach (Part part in vessel.parts)
        {
            if (part == null || part.Modules == null) throw new InconclusiveException("Part modules are unavailable for cryopod count.");
            foreach (PartModule module in part.Modules) if (module != null && module.moduleName == "DeepFreezer") count++;
        }
        return count;
    }

    static readonly string[] MissionCrewNames = { "Ada Kerman", "Basil Kerman", "Hamish Kerman", "Vikram Kerman" };
    static string FrozenCrewSnapshot(out bool known, out bool allAwake)
    {
        known = true; allAwake = true;
        var entries = new List<string>();
        foreach (string name in MissionCrewNames)
        {
            bool frozen;
            bool read = CryoBridge.TryFrozen(name, out frozen);
            if (!read) known = false;
            else if (frozen) allAwake = false;
            entries.Add(name + "=" + (read ? (frozen ? "frozen" : "awake") : "unknown"));
        }
        return String.Join("|", entries.ToArray());
    }

    static string ResourceSnapshot(Vessel vessel)
    {
        if (vessel == null || vessel.parts == null) throw new InconclusiveException("Vessel resource list is unavailable.");
        var entries = new List<string>();
        foreach (Part part in vessel.parts)
        {
            if (part == null || part.Resources == null) throw new InconclusiveException("Part resource list is unavailable.");
            foreach (PartResource resource in part.Resources)
            {
                if (resource == null) throw new InconclusiveException("A part resource entry is null.");
                ValidateResource(resource);
                entries.Add(part.flightID.ToString(CultureInfo.InvariantCulture) + ":" + Safe(resource.resourceName) + ":" +
                    resource.amount.ToString("R", CultureInfo.InvariantCulture) + ":" +
                    resource.maxAmount.ToString("R", CultureInfo.InvariantCulture) + ":" +
                    resource.flowState.ToString().ToLowerInvariant());
            }
        }
        entries.Sort(StringComparer.Ordinal);
        return String.Join("|", entries.ToArray());
    }

    static SortedDictionary<string, double> ResourceTotals(Vessel vessel)
    {
        var totals = new SortedDictionary<string, double>(StringComparer.Ordinal);
        if (vessel == null || vessel.parts == null) throw new InconclusiveException("Vessel resource totals are unavailable.");
        foreach (Part part in vessel.parts)
        {
            if (part == null || part.Resources == null) throw new InconclusiveException("Part resource totals are unavailable.");
            foreach (PartResource resource in part.Resources)
            {
                if (resource == null) throw new InconclusiveException("A part resource total is null.");
                ValidateResource(resource);
                double current;
                totals.TryGetValue(resource.resourceName, out current);
                double total = current + resource.amount;
                if (Double.IsNaN(total) || Double.IsInfinity(total)) throw new InconclusiveException("Aggregated resource total is not finite.");
                totals[resource.resourceName] = total;
            }
        }
        return totals;
    }

    static string ResourceTotalsDelta(SortedDictionary<string, double> before, SortedDictionary<string, double> after)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string name in before.Keys) names.Add(name);
        foreach (string name in after.Keys) names.Add(name);
        var rows = new List<string>();
        foreach (string name in names)
        {
            double oldValue, newValue;
            before.TryGetValue(name, out oldValue);
            after.TryGetValue(name, out newValue);
            rows.Add(name + ":before=" + oldValue.ToString("R", CultureInfo.InvariantCulture) +
                ",after=" + newValue.ToString("R", CultureInfo.InvariantCulture) +
                ",delta=" + (newValue - oldValue).ToString("R", CultureInfo.InvariantCulture));
        }
        return String.Join("|", rows.ToArray());
    }

    static void ValidateResource(PartResource resource)
    {
        if (String.IsNullOrEmpty(resource.resourceName) ||
            Double.IsNaN(resource.amount) || Double.IsInfinity(resource.amount) ||
            Double.IsNaN(resource.maxAmount) || Double.IsInfinity(resource.maxAmount) ||
            resource.amount < 0 || resource.maxAmount < 0 || resource.amount > resource.maxAmount)
            throw new InconclusiveException("Invalid or out-of-range resource observation for " + Safe(resource.resourceName) + ".");
    }

    void Record(string key, object value)
    {
        string text = value is IFormattable
            ? ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture)
            : (value == null ? "" : value.ToString());
        report.SetValue(key, text, true);
        if (scoped) report.Save(Path.Combine(root, ReportFile));
        Debug.Log("[CryoRefusalQA] " + key + "=" + text);
    }

    void Finish()
    {
        if (!scoped) return;
        if (!report.HasValue("run_state")) Record("run_state", "INCONCLUSIVE");
        if (!report.HasValue("probe_completed")) Record("probe_completed", false);
        Record("flight_qualification", false);
        report.Save(Path.Combine(root, ReportFile));
        string running = Path.Combine(root, RunningFile);
        if (File.Exists(running)) File.Move(running, Path.Combine(root, DoneFile));
        scoped = false;
        // Only this explicitly scoped disposable app is closed after its report is written.
        Application.Quit();
    }

    void OnDestroy()
    {
        if (scoped) Finish();
    }

    static string Hash(string path)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    static string HashForObservation(string path, string label)
    {
        try { return Hash(path); }
        catch (Exception ex) { throw new InconclusiveException("Could not read " + label + " hash (" + ex.GetType().Name + ")."); }
    }

    string SavePath { get { return Path.Combine(root, "saves", SaveName, "persistent.sfs"); } }
    string MegaCrewPath { get { return Path.Combine(root, "GameData", "MegaCrew", "Plugins", "MegaCrew.dll"); } }
    string ProviderPath { get { return Path.Combine(root, "GameData", "REPOSoftTech", "BackgroundResources", "BackgroundResources.dll"); } }

    static string Safe(string value) { return (value ?? "").Replace("\r", " ").Replace("\n", " ").Replace("|", "/"); }
}
