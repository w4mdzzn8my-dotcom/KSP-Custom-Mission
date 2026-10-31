using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x=x; this.y=y; } }
    public struct Vector3 { public float x, y, z; public Vector3(float x, float y, float z) { this.x=x; this.y=y; this.z=z; } }
    public sealed class Transform { public Vector3 position, forward; }
    public sealed class GUILayoutOption { }
    public static class GUILayout
    {
        static readonly Queue<int> selections = new Queue<int>();
        public static readonly List<string> Labels = new List<string>();
        public static void Choose(params int[] values) { selections.Clear(); foreach (int value in values) selections.Enqueue(value); }
        public static void Label(string value) { Labels.Add(value); }
        public static Vector2 BeginScrollView(Vector2 value, params GUILayoutOption[] options) { return value; }
        public static void EndScrollView() { }
        public static int SelectionGrid(int selected, string[] labels, int columns, params GUILayoutOption[] options)
        { return selections.Count == 0 ? selected : selections.Dequeue(); }
        public static GUILayoutOption Height(float value) { return new GUILayoutOption(); }
        public static GUILayoutOption MinHeight(float value) { return new GUILayoutOption(); }
    }
}

public sealed class CelestialBody { public string bodyName; }
public sealed class PartInfo { public string title; }
public class PartModule { }
public sealed class ModuleDockingNode : PartModule
{
    public string nodeTransformName;
    string currentState;
    public string state { get { return currentState; } set { currentState = value; MutationCount++; } }
    public UnityEngine.Transform transform;
    public int MutationCount;
    public UnityEngine.Transform GetTransform() { return transform; }
    public void Dock() { MutationCount++; }
    public void Undock() { MutationCount++; }
    public void Decouple() { MutationCount++; }
}
public sealed class Part
{
    public uint flightID;
    public string name;
    public PartInfo partInfo;
    public List<PartModule> Modules = new List<PartModule>();
}
public sealed class Vessel
{
    public Guid id;
    public CelestialBody mainBody;
    public bool loaded = true, packed;
    public List<Part> parts = new List<Part>();
    public string vesselName;
}
public static class FlightGlobals
{
    public static Vessel ActiveVessel;
    public static List<Vessel> Vessels = new List<Vessel>();
    public static bool ready = true;
}
public static class FlightDriver { public static bool Pause; }
public static class TimeWarp { public static int CurrentRateIndex; public static float CurrentRate = 1.0f; }
