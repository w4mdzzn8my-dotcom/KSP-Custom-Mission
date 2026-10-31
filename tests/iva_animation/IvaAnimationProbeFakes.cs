using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public class MonoBehaviour { }
    public class RuntimeAnimatorController
    {
        public string name { get; set; } = "catalog-controller";
        public AnimationClip[] animationClips { get; set; } = new[] { new AnimationClip() };
    }
    public sealed class AnimationClip { public string name { get; set; } = "idle"; public float length { get; set; } = 1f; }
    public struct AnimatorStateInfo { public int fullPathHash { get; set; } public int shortNameHash { get; set; } }
    public sealed class Animator
    {
        public string name { get; set; } = "catalog-animator";
        public bool enabled { get; set; } = true;
        public RuntimeAnimatorController runtimeAnimatorController { get; set; } = new RuntimeAnimatorController();
        public AnimationClip[] animationClips { get; set; } = new[] { new AnimationClip() };
        public int layerCount { get; set; } = 1;
        public int ReadCount { get; private set; }
        public string GetLayerName(int layer) { ReadCount++; return "Base Layer"; }
        public AnimatorStateInfo GetCurrentAnimatorStateInfo(int layer) { ReadCount++; return new AnimatorStateInfo(); }
        public bool IsInTransition(int layer) { ReadCount++; return false; }
        public AnimatorStateInfo GetNextAnimatorStateInfo(int layer) { ReadCount++; return new AnimatorStateInfo(); }
        public bool HasState(int layer, int hash) { ReadCount++; return hash == StringToHash("wavesToOtherKerbal"); }
        public static int StringToHash(string text) { unchecked { int h = 17; foreach (char c in text) h = h * 31 + c; return h; } }
    }
    public static class Debug { public static void Log(string message) { } public static void LogWarning(string message) { } }
}

[AttributeUsage(AttributeTargets.Class)] public sealed class KSPAddon : Attribute
{
    public enum Startup { Flight }
    public KSPAddon(Startup startup, bool once) { }
}
public sealed class Game { }
public sealed class CameraManager
{
    public enum CameraMode { IVA, Flight }
    public static CameraManager Instance { get; set; } = new CameraManager();
    public CameraMode currentCameraMode { get; set; } = CameraMode.IVA;
}
public static class KSPUtil { public static string ApplicationRootPath { get; set; } }
public static class HighLogic
{
    public static string SaveFolder { get; set; } = "MegaCrew Background QA";
    public static Game CurrentGame { get; set; } = new Game();
    public static bool LoadedSceneIsFlight { get; set; } = true;
}
public static class FlightDriver { public static bool Pause { get; set; } }
public static class TimeWarp { public static int CurrentRateIndex { get; set; } public static float CurrentRate { get; set; } = 1f; }
public static class Planetarium { public static double GetUniversalTime() { return UniversalTime; } public static double UniversalTime { get; set; } = 123.5; }
public static class FlightGlobals { public static Vessel ActiveVessel { get; set; } public static bool ready { get; set; } = true; }
public sealed class Vessel
{
    public Guid id { get; set; } = Guid.NewGuid();
    public bool loaded { get; set; } = true;
    public bool packed { get; set; }
    public bool isEVA { get; set; }
    public string vesselName { get; set; } = "QA vessel";
    public List<Part> parts { get; } = new List<Part>();
}
public sealed class Part
{
    public uint flightID { get; set; }
    public List<ProtoCrewMember> protoModuleCrew { get; set; } = new List<ProtoCrewMember>();
    public InternalModel internalModel { get; set; }
}
public sealed class InternalModel { public List<InternalSeat> seats { get; set; } = new List<InternalSeat>(); }
public sealed class InternalSeat
{
    public ProtoCrewMember crew { get; set; }
    public bool taken { get; set; }
    public Kerbal kerbalRef { get; set; }
}
public sealed class ProtoCrewMember { public string name { get; set; } public InternalSeat seat { get; set; } }
public sealed class Kerbal
{
    public string name { get; set; }
    public ProtoCrewMember protoCrewMember { get; set; }
    public Part InPart { get; set; }
    public UnityEngine.Animator[] Animators { get; set; } = new[] { new UnityEngine.Animator() };
}
namespace MegaCrew
{
    public static class CryoBridge
    {
        public static bool Known { get; set; } = true;
        public static bool Frozen { get; set; }
        public static bool TryFrozen(string name, out bool frozen) { frozen = Frozen; return Known; }
    }
}
