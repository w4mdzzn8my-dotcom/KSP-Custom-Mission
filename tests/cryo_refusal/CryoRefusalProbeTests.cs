using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;
using DF;

static class CryoRefusalProbeTests
{
    const string Scope = "MEGACREW_DISPOSABLE_CRYO_REFUSAL_QA_V1\n";
    const string Enable = "AUTHORIZE ONE ORIGINAL-PROVIDER CRYO REFUSAL QA FOR SAVE: MegaCrew Background QA\n";
    static int assertions;
    static string root, fixtureRoot, probePath;
    static Type probeType;

    static void Main(string[] args)
    {
        if (args.Length != 2) throw new Exception("Expected <fixture-root> <probe-assembly-path>");
        fixtureRoot = Path.GetFullPath(args[0]); probePath = Path.GetFullPath(args[1]);
        root = Path.Combine(Path.GetTempPath(), "cryo-refusal-boundary-" + Guid.NewGuid().ToString("N"), "KSP-Hermes-QA");
        Directory.CreateDirectory(root);
        CopyFixtureInputs();
        KSPUtil.ApplicationRootPath = root;
        Assembly.LoadFrom(Path.Combine(root, "GameData", "MegaCrew", "Plugins", "MegaCrew.dll"));
        probeType = Assembly.LoadFrom(probePath).GetType("CryoRefusalProbe", true);

        TestUnscopedRefusesWithoutWritesOrGameCalls();
        TestMissingAndStaleAuthorizationRefuse();
        TestPinnedInputMismatchRefusesBeforeLoad();
        TestValidOriginalProviderRefusalAndLifecycle();
        TestMenuClockStateIsObservedButDoesNotBlockReadiness();
        TestStrictFlightClockGuardAfterReadiness();
        TestUnknownProviderReadinessDoesNotDispatch();
        TestUnknownCrewStateDoesNotDispatch();
        TestPreFrozenAdaDoesNotDispatch();
        TestAdaInsideFreezerDoesNotDispatch();
        TestWrongFixtureShapeDoesNotDispatch();
        TestWrongActiveVesselAfterRequestCannotPass();

        Console.WriteLine("Cryo refusal probe component-boundary tests passed (" + assertions + " assertions). Original plugin/API and fixture hashes were real; KSP save loading, scene changes and resources were recorded fakes.");
        try { Directory.Delete(Path.GetDirectoryName(root), true); } catch { }
    }

    static void TestMenuClockStateIsObservedButDoesNotBlockReadiness()
    {
        ResetRoot(); WriteAuthorization(); ResetEnvironment();
        GamePersistence.NextGame = MakeGame(17, 4, false);
        FlightDriver.Pause = true; TimeWarp.CurrentRateIndex = -1; TimeWarp.CurrentRate = 0f;
        FlightDriver.ReadyAfterWaits = 2;
        FlightDriver.ReadyPause = false; FlightDriver.ReadyRateIndex = 0; FlightDriver.ReadyRate = 1f;
        RunProbe();
        string report = ReadReport();
        Contains(report, "menu_pause_observed=True", "uninitialized main-menu pause is recorded, not rejected");
        Contains(report, "menu_time_warp_rate_index_observed=-1", "uninitialized main-menu warp index is recorded, not rejected");
        Contains(report, "flight_pause_at_readiness=False", "flight pause is sampled after readiness");
        Contains(report, "flight_time_warp_rate_index_at_readiness=0", "flight warp index is sampled after readiness");
        Contains(report, "result=REFUSED_AS_EXPECTED", "normal flight clock allows the refusal observation");
        Contains(report, "freeze_request_called=True", "normal ready flight reaches the one refusal request");
        Equal(1, Game.StartCalls, "menu clock state did not prevent native Game.Start");
        AssertNativeLifecycleOrder();
        Equal(0, FreezeModuleCalls(), "normal ready flight still receives a refusal");
    }

