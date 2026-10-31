// QA-only assembly. NEVER include in the normal MegaCrew plugin.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using MegaCrew;
using SaveUpgradePipeline;

[KSPAddon(KSPAddon.Startup.MainMenu, false)]
public sealed class HermesCryoProbe : MonoBehaviour
{
    const string SaveName = "MegaCrew Background QA";
    const int ExpectedParts = 17;
    const int ExpectedCrew = 4;
    const int ExpectedScenarioModules = 13;

    enum ScenePhase
    {
        Menu,
        StartingFlight,
        Flight,
        ToTrackingStation,
        TrackingStation,
        TrackingStationUnloaded,
        ToMenu
    }

    string root;
    ConfigNode report = new ConfigNode("HERMES_CRYO_QA");
    bool scoped;
    bool fixtureLoaded;
    Game expectedGame;
    Guid intendedVessel;
    ScenePhase phase = ScenePhase.Menu;

    sealed class ProbeException : Exception
    {
        public ProbeException(string message) : base(message) { }
    }

    sealed class ProbeInconclusiveException : Exception
    {
        public ProbeInconclusiveException(string message) : base(message) { }
    }

    IEnumerator Start()
    {
        root = Path.GetFullPath(KSPUtil.ApplicationRootPath);
        string scope = Path.Combine(root, "HermesCryoQA.scope");
        string marker = Path.Combine(root, "HermesCryoQA.enable");
        if (new DirectoryInfo(root).Name != "KSP-Hermes-QA" || !File.Exists(scope) ||
            File.ReadAllText(scope).Trim() != "MEGACREW_DISPOSABLE_CRYO_QA_V1" || !File.Exists(marker))
            yield break;
        foreach (string suffix in new[] { "running", "done" })
            if (File.Exists(Path.Combine(root, "HermesCryoQA." + suffix))) yield break;
        if (File.Exists(Path.Combine(root, "HermesCryoQA-result.cfg"))) yield break;

        scoped = true;
        DontDestroyOnLoad(this);
        File.Move(marker, Path.Combine(root, "HermesCryoQA.running"));
        IEnumerator steps = Run();
        while (true)
        {
            object next = null;
            bool more = false;
            Exception failure = null;
            try
            {
                ValidateContext();
                more = steps.MoveNext();
                if (more) next = steps.Current;
            }
            catch (Exception ex) { failure = ex; }

            if (failure != null)
            {
                Record("failure", failure.GetType().Name + ": " + failure.Message);
                Finish(false, failure is ProbeInconclusiveException);
                yield break;
            }
            if (!more) { Finish(true); yield break; }
            yield return next;
        }
    }

    void ValidateContext()
    {
        if (!String.Equals(Path.GetFullPath(KSPUtil.ApplicationRootPath), root, StringComparison.Ordinal))
            throw new ProbeException("QA root changed.");
        if (!fixtureLoaded) return;
        if (HighLogic.SaveFolder != SaveName || !System.Object.ReferenceEquals(HighLogic.CurrentGame, expectedGame))
            throw new ProbeException("QA save/game binding changed.");

        switch (phase)
        {
            case ScenePhase.Menu:
                if (HighLogic.LoadedScene != GameScenes.MAINMENU)
                    throw new ProbeException("Expected the main menu before loading the frozen save.");
                break;
            case ScenePhase.Flight:
                RequireFlightContext();
                break;
            case ScenePhase.TrackingStation:
                RequireTrackingStationContext();
                break;
            case ScenePhase.TrackingStationUnloaded:
                RequireTrackingStationContext();
                Vessel unloaded = FindVessel(intendedVessel);
                if (unloaded == null || unloaded.loaded)
                    throw new ProbeException("The intended vessel is no longer present as an unloaded tracking-station vessel.");
                break;
            // The following phases explicitly permit only the scene transition
            // they name. Their caller has a bounded wait and checks the target
            // scene before advancing to its stable phase.
            case ScenePhase.StartingFlight:
            case ScenePhase.ToTrackingStation:
            case ScenePhase.ToMenu:
                break;
        }
    }

