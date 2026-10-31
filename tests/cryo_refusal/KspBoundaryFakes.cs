using System;
using System.Collections.Generic;

[assembly: System.Reflection.AssemblyVersion("0.0.0.0")]

[AttributeUsage(AttributeTargets.Class)]
public sealed class KSPAddon : Attribute
{
    public enum Startup { MainMenu }
    public KSPAddon(Startup startup, bool once) { }
}

public enum GameScenes { MAINMENU, FLIGHT }

public static class KSPUtil { public static string ApplicationRootPath; }
public static class HighLogic
{
    public static string SaveFolder = "MegaCrew Background QA";
    public static Game CurrentGame;
    public static GameScenes LoadedScene = GameScenes.MAINMENU;
    public static bool LoadedSceneIsFlight { get { return LoadedScene == GameScenes.FLIGHT; } }
}
public static class FlightDriver
{
    public static bool Pause;
    // Kept as a sentinel so tests can prove the probe no longer invokes it directly.
    public static int StartCalls;
    public static int ReadyAfterWaits;
    public static bool ReadyPause;
    public static int ReadyRateIndex;
    public static float ReadyRate = 1f;
    public static void SetFlightReady()
    {
        Pause = ReadyPause;
        TimeWarp.CurrentRateIndex = ReadyRateIndex;
        TimeWarp.CurrentRate = ReadyRate;
        FlightGlobals.ready = true;
        HighLogic.LoadedScene = GameScenes.FLIGHT;
        if (FlightGlobals.ActiveVessel != null) { FlightGlobals.ActiveVessel.loaded = true; FlightGlobals.ActiveVessel.packed = false; }
    }
}
public static class FlightGlobals { public static bool ready; public static Vessel ActiveVessel { get; set; } }
public static class TimeWarp { public static int CurrentRateIndex; public static float CurrentRate = 1f; }
public static class Planetarium { public static double UniversalTime = 123.5; public static double GetUniversalTime() { return UniversalTime; } }

public sealed class ConfigNode
{
    readonly Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
    public ConfigNode(string name) { }
    public void SetValue(string key, string value, bool createIfNotFound) { values[key] = value; }
    public bool HasValue(string key) { return values.ContainsKey(key); }
    public string GetValue(string key) { string value; return values.TryGetValue(key, out value) ? value : null; }
    public void Save(string path)
    {
        var keys = new List<string>(values.Keys); keys.Sort(StringComparer.Ordinal);
        using (var writer = new System.IO.StreamWriter(path, false, new System.Text.UTF8Encoding(false)))
            foreach (string key in keys) writer.WriteLine(key + "=" + values[key]);
    }
}

public enum SaveMode { OVERWRITE }
public static class LifecycleLog
{
    public static readonly List<string> Calls = new List<string>();
    public static void Add(string call) { Calls.Add(call); }
    public static string Joined { get { return String.Join(",", Calls.ToArray()); } }
}
public sealed class ScenarioContainer : List<ProtoScenarioModule> { }
public sealed class ProtoScenarioModule { public string moduleName { get; set; } }
public sealed class Game
{
    public FlightState flightState;
    public Vessel fixtureVessel;
    public ScenarioContainer scenarios = new ScenarioContainer();
    public ConfigNode config = new ConfigNode("GAME");
    public static int StartCalls;
    public void Start()
    {
        StartCalls++;
        LifecycleLog.Add("game.Start");
        GameEvents.onGameStarted.Fire(this);
        FlightGlobals.ActiveVessel = fixtureVessel;
        if (FlightDriver.ReadyAfterWaits == 0) FlightDriver.SetFlightReady();
        else { FlightGlobals.ready = false; HighLogic.LoadedScene = GameScenes.MAINMENU; }
    }
}
public sealed class FlightState { public List<ProtoVessel> protoVessels = new List<ProtoVessel>(); }
public sealed class ProtoVessel { public Guid vesselID; }
public static class GamePersistence
{
    public static int LoadCalls;
    public static int UpdateScenarioModulesCalls;
    public static int SaveCalls;
    public static int ThreeArgumentSaveCalls;
    public static bool ScenarioModulesUpdated;
    public static bool SavedForNativeStart;
    public static string LastSaveFile, LastSaveFolder;
    public static SaveMode LastSaveMode;
    public static Game LastSavedGame;
    public static Game NextGame;
    public static Game LoadGame(string file, string save, bool loadFromBackup, bool loadOnDemand)
    {
        LoadCalls++; LifecycleLog.Add("GamePersistence.LoadGame"); return NextGame;
    }
    public static void UpdateScenarioModules(Game game)
    {
        UpdateScenarioModulesCalls++; LifecycleLog.Add("GamePersistence.UpdateScenarioModules");
        ScenarioModulesUpdated = true;
        if (!game.scenarios.Exists(s => s != null && s.moduleName == "DeepFreeze"))
            game.scenarios.Add(new ProtoScenarioModule { moduleName = "DeepFreeze" });
    }
    public static void SaveGame(Game game, string file, string save, SaveMode mode)
    {
        LastSavedGame = game;
        SaveCalls++; LastSaveFile = file; LastSaveFolder = save; LastSaveMode = mode;
        SavedForNativeStart = true;
        LifecycleLog.Add("GamePersistence.SaveGame(Game,...)" );
    }
    public static void SaveGame(string file, string save, SaveMode mode)
    {
        ThreeArgumentSaveCalls++;
        LifecycleLog.Add("GamePersistence.SaveGame(legacy convenience)");
    }
}
public sealed class FakeGameEvent<T>
{
    public bool WasFired { get; private set; }
    readonly List<Action<T>> listeners = new List<Action<T>>();
    public void Fire(T value) { WasFired = true; LifecycleLog.Add("GameEvents.onGameStatePostLoad.Fire"); }
    public void Reset() { WasFired = false; }
}
public sealed class FakeGameStartEvent
{
    readonly List<Action<Game>> listeners = new List<Action<Game>>();
    public void Add(Action<Game> listener) { listeners.Add(listener); }
    public void Fire(Game game)
    {
        LifecycleLog.Add("GameEvents.onGameStarted.Fire");
        foreach (Action<Game> listener in listeners.ToArray()) listener(game);
    }
    public void Reset() { listeners.Clear(); }
}
public static class GameEvents
{
    public static readonly FakeGameEvent<ConfigNode> onGameStatePostLoad = new FakeGameEvent<ConfigNode>();
    public static readonly FakeGameStartEvent onGameStarted = new FakeGameStartEvent();
}
public sealed class Vessel : UnityEngine.MonoBehaviour
{
    public string vesselName;
    public Guid id;
    public bool loaded;
    public bool packed;
    public readonly List<Part> parts = new List<Part>();
    public int GetCrewCount() { int count = 0; foreach (Part part in parts) if (part != null && part.protoModuleCrew != null) count += part.protoModuleCrew.Count; return count; }
}
public sealed class Part
{
    public uint flightID;
    readonly PartModuleList modules = new PartModuleList();
    public PartModuleList Modules { get { return modules; } }
    public readonly List<ProtoCrewMember> protoModuleCrew = new List<ProtoCrewMember>();
    public readonly List<PartResource> Resources = new List<PartResource>();
}
public class PartModule { public string moduleName; }
public sealed class PartModuleList : List<PartModule> { }
public sealed class PartResource
{
    public string resourceName;
    public double amount;
    public double maxAmount;
    public bool flowState = true;
}
public sealed class ProtoCrewMember { public string name { get; set; } }