    static void TestStrictFlightClockGuardAfterReadiness()
    {
        ResetRoot(); WriteAuthorization(); ResetEnvironment();
        GamePersistence.NextGame = MakeGame(17, 4, false);
        FlightDriver.ReadyAfterWaits = 2;
        FlightDriver.ReadyPause = true;
        RunProbe();
        string report = ReadReport();
        Contains(report, "flight_pause_at_readiness=True", "paused flight is observed only after readiness");
        Contains(report, "run_state=INCONCLUSIVE", "paused flight fails closed after readiness");
        Equal(false, report.Contains("freeze_request_called=True"), "paused flight never dispatches Freeze");
        Equal(1, Game.StartCalls, "paused-flight guard is reached only after Game.Start");
        Equal(0, FreezeModuleCalls(), "paused flight makes no pod call");
    }

    static void TestUnscopedRefusesWithoutWritesOrGameCalls()
    {
        string wrong = Path.Combine(Path.GetDirectoryName(root), "Unscoped");
        Directory.CreateDirectory(wrong); KSPUtil.ApplicationRootPath = wrong;
        ResetEnvironment(); RunProbe();
        Equal(0, GamePersistence.LoadCalls, "unscoped probe never loads a save");
        Equal(0, Game.StartCalls, "unscoped probe never starts the game");
        Equal(0, FlightDriver.StartCalls, "unscoped probe never calls FlightDriver directly");
        Equal(0, Application.QuitCount, "unscoped probe never quits KSP");
        Equal(false, File.Exists(Path.Combine(wrong, "CryoRefusalQA.running")), "unscoped probe writes no running marker");
        Equal(false, File.Exists(Path.Combine(wrong, "CryoRefusalQA-result.cfg")), "unscoped probe writes no report");
        Equal(false, File.Exists(Path.Combine(wrong, "CryoRefusalQA.done")), "unscoped probe writes no completion marker");
        KSPUtil.ApplicationRootPath = root;
    }

    static void TestMissingAndStaleAuthorizationRefuse()
    {
        ResetRoot(); WriteScope(); ResetEnvironment(); RunProbe();
        Equal(0, GamePersistence.LoadCalls, "missing enable token refuses before load");
        Equal(false, File.Exists(Path.Combine(root, "CryoRefusalQA-result.cfg")), "missing enable token causes no report write");

        ResetRoot(); WriteScope(); WriteEnable(); File.WriteAllText(Path.Combine(root, "CryoRefusalQA.done"), "old"); ResetEnvironment(); RunProbe();
        Equal(0, GamePersistence.LoadCalls, "old consumed marker refuses re-execution");
        Equal(false, File.Exists(Path.Combine(root, "CryoRefusalQA.running")), "stale authorization does not start a new run");
    }

    static void TestPinnedInputMismatchRefusesBeforeLoad()
    {
        ResetRoot(); WriteAuthorization();
        File.AppendAllText(Path.Combine(root, "GameData/REPOSoftTech/BackgroundResources/BackgroundResources.dll"), "bad");
        ResetEnvironment(); RunProbe();
        string report = ReadReport();
        Equal(0, GamePersistence.LoadCalls, "wrong provider hash refuses before save load");
        Equal(0, Game.StartCalls, "wrong provider hash never starts the game");
        Equal(0, FlightDriver.StartCalls, "wrong provider hash never calls FlightDriver directly");
        Equal(0, FreezeModuleCalls(), "wrong provider hash never invokes freezer action");
        Contains(report, "run_state=INCONCLUSIVE", "hash mismatch is inconclusive");
        Contains(report, "no cryo request made", "hash mismatch explains request was not made");
        Equal(1, Application.QuitCount, "authorized refusal report closes only the scoped QA process");
        CopyFixtureInputs();
    }