    void RequireFlightContext()
    {
        Vessel active = FlightGlobals.ActiveVessel;
        if (!HighLogic.LoadedSceneIsFlight || !FlightGlobals.ready || active == null ||
            !active.loaded || active.packed || active.id != intendedVessel)
            throw new ProbeException("Expected the intended vessel loaded and unpacked in a ready flight scene.");
        if (FlightDriver.Pause || TimeWarp.CurrentRateIndex != 0 || TimeWarp.CurrentRate != 1.0f)
            throw new ProbeException("Flight observation requires unpaused actual 1x.");
    }

    void RequireTrackingStationContext()
    {
        if (HighLogic.LoadedScene != GameScenes.TRACKSTATION)
            throw new ProbeException("Expected the tracking-station scene.");
        if (FlightDriver.Pause || TimeWarp.CurrentRateIndex != 0 || TimeWarp.CurrentRate != 1.0f)
            throw new ProbeException("Tracking-station observation requires unpaused actual 1x.");
    }

    void RequireElectricityPolicy(string stage)
    {
        bool required, fatal;
        bool known = CryoBridge.TryPowerPolicy(out required, out fatal);
        Record(stage + "_electricity_policy_known", known);
        Record(stage + "_electricity_required", known && required);
        Record(stage + "_fatal_consequence_setting_observed", known && fatal);
        if (!known || !required)
            throw new ProbeInconclusiveException("DeepFreeze electricity policy is unknown or disabled; no cryogenic request made.");
    }

