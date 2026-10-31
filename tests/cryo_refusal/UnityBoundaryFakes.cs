[assembly: System.Reflection.AssemblyVersion("0.0.0.0")]
namespace UnityEngine
{
    public class Object
    {
        public static bool operator ==(Object left, Object right) { return ReferenceEquals(left, right); }
        public static bool operator !=(Object left, Object right) { return !ReferenceEquals(left, right); }
        public override bool Equals(object value) { return ReferenceEquals(this, value); }
        public override int GetHashCode() { return base.GetHashCode(); }
    }
    public class MonoBehaviour : Object
    {
        protected void DontDestroyOnLoad(Object value) { }
    }
    public static class Debug
    {
        public static void Log(object message) { }
        public static void LogWarning(object message) { }
        public static void LogError(object message) { }
    }
    public static class Application { public static int QuitCount; public static void Quit() { QuitCount++; } }
    public static class Time { public static float realtimeSinceStartup; }
    public sealed class WaitForSecondsRealtime { public readonly float seconds; public WaitForSecondsRealtime(float value) { seconds = value; } }
}