    static void TestValidOriginalProviderRefusalAndLifecycle()
    {
        ResetRoot(); WriteAuthorization(); ResetEnvironment();
        var game = MakeGame(parts: 17, crew: 4, adaInFreezer: false);
        GamePersistence.NextGame = game;
        FlightDriver.ReadyAfterWaits = 2; // prove the probe tolerates asynchronous scene readiness
        RunProbe();

        string report = ReadReport();
        Console.WriteLine("refusal report: " + report.Replace("\n", " | "));
        Contains(report, "loaded_parts=17", "pinned fixture has 17 loaded parts");
        Contains(report, "loaded_awake_crew=4", "pinned fixture has four awake crew");
        Contains(report, "ada_frozen_before_known=True", "Ada frozen state was verified before request");
        Contains(report, "ada_frozen_before=False", "Ada is not frozen before request");
        Contains(report, "freeze_request_called=True", "exactly one source dispatch point was reached");
        Contains(report, "freeze_request_returned_true=False", "original provider rejects Ada outside a freezer");
        Contains(report, "preflight_ut_advance=5", "probe observes five seconds of actual simulated UT before dispatch");
        Contains(report, "post_request_ut_advance=5", "probe observes five seconds of actual simulated UT after dispatch");
        Contains(report, "ada_frozen_after=False", "Ada remains unfrozen after refusal");
        Contains(report, "parts_after=17", "part count remains unchanged");
        Contains(report, "awake_crew_after=4", "crew count remains unchanged");
        Contains(report, "ada_aboard_after=True", "Ada remains aboard");
        Contains(report, "result=REFUSED_AS_EXPECTED", "only verified refusal completes");
        Contains(report, "run_state=COMPLETED", "validated observation completed");
        Contains(report, "probe_completed=True", "completed observation is explicit");
        Contains(report, "flight_qualification=False", "backend observation never qualifies the whole flight");
        Contains(report, "stage=procedure_finished", "successful report identifies finished observation");
        Contains(report, "resource_delta_cause_inferred=False", "resource changes are not attributed");
        Equal(false, report.Contains("transfer="), "probe never claims a transfer");
        Equal(1, GamePersistence.LoadCalls, "valid scoped run loads once");
        Equal(1, Game.StartCalls, "valid scoped run calls Game.Start once");
        Equal(0, FlightDriver.StartCalls, "valid scoped run never calls FlightDriver directly");
        AssertNativeLifecycleOrder();
        Contains(report, "scenario_module_update_called=True", "KSP scenario modules are updated before flight");
        Contains(report, "scenario_module_count_after_update=1", "fixture has one scenario module after KSP updates scenario modules");
        Contains(report, "scenario_module_names_after_update=DeepFreeze", "scenario instance name proves DeepFreeze was added by stock scenario update");
        Equal(true, game.scenarios.Exists(s => s != null && s.moduleName == "DeepFreeze"), "recorded UpdateScenarioModules supplies the AddToAllGames DeepFreeze scenario node");
        Equal(1, GamePersistence.UpdateScenarioModulesCalls, "UpdateScenarioModules is called once");
        Equal(1, GamePersistence.SaveCalls, "one scoped native save call occurs");
        Equal(0, GamePersistence.ThreeArgumentSaveCalls, "stock four-argument SaveGame overload is used");
        Equal(true, System.Object.ReferenceEquals(game, GamePersistence.LastSavedGame), "SaveGame receives the exact CurrentGame object");
        Contains(report, "native_lifecycle_save_performed=True", "fixture is saved through the native scoped save step");
        Contains(report, "postload_event_fired=True", "native post-load event is fired");
        Contains(report, "game_start_called=True", "native Game.Start lifecycle is used");
        Equal("persistent", GamePersistence.LastSaveFile, "native save uses the persistent save filename");
        Equal("MegaCrew Background QA", GamePersistence.LastSaveFolder, "native save targets the exact disposable save");
        Equal(SaveMode.OVERWRITE, GamePersistence.LastSaveMode, "native save uses overwrite mode for the disposable clone");
        Equal(1, Application.QuitCount, "scoped completion quits once");
        Equal(1, CountFiles("CryoRefusalQA.running", "CryoRefusalQA.done"), "running token is consumed into one done token");
        Equal(0, FreezeModuleCalls(), "rejected request did not call a DeepFreezer module");
        Equal(4, game.fixtureVessel.GetCrewCount(), "recording boundary did not transfer/remove crew");
        Equal(17, game.fixtureVessel.parts.Count, "recording boundary did not add/remove parts");
        Equal(false, DF.DeepFreeze.Instance.FrozenKerbals.Contains("Ada Kerman"), "recording boundary did not mark Ada frozen");
        Equal(ExpectedHash("saves/MegaCrew Background QA/persistent.sfs"), Hash(FromRoot("saves/MegaCrew Background QA/persistent.sfs")), "save input remains byte-identical");
        Equal(ExpectedHash("GameData/MegaCrew/Plugins/MegaCrew.dll"), Hash(FromRoot("GameData/MegaCrew/Plugins/MegaCrew.dll")), "plugin input remains byte-identical");
        Equal(ExpectedHash("GameData/REPOSoftTech/BackgroundResources/BackgroundResources.dll"), Hash(FromRoot("GameData/REPOSoftTech/BackgroundResources/BackgroundResources.dll")), "provider input remains byte-identical");
    }

