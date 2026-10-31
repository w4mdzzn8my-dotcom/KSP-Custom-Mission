using System;
using MegaCrew;

namespace MegaCrew
{
    // Production-binding stub for the isolated bridge runner.
    public sealed class MissionConsole
    {
        public static bool Ready;
        public static string CurrentReason = String.Empty;
        public static int Calls;
        public static string LastName;
        public static Guid LastVessel;

        public static bool CanPerformDuty(string crewName, Guid vesselId, out string reason)
        {
            Calls++;
            LastName = crewName;
            LastVessel = vesselId;
            reason = CurrentReason;
            return Ready;
        }
    }
}

sealed class MissingReadinessApi { }
sealed class WrongReadinessApi
{
    public static bool CanPerformDuty(string name, Guid vessel, out object reason)
    { reason = null; return true; }
}
sealed class WrongReturnReadinessApi
{
    public static int CanPerformDuty(string name, Guid vessel, out string reason)
    { reason = String.Empty; return 1; }
}
sealed class ThrowingReadinessApi
{
    public static bool CanPerformDuty(string name, Guid vessel, out string reason)
    { reason = "partial"; throw new InvalidOperationException("query interrupted"); }
}
sealed class EmptyDeniedReadinessApi
{
    public static bool CanPerformDuty(string name, Guid vessel, out string reason)
    { reason = "  "; return false; }
}
sealed class InconsistentReadinessApi
{
    public static bool CanPerformDuty(string name, Guid vessel, out string reason)
    { reason = "not actually ready"; return true; }
}

static class DutyReadinessBridgeTests
{
    static void Is(bool value, string name)
    { if (!value) throw new Exception(name); }

    static bool TryDispatch(Type type, string name, Guid vessel, ref int dispatches, out string reason)
    {
        if (!DutyReadinessBridge.CanPerformDuty(name, vessel, out reason, type)) return false;
        dispatches++;
        return true;
    }

    static void Main()
    {
        Guid vessel = Guid.NewGuid();
        MissionConsole.Calls = 0;
        MissionConsole.Ready = true;
        MissionConsole.CurrentReason = String.Empty;
        string reason;
        Is(DutyReadinessBridge.CanPerformDuty("Ada Kerman", vessel, out reason), "public production binding allows live-ready duty");
        Is(reason == String.Empty && MissionConsole.Calls == 1, "success result and call count");
        MissionConsole.Ready = false;
        MissionConsole.CurrentReason = "This crew member must be awake aboard the mission vessel.";
        Is(!DutyReadinessBridge.CanPerformDuty("Basil Kerman", vessel, out reason), "second query observes changed live state");
        Is(MissionConsole.Calls == 2 && MissionConsole.LastName == "Basil Kerman" && MissionConsole.LastVessel == vessel,
            "exact crew name and vessel GUID forwarded on each call");
        Is(reason == MissionConsole.CurrentReason, "authoritative denial reason forwarded");

        Is(!DutyReadinessBridge.CanPerformDuty("  ", vessel, out reason) && !String.IsNullOrWhiteSpace(reason),
            "blank crew identity fails closed with a reason");
        Is(!DutyReadinessBridge.CanPerformDuty("Ada Kerman", Guid.Empty, out reason) && !String.IsNullOrWhiteSpace(reason),
            "empty vessel identity fails closed with a reason");
        Is(!DutyReadinessBridge.CanPerformDuty("Ada Kerman", vessel, out reason, typeof(MissingReadinessApi)) &&
            !String.IsNullOrWhiteSpace(reason), "missing method fails closed clearly");
        Is(!DutyReadinessBridge.CanPerformDuty("Ada Kerman", vessel, out reason, typeof(WrongReadinessApi)) &&
            !String.IsNullOrWhiteSpace(reason), "wrong out-parameter signature fails closed clearly");
        Is(!DutyReadinessBridge.CanPerformDuty("Ada Kerman", vessel, out reason, typeof(WrongReturnReadinessApi)) &&
            !String.IsNullOrWhiteSpace(reason), "wrong return type fails closed clearly");
        Is(!DutyReadinessBridge.CanPerformDuty("Ada Kerman", vessel, out reason, typeof(ThrowingReadinessApi)) &&
            reason.Contains("query interrupted"), "throwing readiness API fails closed and reports cause");
        Is(!DutyReadinessBridge.CanPerformDuty("Ada Kerman", vessel, out reason, typeof(EmptyDeniedReadinessApi)) &&
            !String.IsNullOrWhiteSpace(reason), "denial with empty reason gets clear fallback");
        Is(!DutyReadinessBridge.CanPerformDuty("Ada Kerman", vessel, out reason, typeof(InconsistentReadinessApi)) &&
            !String.IsNullOrWhiteSpace(reason), "positive result with contradictory reason fails closed");

        int dispatches = 0;
        Is(!TryDispatch(typeof(EmptyDeniedReadinessApi), "Ada Kerman", vessel, ref dispatches, out reason) && dispatches == 0,
            "denied readiness cannot reach the guarded mutation");
        MissionConsole.Ready = true;
        MissionConsole.CurrentReason = String.Empty;
        Is(TryDispatch(typeof(MissionConsole), "Ada Kerman", vessel, ref dispatches, out reason) && dispatches == 1,
            "live approved readiness reaches guarded mutation");
        Console.WriteLine("PASS: exact live query binding, forwarding, fail-closed API errors, and denial-before-dispatch.");
    }
}
