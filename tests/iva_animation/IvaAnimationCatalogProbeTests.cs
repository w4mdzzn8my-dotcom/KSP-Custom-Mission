using System;
using System.IO;
using System.Reflection;
using MegaCrew.IvaAnimationQA;

static class IvaAnimationCatalogProbeTests
{
    const string Scope = "MEGACREW_DISPOSABLE_IVA_ANIMATION_QA_V1\n";
    const string Enable = "AUTHORIZE ONE READ-ONLY IVA ANIMATION CATALOG FOR SAVE: MegaCrew Background QA\n";
    static int assertions;

    static void Main()
    {
        TestRefusalBoundaries();
        TestOneShotCompleteCatalogAndUncrewedPart();
        TestUnknownCryoIsInconclusive();
        TestMissingRendererIsInconclusive();
        TestMismatchedRenderedCrewIdentityIsInconclusive();
        Console.WriteLine("IVA animation catalog probe tests passed (" + assertions + " assertions).");
    }

    static void TestRefusalBoundaries()
    {
        RunCase("unscoped", scoped: false, enabled: true, save: "MegaCrew Background QA", iva: true, paused: false, expectWrite: false);
        RunCase("wrong-save", scoped: true, enabled: true, save: "Other Save", iva: true, paused: false, expectWrite: false);
        RunCase("no-iva", scoped: true, enabled: true, save: "MegaCrew Background QA", iva: false, paused: false, expectWrite: false);
        RunCase("paused", scoped: true, enabled: true, save: "MegaCrew Background QA", iva: true, paused: true, expectWrite: false);
    }

    static void TestOneShotCompleteCatalogAndUncrewedPart()
    {
        using (var box = new Case("complete", true, true, "MegaCrew Background QA", true, false))
        {
            Vessel vessel = MakeVessel(rendered: true);
            vessel.parts.Add(new Part { flightID = 202 }); // An uncrewed part is not a missing seat.
            FlightGlobals.ActiveVessel = vessel;
            var probe = box.StartProbe();
            box.Update(probe);
            Equal(true, File.Exists(Path.Combine(box.Root, "IvaAnimationQA.done")), "successful claim moves running marker to done");
            Equal(false, File.Exists(Path.Combine(box.Root, "IvaAnimationQA.enable")), "enable token is consumed");
            string report = File.ReadAllText(Path.Combine(box.Root, "IvaAnimationQA.report.txt"));
            Contains(report, "probe_completed=true", "matching seat/render binding completes catalog");
            Contains(report, "occupied_seats=1", "uncrewed parts do not inflate occupied seat count");
            Contains(report, "rendered_crew=1", "matching rendered kerbal is counted");
            Contains(report, "animation_qualification=false", "catalog never claims animation qualification");
            Contains(report, "result=CATALOG_COMPLETE_ONLY", "complete output is explicitly catalog-only");
            Equal(false, report.Contains("play="), "report does not contain animation play request");
            Equal(true, vessel.parts[0].internalModel.seats[0].kerbalRef.Animators[0].ReadCount > 0, "catalog uses animator read APIs");
            int fileCount = CountFiles(box.Root, "IvaAnimationQA.*");
            box.Update(probe);
            Equal(fileCount, CountFiles(box.Root, "IvaAnimationQA.*"), "one-shot completion does not create a second marker/report");
        }
    }

    static void TestUnknownCryoIsInconclusive()
    {
        using (var box = new Case("unknown-cryo", true, true, "MegaCrew Background QA", true, false))
        {
            FlightGlobals.ActiveVessel = MakeVessel(rendered: true);
            MegaCrew.CryoBridge.Known = false;
            var probe = box.StartProbe(); box.Update(probe);
            string report = File.ReadAllText(Path.Combine(box.Root, "IvaAnimationQA.report.txt"));
            Contains(report, "freeze_state=unknown", "unknown cryo state is recorded");
            Contains(report, "probe_completed=false", "unknown cryo state blocks completeness");
            Contains(report, "result=INCONCLUSIVE", "unknown cryo state is inconclusive");
        }
        MegaCrew.CryoBridge.Known = true;
    }

    static void TestMissingRendererIsInconclusive()
    {
        using (var box = new Case("missing-renderer", true, true, "MegaCrew Background QA", true, false))
        {
            FlightGlobals.ActiveVessel = MakeVessel(rendered: false);
            var probe = box.StartProbe(); box.Update(probe);
            string report = File.ReadAllText(Path.Combine(box.Root, "IvaAnimationQA.report.txt"));
            Contains(report, "rendered_kerbal=unavailable_or_mismatched", "seat without matching rendered Kerbal is identified");
            Contains(report, "probe_completed=false", "missing renderer blocks completeness");
            Contains(report, "result=INCONCLUSIVE", "missing renderer is inconclusive");
        }
    }