    static void TestUnknownProviderReadinessDoesNotDispatch()
    {
        ResetRoot(); WriteAuthorization(); ResetEnvironment();
        GamePersistence.NextGame = MakeGame(17, 4, false); DF.DeepFreeze.APIReady = false;
        DF.DeepFreeze.InitializeOnNativeGameStart = false;
        RunProbe();
        string report = ReadReport();
        Contains(report, "run_state=INCONCLUSIVE", "provider initialization timeout is inconclusive");
        Equal(false, report.Contains("freeze_request_called=True"), "unknown provider readiness never dispatches");
        Equal(0, FreezeModuleCalls(), "unknown provider readiness never acts on a pod");
    }

    static void TestUnknownCrewStateDoesNotDispatch()
    {
        ResetRoot(); WriteAuthorization(); ResetEnvironment();
        GamePersistence.NextGame = MakeGame(17, 4, false); DF.DeepFreeze.Instance.FrozenKerbals = null;
        RunProbe();
        string report = ReadReport();
        Contains(report, "run_state=INCONCLUSIVE", "unknown crew state is inconclusive");
        Equal(false, report.Contains("freeze_request_called=True"), "unknown frozen state never dispatches");
        Equal(0, FreezeModuleCalls(), "unknown frozen state never acts on a pod");
    }

    static void TestPreFrozenAdaDoesNotDispatch()
    {
        ResetRoot(); WriteAuthorization(); ResetEnvironment();
        Game game = MakeGame(17, 4, false); GamePersistence.NextGame = game;
        DF.DeepFreeze.Instance.FrozenKerbals["Ada Kerman"] = new FrozenInfo { vesselID = game.fixtureVessel.id, partID = 17 };
        RunProbe();
        string report = ReadReport();
        Contains(report, "run_state=INCONCLUSIVE", "pre-frozen Ada refuses test request");
        Equal(false, report.Contains("freeze_request_called=True"), "pre-frozen Ada does not dispatch");
        Equal(0, FreezeModuleCalls(), "pre-frozen Ada does not invoke pod");
    }

    static void TestAdaInsideFreezerDoesNotDispatch()
    {
        ResetRoot(); WriteAuthorization(); ResetEnvironment();
        GamePersistence.NextGame = MakeGame(17, 4, true);
        RunProbe();
        string report = ReadReport();
        Contains(report, "run_state=INCONCLUSIVE", "Ada already in a freezer is outside refusal fixture");
        Equal(false, report.Contains("freeze_request_called=True"), "Ada in freezer is never dispatched");
        Equal(0, FreezeModuleCalls(), "Ada in freezer is not frozen twice");
    }

    static void TestWrongFixtureShapeDoesNotDispatch()
    {
        ResetRoot(); WriteAuthorization(); ResetEnvironment();
        GamePersistence.NextGame = MakeGame(16, 4, false);
        RunProbe();
        string report = ReadReport();
        Contains(report, "run_state=INCONCLUSIVE", "wrong part count refuses fixture");
        Equal(false, report.Contains("freeze_request_called=True"), "wrong fixture shape does not dispatch");
        Equal(0, FreezeModuleCalls(), "wrong fixture shape does not invoke pod");

        ResetRoot(); WriteAuthorization(); ResetEnvironment();
        GamePersistence.NextGame = MakeGame(17, 3, false);
        RunProbe();
        report = ReadReport();
        Contains(report, "run_state=INCONCLUSIVE", "wrong awake crew count refuses fixture");
        Equal(false, report.Contains("freeze_request_called=True"), "wrong crew count does not dispatch");
    }