    void Record(string key, object value)
    {
        string text = value is IFormattable
            ? ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture)
            : (value == null ? "" : value.ToString());
        report.SetValue(key, text, true);
        report.Save(Path.Combine(root, "HermesCryoQA-result.cfg"));
        Debug.Log("[HermesCryoQA] " + key + "=" + text);
    }

    void Finish(bool complete, bool inconclusive = false)
    {
        Record("run_state", complete ? "completed" : inconclusive ? "inconclusive" : "failed");
        Record("probe_completed", complete);
        Record("flight_qualification", false);
        Record("gui_qualification", false);
        Record("transfer_busy_qualification", false);
        string marker = Path.Combine(root, "HermesCryoQA.running");
        if (File.Exists(marker)) File.Move(marker, Path.Combine(root, "HermesCryoQA.done"));
        if (scoped) Application.Quit();
    }

    static double Charge(Vessel vessel)
    {
        double amount = 0;
        foreach (Part part in vessel.parts)
            foreach (PartResource resource in part.Resources)
                if (resource.resourceName == "ElectricCharge" && resource.flowState) amount += resource.amount;
        return amount;
    }

    static bool Frozen(Guid vesselId, uint podId)
    {
        DF.KerbalInfo info;
        return DF.DeepFreeze.Instance != null &&
            DF.DeepFreeze.Instance.FrozenKerbals.TryGetValue("Ada Kerman", out info) &&
            info.vesselID == vesselId && info.partID == podId;
    }

    static bool TryFrozen(Guid vesselId, uint podId, out bool frozen)
    {
        frozen = false;
        if (!CryoBridge.TryFrozen("Ada Kerman", out frozen)) return false;
        return !frozen || Frozen(vesselId, podId);
    }

    IEnumerator Run()
    {
        Record("schema", 1);
        Record("probe_completed", false);
        Record("flight_qualification", false);
        Record("gui_qualification", false);
        Record("transfer_busy_qualification", false);
        Record("run_state", "running");
        Record("stage", "initializing");
        Record("programmatic_crew_move_setup", true);
        Record("gui_transfer_tested", false);
        Record("transfer_busy_tested", false);
        yield return new WaitForSecondsRealtime(5);

        string variant = File.ReadAllText(Path.Combine(root, "HermesCryoQA.variant")).Trim();
        Record("variant", variant);
        if (variant != "original" && variant != "patched") throw new ProbeException("Unknown QA variant.");
        string providerPath = Path.Combine(root, "GameData/REPOSoftTech/BackgroundResources/BackgroundResources.dll");
        string providerHash = Hash(providerPath);
        Record("provider_sha256", providerHash);
        string expectedProvider = variant == "patched"
            ? "d2c1ab678a0891246dc7b16e66e180ced7e8ad55db8bb9971cc742224dfee45a"
            : "ed956b9ec2cc5f414586a52a9086423e4c9125fa72097bce2721ea41eb075bc9";
        if (providerHash != expectedProvider) throw new ProbeException("Provider hash mismatch.");

        RequireMenu();
        IEnumerator firstLoad = LoadAndStartFlight("initial_load", Guid.Empty);
        while (firstLoad.MoveNext()) yield return firstLoad.Current;

        Vessel vessel = FlightGlobals.ActiveVessel;
        Guid vesselId = vessel.id;
        if (vessel.parts == null || vessel.parts.Count != ExpectedParts || vessel.GetCrewCount() != ExpectedCrew)
            throw new ProbeException("Pristine vessel/crew mismatch.");
        Record("initial_parts", vessel.parts.Count);
        Record("initial_awake_crew", vessel.GetCrewCount());
        Record("initial_ec", Charge(vessel));

        Part freezer = null, origin = null;
        ProtoCrewMember ada = null;
        foreach (Part part in vessel.parts)
        {
            foreach (PartModule module in part.Modules)
                if (module.moduleName == "DeepFreezer") freezer = part;
            foreach (ProtoCrewMember person in part.protoModuleCrew)
                if (person.name == "Ada Kerman") { ada = person; origin = part; }
        }
        if (freezer == null || origin == null || ada == null || Charge(vessel) < 3300)
            throw new ProbeException("Missing pod/crew or insufficient freeze reserve.");
        uint podId = freezer.flightID;
        RequireElectricityPolicy("before_crew_move");
        Record("stage", "programmatic_crew_move_setup");
        Record("crew_move_method", "direct part crew-list/event setup; not GUI transfer evidence");

        // This programmatic arrangement only places Ada in the pod for the
        // serial freeze procedure. It does not demonstrate an in-progress
        // stock transfer or transfer-busy refusal.
        origin.RemoveCrewmember(ada);
        freezer.AddCrewmember(ada);
        GameEvents.onCrewTransferred.Fire(new GameEvents.HostedFromToAction<ProtoCrewMember, Part>(ada, origin, freezer));
        Vessel.CrewWasModified(origin.vessel, freezer.vessel);
        vessel.DespawnCrew();
        yield return new WaitForSecondsRealtime(1);
        vessel.SpawnCrew();

        DF.DeepFreezer actualPod = FindPod(freezer);
        if (actualPod == null) throw new ProbeException("Actual pod module unavailable.");
        float deadline = Time.realtimeSinceStartup + 90;
        while (!DF.DeepFreeze.APIReady || actualPod.DFIcrewXferTOActive || actualPod.DFIcrewXferFROMActive)
        {
            if (Time.realtimeSinceStartup > deadline) throw new ProbeException("Stock transfer/pod readiness timeout.");
            yield return new WaitForSecondsRealtime(1);
        }
        float settleDeadline = Time.realtimeSinceStartup + 2;
        while (Time.realtimeSinceStartup < settleDeadline)
            yield return new WaitForSecondsRealtime(1);
        Record("stage", "freezing_crew");
        RequireElectricityPolicy("before_freeze_request");
        if (!freezer.protoModuleCrew.Contains(ada) || !CryoBridge.Freeze("Ada Kerman"))
            throw new ProbeException("Real freeze request rejected.");

        deadline = Time.realtimeSinceStartup + 240;
        while (true)
        {
            bool frozen;
            if (TryFrozen(vesselId, podId, out frozen) && frozen) break;
            if (Time.realtimeSinceStartup > deadline) throw new ProbeException("Actual freeze timeout.");
            yield return new WaitForSecondsRealtime(1);
        }
        RequireFlightContext();
        foreach (Part part in vessel.parts)
            foreach (PartModule module in part.Modules)
            {
                ModuleDeployableSolarPanel panel = module as ModuleDeployableSolarPanel;
                if (panel != null && panel.deployState.ToString() != "RETRACTED")
                    throw new ProbeException("Expected stowed solar panels for unloaded power observation.");
                ModuleGenerator generator = module as ModuleGenerator;
                if (generator != null && generator.generatorIsActive)
                    throw new ProbeException("Unexpected active generator in power fixture.");
            }
        Record("actual_frozen_before_unload", true);
        Record("ec_after_freeze", Charge(vessel));
        SaveLiveSnapshot("frozen_before_unload");

        Record("stage", "transition_to_tracking_station");
        phase = ScenePhase.ToTrackingStation;
        HighLogic.LoadScene(GameScenes.TRACKSTATION);
        deadline = Time.realtimeSinceStartup + 180;
        while (HighLogic.LoadedScene != GameScenes.TRACKSTATION)
        {
            if (Time.realtimeSinceStartup > deadline) throw new ProbeInconclusiveException("Tracking-station scene transition timeout.");
            yield return new WaitForSecondsRealtime(1);
        }
        phase = ScenePhase.TrackingStation;
        Record("tracking_pause_at_ready", FlightDriver.Pause);
        Record("tracking_rate_index_at_ready", TimeWarp.CurrentRateIndex);
        Record("tracking_rate_at_ready", TimeWarp.CurrentRate);
        RequireTrackingStationContext();

        deadline = Time.realtimeSinceStartup + 90;
        Vessel unloadedVessel = FindVessel(vesselId);
        while (unloadedVessel == null || unloadedVessel.loaded)
        {
            if (Time.realtimeSinceStartup > deadline) throw new ProbeException("Vessel did not become genuinely unloaded.");
            yield return new WaitForSecondsRealtime(1);
            unloadedVessel = FindVessel(vesselId);
        }
        phase = ScenePhase.TrackingStationUnloaded;
        if (!BackgroundResources.UnloadedResources.InterestedVessels.Contains(unloadedVessel.protoVessel))
            throw new ProbeException("Provider did not register the unloaded vessel.");
        DF.PartInfo pod;
        if (DF.DeepFreeze.Instance == null || !DF.DeepFreeze.Instance.KnownFreezerParts.TryGetValue(podId, out pod))
            throw new ProbeException("Missing real pod ledger.");
        double before, capacity;
        BackgroundResources.UnloadedResourceProcessing.GetResourceTotals(unloadedVessel.protoVessel, "ElectricCharge", out before, out capacity);
        Record("stage", "observing_unloaded_power");
        double startUt = RequireUt(), lastPodUt = pod.timeLastElectricity;
        float wallStart = Time.realtimeSinceStartup;
        Record("unloaded_before_ec", before);
        Record("unloaded_before_ut", startUt);
        Record("unloaded_before_pod_stamp", lastPodUt);
        while (Time.realtimeSinceStartup - wallStart < 45f)
        {
            ValidateContext();
            yield return new WaitForSecondsRealtime(1);
        }
        double afterUt = RequireUt();
        double elapsedUt = afterUt - startUt;
        if (elapsedUt < 40.0 || elapsedUt > 50.0)
            throw new ProbeException("Tracking-station UT did not advance near 1x during the 45-second observation.");
        double after;
        BackgroundResources.UnloadedResourceProcessing.GetResourceTotals(unloadedVessel.protoVessel, "ElectricCharge", out after, out capacity);
        Record("unloaded_after_ec", after);
        Record("unloaded_after_ut", afterUt);
        Record("unloaded_elapsed_realtime", Time.realtimeSinceStartup - wallStart);
        Record("unloaded_elapsed_ut", elapsedUt);
        Record("pod_accounted_seconds", pod.timeLastElectricity - lastPodUt);
        Record("pod_expected_ec", (pod.timeLastElectricity - lastPodUt) * pod.frznChargeRequired / 60 * pod.numFrznCrew);
        Record("observed_ec_delta", after - before);
        if (variant == "patched" && after >= before)
            throw new ProbeException("Corrected provider did not produce net EC draw; investigate actual load/generation.");
        Record("actual_frozen_after_unload", Frozen(vesselId, podId));
        if (!Frozen(vesselId, podId)) throw new ProbeException("Frozen crew lost during unloaded interval.");
        SaveLiveSnapshot("after_unloaded_observation");

        Record("stage", "transition_to_menu_for_frozen_reload");
        phase = ScenePhase.ToMenu;
        HighLogic.LoadScene(GameScenes.MAINMENU);
        deadline = Time.realtimeSinceStartup + 180;
        while (HighLogic.LoadedScene != GameScenes.MAINMENU)
        {
            if (Time.realtimeSinceStartup > deadline) throw new ProbeInconclusiveException("Main-menu transition timeout before frozen reload.");
            yield return new WaitForSecondsRealtime(1);
        }
        phase = ScenePhase.Menu;
        RequireMenu();

        Record("stage", "reloading_frozen_game_through_native_pipeline");
        IEnumerator reload = LoadAndStartFlight("frozen_reload", vesselId);
        while (reload.MoveNext()) yield return reload.Current;
        vessel = FlightGlobals.ActiveVessel;
        bool frozenAfterReload;
        bool frozenAfterReloadKnown = TryFrozen(vesselId, podId, out frozenAfterReload);
        Record("frozen_after_reload_known", frozenAfterReloadKnown);
        Record("frozen_after_reload", frozenAfterReloadKnown && frozenAfterReload);
        Record("awake_after_reload", vessel.GetCrewCount());
        freezer = FindPart(vessel, podId);
        if (!frozenAfterReloadKnown || !frozenAfterReload || !Frozen(vesselId, podId) ||
            vessel.GetCrewCount() != 3 || freezer == null ||
            !ContainsCrew(freezer, "Ada Kerman"))
            throw new ProbeException("Frozen crew/pod persistence did not match the saved vessel.");

        Record("stage", "recharging_from_solar");
        foreach (Part part in vessel.parts)
            foreach (PartModule module in part.Modules)
                if (module.moduleName == "ModuleDeployableSolarPanel" && module.Events["Extend"] != null && module.Events["Extend"].active)
                    module.Events["Extend"].Invoke();
        deadline = Time.realtimeSinceStartup + 2400;
        while (Charge(vessel) < 3300)
        {
            if (Time.realtimeSinceStartup > deadline) throw new ProbeException("Solar recharge timeout.");
            yield return new WaitForSecondsRealtime(1);
        }
        Record("stage", "thawing_crew");
        if (!CryoBridge.Thaw("Ada Kerman")) throw new ProbeException("Real thaw request rejected.");
        deadline = Time.realtimeSinceStartup + 240;
        while (true)
        {
            bool frozen;
            if (TryFrozen(vesselId, podId, out frozen) && !frozen) break;
            if (Time.realtimeSinceStartup > deadline) throw new ProbeException("Actual thaw timeout.");
            yield return new WaitForSecondsRealtime(1);
        }
        Record("actual_thaw_complete", true);
        Record("final_awake_crew", vessel.GetCrewCount());
        if (vessel.GetCrewCount() != 4) throw new ProbeException("Crew count did not recover after thaw.");
        SaveLiveSnapshot("after_thaw");
        Record("stage", "procedure_finished");
        Record("note", "Single-pod 1x procedure only; GUI transfer, transfer-busy refusal, multiple pods, depletion/settings, high warp and thermal behavior remain unqualified.");
    }

    IEnumerator LoadAndStartFlight(string stage, Guid expectedVesselId)
    {
        phase = ScenePhase.Menu;
        RequireMenu();
        Record("stage", stage + "_loading_sfs");
        ConfigNode sfs = GamePersistence.LoadSFSFile("persistent", SaveName);
        if (sfs == null) throw new ProbeException("Could not load persistent SFS from the disposable save.");

        ConfigNode upgraded = null;
        bool callbackReceived = false;
        bool pipelineSucceeded = false;
        string pipelineFailure = "";
        KSPUpgradePipeline.Process(sfs, SaveName, LoadContext.SFS,
            delegate(ConfigNode node) { upgraded = node; pipelineSucceeded = true; callbackReceived = true; },
            delegate(KSPUpgradePipeline.UpgradeFailOption option, ConfigNode node)
            {
                pipelineFailure = option.ToString();
                callbackReceived = true;
            });

        float deadline = Time.realtimeSinceStartup + 180;
        while (!callbackReceived)
        {
            if (Time.realtimeSinceStartup > deadline)
                throw new ProbeInconclusiveException("Save-upgrade callback timed out; no failure/LoadAnyway path is accepted.");
            yield return new WaitForSecondsRealtime(1);
        }
        if (!pipelineSucceeded || upgraded == null)
            throw new ProbeException("Save-upgrade pipeline failed (" + pipelineFailure + "); refusing all failure options.");

        Game game = GamePersistence.LoadGameCfg(upgraded, SaveName, true, false);
        if (game == null || game.flightState == null || game.flightState.protoVessels == null || game.flightState.protoVessels.Count != 1)
            throw new ProbeException("Native load produced no single-vessel game state.");
        int activeIndex = game.flightState.activeVesselIdx;
        if (activeIndex != 0 || game.flightState.protoVessels[activeIndex].vesselID == Guid.Empty)
            throw new ProbeException("Loaded save did not identify one active vessel at index zero.");
        Guid loadedVessel = game.flightState.protoVessels[activeIndex].vesselID;
        if (expectedVesselId == Guid.Empty) intendedVessel = loadedVessel;
        else if (loadedVessel != expectedVesselId || intendedVessel != expectedVesselId)
            throw new ProbeException("Frozen reload vessel GUID changed.");
        Record(stage + "_vessel_guid", loadedVessel.ToString("D"));
        Record(stage + "_active_vessel_index", activeIndex);

        // Mirror MainMenu.OnLoadDialogPipelineFinished using only public APIs.
        HighLogic.CurrentGame = game;
        expectedGame = game;
        fixtureLoaded = true;
        GamePersistence.UpdateScenarioModules(game);
        Record(stage + "_scenario_module_count", game.scenarios == null ? -1 : game.scenarios.Count);
        string scenarioNames = ScenarioModuleNames(game);
        Record(stage + "_scenario_module_names", scenarioNames);
        if (game.scenarios == null || game.scenarios.Count != ExpectedScenarioModules)
            throw new ProbeException("Native scenario update did not produce the expected scenario count.");
        if (CountScenario(game, "DeepFreeze") != 1)
            throw new ProbeException("Native scenario update did not produce exactly one DeepFreeze scenario.");
        GamePersistence.SaveGame(game, "persistent", SaveName, SaveMode.OVERWRITE);
        Record(stage + "_native_save_performed", true);
        GameEvents.onGameStatePostLoad.Fire(upgraded);
        Record(stage + "_postload_event_fired", true);
        HighLogic.SaveFolder = SaveName;
        phase = ScenePhase.StartingFlight;
        game.Start();
        Record(stage + "_game_start_called", true);

        deadline = Time.realtimeSinceStartup + 180;
        while (!HighLogic.LoadedSceneIsFlight || !FlightGlobals.ready || FlightGlobals.ActiveVessel == null ||
               !FlightGlobals.ActiveVessel.loaded || FlightGlobals.ActiveVessel.packed)
        {
            if (Time.realtimeSinceStartup > deadline) throw new ProbeInconclusiveException("Native Game.Start flight readiness timeout.");
            yield return new WaitForSecondsRealtime(1);
        }
        phase = ScenePhase.Flight;
        RequireFlightContext();
        Record(stage + "_flight_pause", FlightDriver.Pause);
        Record(stage + "_flight_rate_index", TimeWarp.CurrentRateIndex);
        Record(stage + "_flight_rate", TimeWarp.CurrentRate);
        Record(stage + "_flight_ready", true);
        float stabilizationDeadline = Time.realtimeSinceStartup + 12;
        while (Time.realtimeSinceStartup < stabilizationDeadline)
            yield return new WaitForSecondsRealtime(1);
        RequireFlightContext();
        if (FlightGlobals.ActiveVessel.id != intendedVessel)
            throw new ProbeException("Active vessel changed after native load.");
    }

    void SaveLiveSnapshot(string label)
    {
        RequireFlightContextIfFlight();
        if (phase == ScenePhase.TrackingStation || phase == ScenePhase.TrackingStationUnloaded)
            RequireTrackingStationContext();
        Game live = HighLogic.CurrentGame;
        if (live == null || !System.Object.ReferenceEquals(live, expectedGame))
            throw new ProbeException("Cannot save a snapshot from an unbound live Game.");
        Game snapshot = live.Updated(GameScenes.FLIGHT);
        if (snapshot == null || snapshot.flightState == null || snapshot.flightState.protoVessels == null ||
            snapshot.flightState.protoVessels.Count != 1 || snapshot.flightState.activeVesselIdx != 0 ||
            snapshot.flightState.protoVessels[0].vesselID != intendedVessel)
            throw new ProbeException("Live snapshot did not preserve the single intended active vessel; refusing to save.");
        GamePersistence.SaveGame(snapshot, "persistent", SaveName, SaveMode.OVERWRITE);
        Record(label + "_snapshot_saved", true);
        Record(label + "_snapshot_active_index", snapshot.flightState.activeVesselIdx);
        Record(label + "_snapshot_vessel_guid", snapshot.flightState.protoVessels[0].vesselID.ToString("D"));
    }

    void RequireFlightContextIfFlight()
    {
        if (phase == ScenePhase.Flight) RequireFlightContext();
    }

    static Vessel FindVessel(Guid id)
    {
        if (FlightGlobals.Vessels == null) return null;
        return FlightGlobals.Vessels.Find(delegate(Vessel vessel) { return vessel != null && vessel.id == id; });
    }

    static Part FindPart(Vessel vessel, uint flightId)
    {
        if (vessel == null || vessel.parts == null) return null;
        foreach (Part part in vessel.parts) if (part != null && part.flightID == flightId) return part;
        return null;
    }

    static bool ContainsCrew(Part part, string name)
    {
        if (part == null || part.protoModuleCrew == null) return false;
        foreach (ProtoCrewMember person in part.protoModuleCrew) if (person != null && person.name == name) return true;
        return false;
    }

    static DF.DeepFreezer FindPod(Part part)
    {
        if (part == null || part.Modules == null) return null;
        foreach (PartModule module in part.Modules)
        {
            DF.DeepFreezer pod = module as DF.DeepFreezer;
            if (pod != null) return pod;
        }
        return null;
    }

    static string ScenarioModuleNames(Game game)
    {
        if (game == null || game.scenarios == null) return "";
        List<string> names = new List<string>();
        foreach (ProtoScenarioModule scenario in game.scenarios)
            if (scenario != null) names.Add(scenario.moduleName ?? "");
        names.Sort(StringComparer.Ordinal);
        return String.Join("|", names.ToArray());
    }

    static int CountScenario(Game game, string moduleName)
    {
        int count = 0;
        if (game == null || game.scenarios == null) return count;
        foreach (ProtoScenarioModule scenario in game.scenarios)
            if (scenario != null && scenario.moduleName == moduleName) count++;
        return count;
    }

    void RequireMenu()
    {
        if (HighLogic.LoadedScene != GameScenes.MAINMENU) throw new ProbeException("Expected MAINMENU for native save loading.");
        Record("menu_pause_observed", FlightDriver.Pause);
        Record("menu_rate_index_observed", TimeWarp.CurrentRateIndex);
        Record("menu_rate_observed", TimeWarp.CurrentRate);
    }

    static double RequireUt()
    {
        double ut = Planetarium.GetUniversalTime();
        if (Double.IsNaN(ut) || Double.IsInfinity(ut) || ut <= 0) throw new ProbeException("Universal time is not finite and positive.");
        return ut;
    }

    static string Hash(string path)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
}