    static void TestMismatchedRenderedCrewIdentityIsInconclusive()
    {
        using (var box = new Case("mismatched-renderer", true, true, "MegaCrew Background QA", true, false))
        {
            Vessel vessel = MakeVessel(rendered: true);
            vessel.parts[0].internalModel.seats[0].kerbalRef.protoCrewMember = new ProtoCrewMember { name = "Different crew" };
            FlightGlobals.ActiveVessel = vessel;
            var probe = box.StartProbe(); box.Update(probe);
            string report = File.ReadAllText(Path.Combine(box.Root, "IvaAnimationQA.report.txt"));
            Contains(report, "rendered_kerbal=unavailable_or_mismatched", "rendered Kerbal must reference the same occupied crew member");
            Contains(report, "probe_completed=false", "mismatched member identity blocks completeness");
            Contains(report, "result=INCONCLUSIVE", "mismatched member identity is inconclusive");
        }
    }

    static void RunCase(string name, bool scoped, bool enabled, string save, bool iva, bool paused, bool expectWrite)
    {
        using (var box = new Case(name, scoped, enabled, save, iva, paused))
        {
            FlightGlobals.ActiveVessel = MakeVessel(rendered: true);
            var probe = box.StartProbe(); box.Update(probe);
            Equal(expectWrite, File.Exists(Path.Combine(box.Root, "IvaAnimationQA.running")) || File.Exists(Path.Combine(box.Root, "IvaAnimationQA.done")) || File.Exists(Path.Combine(box.Root, "IvaAnimationQA.failed")), name + " does not claim/write markers");
            Equal(expectWrite, File.Exists(Path.Combine(box.Root, "IvaAnimationQA.report.txt")), name + " report presence follows claim");
            if (!expectWrite && enabled) Equal(true, File.Exists(Path.Combine(box.Root, "IvaAnimationQA.enable")), name + " preserves unconsumed enable token");
        }
    }

    static Vessel MakeVessel(bool rendered)
    {
        var vessel = new Vessel { vesselName = "Fixture" };
        var crew = new ProtoCrewMember { name = "Ada" };
        var part = new Part { flightID = 101, protoModuleCrew = new System.Collections.Generic.List<ProtoCrewMember> { crew }, internalModel = new InternalModel() };
        var seat = new InternalSeat { crew = crew, taken = true };
        var kerbal = new Kerbal { name = "Ada rendered", protoCrewMember = crew, InPart = part };
        if (!rendered) { seat.kerbalRef = null; crew.seat = null; }
        else { seat.kerbalRef = kerbal; crew.seat = seat; }
        part.internalModel.seats.Add(seat); vessel.parts.Add(part);
        return vessel;
    }

    sealed class Case : IDisposable
    {
        public readonly string Root;
        public Case(string name, bool scoped, bool enabled, string save, bool iva, bool paused)
        {
            Root = Path.Combine(Path.GetTempPath(), "iva-probe-tests-" + Guid.NewGuid().ToString("N"), "KSP-Hermes-QA");
            Directory.CreateDirectory(Root); KSPUtil.ApplicationRootPath = Root;
            if (scoped) File.WriteAllText(Path.Combine(Root, "IvaAnimationQA.scope"), Scope);
            if (enabled) File.WriteAllText(Path.Combine(Root, "IvaAnimationQA.enable"), Enable);
            HighLogic.SaveFolder = save; HighLogic.CurrentGame = new Game(); HighLogic.LoadedSceneIsFlight = true;
            FlightGlobals.ready = true; FlightDriver.Pause = paused;
            TimeWarp.CurrentRateIndex = 0; TimeWarp.CurrentRate = 1f;
            CameraManager.Instance.currentCameraMode = iva ? CameraManager.CameraMode.IVA : CameraManager.CameraMode.Flight;
            Planetarium.UniversalTime = 123.5;
            MegaCrew.CryoBridge.Known = true; MegaCrew.CryoBridge.Frozen = false;
        }
        public IvaAnimationCatalogProbe StartProbe() { var p = new IvaAnimationCatalogProbe(); Call(p, "Start"); return p; }
        public void Update(IvaAnimationCatalogProbe p) { Call(p, "Update"); }
        public void Dispose() { try { Directory.Delete(Path.GetDirectoryName(Root), true); } catch { } }
    }

    static void Call(object instance, string method) { instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, null); }
    static int CountFiles(string dir, string pattern) { return Directory.GetFiles(dir, pattern).Length; }
    static void Contains(string actual, string expected, string message) { Equal(true, actual.Contains(expected), message + " (expected: " + expected + ")"); }
    static void Equal<T>(T expected, T actual, string message) { assertions++; if (!Object.Equals(expected, actual)) throw new Exception(message + ": expected " + expected + ", got " + actual); }
}