    static void TestWrongActiveVesselAfterRequestCannotPass()
    {
        ResetRoot(); WriteAuthorization(); ResetEnvironment();
        GamePersistence.NextGame = MakeGame(17, 4, false);
        bool changed = false;
        RunProbe(delegate {
            if (changed) return;
            string reportPath = Path.Combine(root, "CryoRefusalQA-result.cfg");
            if (File.Exists(reportPath) && File.ReadAllText(reportPath).Contains("freeze_request_called=True"))
            {
                FlightGlobals.ActiveVessel = new Vessel { id = Guid.NewGuid(), loaded = true, packed = false };
                changed = true;
            }
        });
        string report = ReadReport();
        Equal(true, changed, "test changed active vessel only after the single request marker");
        Contains(report, "freeze_request_called=True", "report proves the one request occurred");
        Equal(false, report.Contains("result=REFUSED_AS_EXPECTED"), "wrong active vessel after dispatch cannot pass");
        Equal(0, FreezeModuleCalls(), "wrong-vessel scenario still has no accepted pod call");
    }

    static Game MakeGame(int parts, int crew, bool adaInFreezer)
    {
        var vessel = new Vessel { vesselName = "Pinned QA fixture", id = Guid.NewGuid(), loaded = false, packed = false };
        string[] crewNames = { "Ada Kerman", "Basil Kerman", "Hamish Kerman", "Vikram Kerman" };
        for (int i = 0; i < parts; i++)
        {
            var part = new Part { flightID = (uint)(100 + i) };
            if (i == parts - 1) { part.Modules.Add(new DeepFreezerFake { moduleName = "DeepFreezer" }); }
            vessel.parts.Add(part);
        }
        for (int i = 0; i < crew && i < crewNames.Length; i++)
            vessel.parts[i].protoModuleCrew.Add(new ProtoCrewMember { name = crewNames[i] });
        if (adaInFreezer && parts > 0)
        {
            vessel.parts[0].Modules.Add(new DeepFreezerFake { moduleName = "DeepFreezer" });
            vessel.parts[0].Modules.RemoveAt(0);
            vessel.parts[0].Modules.Add(new DeepFreezerFake { moduleName = "DeepFreezer" });
        }
        foreach (Part part in vessel.parts)
            part.Resources.Add(new PartResource { resourceName = "ElectricCharge", amount = 100, maxAmount = 200, flowState = true });
        var game = new Game { fixtureVessel = vessel, flightState = new FlightState() };
        game.flightState.protoVessels.Add(new ProtoVessel { vesselID = vessel.id });
        return game;
    }

