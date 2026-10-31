using System;
using System.Collections;
using System.Collections.Generic;

[assembly: System.Reflection.AssemblyVersion("0.0.0.0")]

namespace DF
{
    public sealed class FrozenInfo { public Guid vesselID; public uint partID; }
    public sealed class DeepFreezeState
    {
        public IDictionary FrozenKerbals { get; set; } = new Dictionary<string, object>(StringComparer.Ordinal);
    }
    public static class DeepFreeze
    {
        public static bool APIReady;
        public static bool InitializeOnNativeGameStart = true;
        public static DeepFreezeState Instance;
        static DeepFreeze() { GameEvents.onGameStarted.Add(ObserveNativeGameStart); }
        public static void ObserveNativeGameStart(Game game)
        {
            if (!InitializeOnNativeGameStart || game == null ||
                !System.Object.ReferenceEquals(HighLogic.CurrentGame, game) ||
                HighLogic.SaveFolder != "MegaCrew Background QA" ||
                !GamePersistence.ScenarioModulesUpdated || !GamePersistence.SavedForNativeStart ||
                !GameEvents.onGameStatePostLoad.WasFired ||
                !game.scenarios.Exists(s => s != null && s.moduleName == "DeepFreeze")) return;
            if (Instance == null) Instance = new DeepFreezeState();
            APIReady = true;
            LifecycleLog.Add("DeepFreeze.OnLoad/APIReady");
        }
    }
}

public sealed class DeepFreezerFake : PartModule
{
    public bool DFIIsFreezeActive { get; set; }
    public bool DFIIsThawActive { get; set; }
    public bool DFIcrewXferTOActive { get; set; }
    public bool DFIcrewXferFROMActive { get; set; }
    public int FreezeCalls { get; private set; }
    public void beginFreezeKerbal(ProtoCrewMember member) { FreezeCalls++; }
    public void beginThawKerbal(string name) { }
}