    static void RunProbe(Action onWait = null)
    {
        object instance = Activator.CreateInstance(probeType);
        var method = probeType.GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic);
        var routine = (IEnumerator)method.Invoke(instance, null);
        int guard = 0;
        while (routine.MoveNext())
        {
            if (++guard > 2000) throw new Exception("probe coroutine did not terminate");
            var wait = routine.Current as WaitForSecondsRealtime;
            if (wait != null) Advance(wait.seconds);
            if (onWait != null) onWait();
        }
    }

    static int readyWaits;
    static void Advance(float seconds)
    {
        Time.realtimeSinceStartup += seconds;
        Planetarium.UniversalTime += seconds;
        if (Game.StartCalls > 0 && !FlightGlobals.ready)
        {
            readyWaits++;
            if (FlightDriver.ReadyAfterWaits > 0 && readyWaits >= FlightDriver.ReadyAfterWaits) FlightDriver.SetFlightReady();
        }
    }

    static void ResetEnvironment()
    {
        GamePersistence.LoadCalls = 0; GamePersistence.UpdateScenarioModulesCalls = 0; GamePersistence.SaveCalls = 0;
        GamePersistence.ThreeArgumentSaveCalls = 0; GamePersistence.LastSavedGame = null;
        GamePersistence.LastSaveFile = null; GamePersistence.LastSaveFolder = null; GamePersistence.NextGame = null;
        FlightDriver.StartCalls = 0; Game.StartCalls = 0; LifecycleLog.Calls.Clear();
        GamePersistence.ScenarioModulesUpdated = false; GamePersistence.SavedForNativeStart = false;
        GameEvents.onGameStatePostLoad.Reset();
        FlightDriver.ReadyAfterWaits = 0; readyWaits = 0; FlightDriver.Pause = false;
        FlightDriver.ReadyPause = false; FlightDriver.ReadyRateIndex = 0; FlightDriver.ReadyRate = 1f;
        FlightGlobals.ready = false; FlightGlobals.ActiveVessel = null;
        HighLogic.SaveFolder = "MainMenu"; HighLogic.CurrentGame = null; HighLogic.LoadedScene = GameScenes.MAINMENU;
        TimeWarp.CurrentRateIndex = 0; TimeWarp.CurrentRate = 1f;
        Time.realtimeSinceStartup = 1f; Planetarium.UniversalTime = 1000;
        Application.QuitCount = 0;
        DF.DeepFreeze.APIReady = false; DF.DeepFreeze.InitializeOnNativeGameStart = true;
        DF.DeepFreeze.Instance = new DeepFreezeState();
    }

    static void ResetRoot()
    {
        foreach (string name in new[] { "CryoRefusalQA.scope", "CryoRefusalQA.enable", "CryoRefusalQA.running", "CryoRefusalQA.done", "CryoRefusalQA-result.cfg" })
        {
            string path = Path.Combine(root, name); if (File.Exists(path)) File.Delete(path);
        }
        CopyFixtureInputs(); KSPUtil.ApplicationRootPath = root;
    }
    static void WriteScope() { File.WriteAllText(Path.Combine(root, "CryoRefusalQA.scope"), Scope, new System.Text.UTF8Encoding(false)); }
    static void WriteEnable() { File.WriteAllText(Path.Combine(root, "CryoRefusalQA.enable"), Enable, new System.Text.UTF8Encoding(false)); }
    static void WriteAuthorization() { WriteScope(); WriteEnable(); }
    static void CopyFixtureInputs()
    {
        Copy("saves/MegaCrew Background QA/persistent.sfs");
        Copy("GameData/MegaCrew/Plugins/MegaCrew.dll");
        Copy("GameData/REPOSoftTech/BackgroundResources/BackgroundResources.dll");
        Copy("GameData/MegaCrew/Plugins/Assembly-CSharp.dll");
        Copy("GameData/MegaCrew/Plugins/UnityEngine.CoreModule.dll");
    }
    static void Copy(string relative)
    {
        string source = Path.Combine(fixtureRoot, relative.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(source)) return;
        string target = FromRoot(relative); Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(source, target, true);
    }
    static string FromRoot(string relative) { return Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)); }
    static string ReadReport() { string path = Path.Combine(root, "CryoRefusalQA-result.cfg"); return File.Exists(path) ? File.ReadAllText(path) : ""; }
    static void AssertNativeLifecycleOrder()
    {
        string[] expected = { "GamePersistence.LoadGame", "GamePersistence.UpdateScenarioModules", "GamePersistence.SaveGame(Game,...)", "GameEvents.onGameStatePostLoad.Fire", "game.Start", "GameEvents.onGameStarted.Fire", "DeepFreeze.OnLoad/APIReady" };
        int previous = -1;
        foreach (string step in expected)
        {
            int index = LifecycleLog.Calls.IndexOf(step);
            Equal(true, index > previous, "native KSP lifecycle is ordered: " + step);
            previous = index;
        }
        Equal(0, FlightDriver.StartCalls, "native sequence does not call FlightDriver.StartAndFocusVessel directly");
    }
    static int CountFiles(string first, string second) { return (File.Exists(Path.Combine(root, first)) ? 1 : 0) + (File.Exists(Path.Combine(root, second)) ? 1 : 0); }
    static string ExpectedHash(string relative) { return Hash(Path.Combine(fixtureRoot, relative.Replace('/', Path.DirectorySeparatorChar))); }
    static string Hash(string path) { using (SHA256 sha = SHA256.Create()) using (FileStream s = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(s)).Replace("-", "").ToLowerInvariant(); }
    static void Contains(string text, string expected, string message) { Equal(true, text.Contains(expected), message + " (expected: " + expected + ")"); }
    static int FreezeModuleCalls() { int calls = 0; foreach (Part part in (FlightGlobals.ActiveVessel == null ? new System.Collections.Generic.List<Part>() : FlightGlobals.ActiveVessel.parts)) foreach (PartModule module in part.Modules) { var freezer = module as DeepFreezerFake; if (freezer != null) calls += freezer.FreezeCalls; } return calls; }
    static void Equal<T>(T expected, T actual, string message) { assertions++; if (!System.Object.Equals(expected, actual)) throw new Exception(message + ": expected " + expected + ", got " + actual); }
}
