// Disposable, in-memory KSP maneuver-node QA. Never burns, warps, or saves.
using System;
using System.Collections;
using System.IO;
using System.Threading;
using UnityEngine;
using MegaCrew.Navigation;

internal static class NodeQaAuthorization
{
    internal const string SaveName = "MegaCrew Navigation QA";
    internal const string MarkerName = "CircularNodeQA.enable";
    internal const string ConsumedName = "CircularNodeQA.consumed";
    internal const string MarkerText = "AUTHORIZE ONE IN-MEMORY NODE QA RUN FOR SAVE: MegaCrew Navigation QA";
    static int armed;
    static int consumed;

    internal static bool TryConsumeEnableMarker()
    {
        string root = KSPUtil.ApplicationRootPath;
        string savePath = Path.Combine(root, Path.Combine("saves", Path.Combine(SaveName, "persistent.sfs")));
        string marker = Path.Combine(root, MarkerName);
        string consumedMarker = Path.Combine(root, ConsumedName);
        if (!File.Exists(marker)) return false;
        if (!File.Exists(savePath))
        {
            Debug.LogError("[CircularNodeQA] Refusing: exact disposable save is missing: " + savePath);
            return false;
        }
        if (File.Exists(consumedMarker))
        {
            Debug.LogError("[CircularNodeQA] Refusing: consumed marker already exists; remove it manually before a new explicitly authorized run.");
            return false;
        }

        try
        {
            string contents = File.ReadAllText(marker).TrimEnd('\r', '\n');
            if (!String.Equals(contents, MarkerText, StringComparison.Ordinal))
            {
                Debug.LogError("[CircularNodeQA] Refusing: enable marker contents did not exactly match the documented authorization string.");
                return false;
            }
            File.Move(marker, consumedMarker);
            Interlocked.Exchange(ref armed, 1);
            Debug.Log("[CircularNodeQA] Exact authorization consumed for disposable save '" + SaveName + "'.");
            return true;
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref armed, 0);
            Debug.LogError("[CircularNodeQA] Refusing: could not atomically consume marker: " + ex.Message);
            return false;
        }
    }

    internal static bool ConsumeArmOnce()
    {
        if (Interlocked.Exchange(ref consumed, 1) != 0) return false;
        return Interlocked.Exchange(ref armed, 0) == 1;
    }

    internal static void Disarm() { Interlocked.Exchange(ref armed, 0); }
}

[KSPAddon(KSPAddon.Startup.MainMenu, false)]
public sealed class CircularNodeQaArm : MonoBehaviour
{
    IEnumerator Start()
    {
        // Avoid work during the first menu frame, but never poll or auto-launch later.
        yield return new WaitForSecondsRealtime(2f);
        if (!NodeQaAuthorization.TryConsumeEnableMarker()) yield break;

        try
        {
            Game game = GamePersistence.LoadGame("persistent", NodeQaAuthorization.SaveName, true, false);
            if (game == null) throw new InvalidOperationException("GamePersistence did not load the exact disposable save.");
            HighLogic.SaveFolder = NodeQaAuthorization.SaveName;
            HighLogic.CurrentGame = game;
            Debug.Log("[CircularNodeQA] Loading disposable save vessel index 0 into flight.");
            FlightDriver.StartAndFocusVessel(game, 0);
        }
        catch (Exception ex)
        {
            NodeQaAuthorization.Disarm();
            Debug.LogError("[CircularNodeQA] Setup refused/failed after consuming marker: " + ex);
        }
    }
}

[KSPAddon(KSPAddon.Startup.Flight, false)]
public sealed class CircularNodeQaFlight : MonoBehaviour
{
    const double TargetPeA = 80000.0;
    const double MaxFixtureEccentricity = 1e-5;
    const double BurnLeadSeconds = 120.0;
    const double PreviewTargetToleranceMetres = 1.0;
    const double StockPeToleranceMetres = 5.0;
    const double StockApToleranceMetres = 5.0;
    const double TimeWarpTolerance = 0.001;
    const float ReadyDeadlineSeconds = 12f;
    const float SolverDeadlineSeconds = 8f;

    Vessel testVessel;
    PatchedConicSolver testSolver;
    ManeuverNode ownedNode;
    bool ran;
    bool cleaned;
    bool cleanupAttempted;
    bool cancelled;
    bool completed;
    bool cleanupVerified;

    IEnumerator Start()
    {
        if (HighLogic.SaveFolder != NodeQaAuthorization.SaveName || !NodeQaAuthorization.ConsumeArmOnce()) yield break;
        ran = true;
        float readyDeadline = Time.realtimeSinceStartup + ReadyDeadlineSeconds;
        while (!FlightReady())
        {
            if (cancelled || !HighLogic.LoadedSceneIsFlight)
            {
                Fail("Scene/addon changed while waiting for vessel readiness.");
                ReportSummary();
                yield break;
            }
            if (Time.realtimeSinceStartup >= readyDeadline)
            {
                Fail("Timed out waiting for an unpacked active flight vessel.");
                ReportSummary();
                yield break;
            }
            yield return null;
        }

        testVessel = FlightGlobals.ActiveVessel;
        testSolver = testVessel.patchedConicSolver;
        if (!PreconditionsPass()) { ReportSummary(); yield break; }

        CircularLowerPeriapsisProposal preview;
        double predictedPeA, predictedApA;
        try
        {
            double now = Planetarium.GetUniversalTime();
            double burnUT = now + BurnLeadSeconds;
            preview = CircularLowerPeriapsisPreview.Create(testVessel, burnUT,
                TargetPeA, MaxFixtureEccentricity, PreviewTargetToleranceMetres);
            BuildIndependentPrediction(preview, out predictedPeA, out predictedApA);
        }
        catch (Exception ex)
        {
            Fail("Preview/independent prediction rejected: " + ex.Message);
            ReportSummary();
            yield break;
        }

        Check("detached proposal PeA agrees with independent state-vector invariants",
            Math.Abs(preview.PredictedPeriapsisAltitude - predictedPeA) <= PreviewTargetToleranceMetres,
            "proposal=" + F(preview.PredictedPeriapsisAltitude) + "m independent=" + F(predictedPeA) + "m tolerance=" + F(PreviewTargetToleranceMetres) + "m");
        Check("detached target PeA is met", Math.Abs(predictedPeA - TargetPeA) <= PreviewTargetToleranceMetres,
            "predicted=" + F(predictedPeA) + "m target=" + F(TargetPeA) + "m tolerance=" + F(PreviewTargetToleranceMetres) + "m");
        if (failed) { ReportSummary(); yield break; }

        try
        {
            if (!StillSafeToMutate(false)) throw new InvalidOperationException("Preconditions changed immediately before node creation.");
            ownedNode = testSolver.AddManeuverNode(preview.BurnUT);
            if (ownedNode == null) throw new InvalidOperationException("AddManeuverNode returned null.");
            // ManeuverNode DeltaV uses (radial-plus, normal-minus, prograde).
            ownedNode.DeltaV = new global::Vector3d(0.0, 0.0, preview.SignedProgradeDeltaV);
            bool componentsMatch = Math.Abs(ownedNode.DeltaV.x) <= 1e-9 &&
                Math.Abs(ownedNode.DeltaV.y) <= 1e-9 &&
                Math.Abs(ownedNode.DeltaV.z - preview.SignedProgradeDeltaV) <= 1e-9;
            Check("node stores expected R+/N-/P+ components", componentsMatch,
                "x=" + F(ownedNode.DeltaV.x) + " y=" + F(ownedNode.DeltaV.y) + " z=" + F(ownedNode.DeltaV.z) +
                " expectedPrograde=" + F(preview.SignedProgradeDeltaV) + "m/s tolerance=1e-9m/s");
            if (!componentsMatch) throw new InvalidOperationException("Maneuver node DeltaV components did not retain the assigned prograde value.");
            testSolver.UpdateFlightPlan();
        }
        catch (Exception ex)
        {
            Fail("Node creation/update failed: " + ex.Message);
            CleanupOwnedNode();
            ReportSummary();
            yield break;
        }

        float solverDeadline = Time.realtimeSinceStartup + SolverDeadlineSeconds;
        while (Time.realtimeSinceStartup < solverDeadline)
        {
            bool safe = false;
            bool patchReady = false;
            string waitError = null;
            try { safe = StillSafeToMutate(true); }
            catch (Exception ex) { waitError = "Safe-state recheck threw: " + ex.Message; }
            if (waitError == null && safe)
            {
                try { patchReady = ownedNode.nextPatch != null && ownedNode.nextPatch.activePatch; }
                catch (Exception ex) { waitError = "nextPatch polling threw: " + ex.Message; }
            }
            if (waitError != null || !safe)
            {
                Fail(waitError ?? "Vessel, warp or node-list conditions changed while waiting for stock prediction.");
                CleanupOwnedNode();
                ReportSummary();
                yield break;
            }
            if (patchReady) break;
            yield return new WaitForSecondsRealtime(0.25f);
        }

        Orbit stock = null;
        try
        {
            if (ownedNode != null && ownedNode.nextPatch != null && ownedNode.nextPatch.activePatch)
                stock = ownedNode.nextPatch;
            else
                Fail("Stock patched-conic nextPatch did not become active before bounded timeout.");
        }
        catch (Exception ex)
        {
            Fail("Stock nextPatch read failed: " + ex.Message);
        }

        if (stock != null)
        {
            try
            {
                bool sameBody = stock.referenceBody == preview.Body;
                Check("stock nextPatch reference body matches proposal body", sameBody,
                    "proposal=" + (preview.Body == null ? "null" : preview.Body.bodyName) + " stock=" + (stock.referenceBody == null ? "null" : stock.referenceBody.bodyName));
                Check("stock nextPatch PeA matches detached prediction",
                    IsFinite(stock.PeA) && Math.Abs(stock.PeA - predictedPeA) <= StockPeToleranceMetres,
                    "stock=" + F(stock.PeA) + "m detached=" + F(predictedPeA) + "m tolerance=" + F(StockPeToleranceMetres) + "m");
                Check("stock nextPatch ApA matches detached prediction",
                    IsFinite(stock.ApA) && Math.Abs(stock.ApA - predictedApA) <= StockApToleranceMetres,
                    "stock=" + F(stock.ApA) + "m detached=" + F(predictedApA) + "m tolerance=" + F(StockApToleranceMetres) + "m");
            }
            catch (Exception ex)
            {
                Fail("Stock prediction value read failed: " + ex.Message);
            }
        }
        CleanupOwnedNode();
        if (cleanupVerified && !failed) completed = true;
        ReportSummary();
    }

    bool FlightReady()
    {
        Vessel vessel = FlightGlobals.ActiveVessel;
        return HighLogic.LoadedSceneIsFlight && vessel != null && vessel.loaded && !vessel.packed &&
            vessel.situation == Vessel.Situations.ORBITING && vessel.orbit != null &&
            vessel.patchedConicSolver != null;
    }

    bool PreconditionsPass()
    {
        if (!FlightReady() || FlightGlobals.ActiveVessel != testVessel)
        { Fail("Refusing: require the same active, loaded, unpacked vessel in orbit."); return false; }
        if (HighLogic.SaveFolder != NodeQaAuthorization.SaveName)
        { Fail("Refusing: current save folder is not the exact disposable QA save."); return false; }
        if (!AtOneX()) { Fail("Refusing: time warp is active. Set stock warp to 1x manually and re-arm with a fresh marker."); return false; }
        if (testSolver.maneuverNodes == null || testSolver.maneuverNodes.Count != 0)
        { Fail("Refusing: vessel already has maneuver nodes."); return false; }
        if (testVessel.orbit == null || !testVessel.orbit.activePatch || testVessel.orbit.referenceBody == null)
        { Fail("Refusing: active orbit patch/body is invalid."); return false; }
        if (testVessel.orbit.referenceBody.bodyName != "Kerbin")
        { Fail("Refusing: fixture reference body must be Kerbin."); return false; }
        if (!EnginesIdle())
        { Fail("Refusing: an engine is producing or spooling thrust. Disable it manually before arming."); return false; }
        if (!IsFinite(testVessel.orbit.eccentricity) || testVessel.orbit.eccentricity < 0.0 ||
            testVessel.orbit.eccentricity > MaxFixtureEccentricity)
        { Fail("Refusing: disposable fixture is not circular within e <= " + MaxFixtureEccentricity + "."); return false; }
        if (!IsFinite(testVessel.orbit.PeA) || TargetPeA <= 0.0 || TargetPeA >= testVessel.orbit.PeA)
        { Fail("Refusing: fixture periapsis must be above target 80 km."); return false; }
        if (testSolver.maneuverNodes.Count != 0)
        { Fail("Refusing: node list changed during preflight."); return false; }
        Debug.Log("[CircularNodeQA] Preconditions PASS: exact save, loaded/unpacked orbiting vessel, zero nodes, 1x, e=" + F(testVessel.orbit.eccentricity));
        return true;
    }

    bool StillSafeToMutate(bool expectOwnedNode)
    {
        if (cancelled || cleaned || !HighLogic.LoadedSceneIsFlight || HighLogic.SaveFolder != NodeQaAuthorization.SaveName ||
            testVessel == null || !testVessel.loaded || FlightGlobals.ActiveVessel != testVessel || testVessel.packed ||
            testVessel.situation != Vessel.Situations.ORBITING || testSolver == null || !AtOneX() || !EnginesIdle() ||
            testSolver.maneuverNodes == null) return false;
        if (expectOwnedNode) return testSolver.maneuverNodes.Count == 1 && ContainsOwnedNode();
        return ownedNode == null && testSolver.maneuverNodes.Count == 0;
    }

    bool AtOneX()
    {
        return IsFinite(TimeWarp.CurrentRate) && Math.Abs(TimeWarp.CurrentRate - 1.0) <= TimeWarpTolerance &&
            TimeWarp.CurrentRateIndex == 0;
    }

    bool EnginesIdle()
    {
        if (testVessel == null || testVessel.parts == null) return false;
        foreach (Part part in testVessel.parts)
        {
            foreach (PartModule module in part.Modules)
            {
                ModuleEngines engine = module as ModuleEngines;
                if (engine != null && (engine.finalThrust > 0.01f || engine.currentThrottle > 0.001f || engine.requestedThrottle > 0.001f))
                    return false;
            }
        }
        return true;
    }

    void BuildIndependentPrediction(CircularLowerPeriapsisProposal preview,
        out double peA, out double apA)
    {
        Orbit start = preview.StartingOrbit;
        global::Vector3d r = start.getRelativePositionAtUT(preview.BurnUT);
        global::Vector3d v = start.getOrbitalVelocityAtUT(preview.BurnUT);
        double speed = v.magnitude;
        double radius = r.magnitude;
        if (!IsFinite(speed) || speed <= 0.0 || !IsFinite(radius) || radius <= preview.Body.Radius)
            throw new InvalidOperationException("Invalid state vector at requested UT.");
        global::Vector3d vAfter = v + v / speed * preview.SignedProgradeDeltaV;

        // Independent osculating-element calculation from specific energy and angular momentum.
        global::Vector3d h = Cross(r, vAfter);
        double hMagnitude = h.magnitude;
        double energy = 0.5 * Dot(vAfter, vAfter) - preview.Body.gravParameter / radius;
        if (!IsFinite(hMagnitude) || hMagnitude <= 0.0 || !IsFinite(energy) || energy >= 0.0)
            throw new InvalidOperationException("Candidate invariant calculation is not a bound ellipse.");
        double semiMajorAxis = -preview.Body.gravParameter / (2.0 * energy);
        double eccentricitySquared = 1.0 + 2.0 * energy * hMagnitude * hMagnitude /
            (preview.Body.gravParameter * preview.Body.gravParameter);
        if (!IsFinite(semiMajorAxis) || semiMajorAxis <= 0.0 || !IsFinite(eccentricitySquared) || eccentricitySquared < -1e-12)
            throw new InvalidOperationException("Independent conic elements are invalid.");
        double eccentricity = Math.Sqrt(Math.Max(0.0, eccentricitySquared));
        if (eccentricity >= 1.0) throw new InvalidOperationException("Independent conic is not elliptical.");
        peA = semiMajorAxis * (1.0 - eccentricity) - preview.Body.Radius;
        apA = semiMajorAxis * (1.0 + eccentricity) - preview.Body.Radius;
        if (!IsFinite(peA) || !IsFinite(apA) || peA <= 0.0)
            throw new InvalidOperationException("Independent periapsis/apoapsis invalid or intersects body.");

        // Keep detached Orbit available only as an extra API sanity check, not the independent expected result.
        Orbit detached = new Orbit();
        detached.UpdateFromStateVectors(r, vAfter, preview.Body, preview.BurnUT);
        if (!IsFinite(detached.PeA) || !IsFinite(detached.ApA) ||
            Math.Abs(detached.PeA - peA) > 0.01 || Math.Abs(detached.ApA - apA) > 0.01)
            throw new InvalidOperationException("Detached Orbit API does not agree with independent conic invariants within 0.01 m.");
        Debug.Log("[CircularNodeQA] PASS independent energy/angular-momentum conic matches detached Orbit within 0.01 m.");
    }

    bool ContainsOwnedNode()
    {
        if (testSolver == null || testSolver.maneuverNodes == null || ownedNode == null) return false;
        foreach (ManeuverNode node in testSolver.maneuverNodes)
            if (System.Object.ReferenceEquals(node, ownedNode)) return true;
        return false;
    }

    void CleanupOwnedNode(bool retry = false)
    {
        if (cleaned || (cleanupAttempted && !retry)) return;
        cleanupAttempted = true;
        bool removed = false;
        bool hadOwnedNode = ownedNode != null;
        try
        {
            if (hadOwnedNode && ContainsOwnedNode())
            {
                ownedNode.RemoveSelf();
                removed = true;
                if (testSolver != null) testSolver.UpdateFlightPlan();
            }
        }
        catch (Exception ex)
        {
            failed = true;
            Debug.LogError("[CircularNodeQA] Cleanup failed for owned node: " + ex.Message);
        }
        int remaining = -1;
        bool stillPresent = false;
        try
        {
            remaining = testSolver != null && testSolver.maneuverNodes != null ? testSolver.maneuverNodes.Count : -1;
            stillPresent = hadOwnedNode && ContainsOwnedNode();
        }
        catch (Exception ex)
        {
            failed = true;
            Debug.LogError("[CircularNodeQA] Cleanup verification threw: " + ex.Message);
        }
        cleanupVerified = hadOwnedNode && !stillPresent && remaining == 0;
        cleaned = !stillPresent;
        if (hadOwnedNode && !cleanupVerified)
        {
            failed = true;
            Debug.LogError("[CircularNodeQA] FAIL cleanup verification: ownedStillPresent=" + stillPresent + " remainingNodes=" + remaining);
        }
        Debug.Log("[CircularNodeQA] CLEANUP ownedNodeRemoved=" + removed + " cleanupVerified=" + cleanupVerified + " remainingNodes=" + remaining + " (unrelated nodes are never removed)");
        ownedNode = null;
    }

    void Update()
    {
        if (ran && !cleaned && !cleanupAttempted && !HighLogic.LoadedSceneIsFlight)
        {
            cancelled = true;
            Fail("Scene left during QA; removing only the probe-owned node.");
            CleanupOwnedNode();
            ReportSummary();
        }
    }

    void OnDisable() { if (ran) { cancelled = true; CleanupOwnedNode(); ReportSummary(); } }
    void OnDestroy() { if (ran) { cancelled = true; CleanupOwnedNode(true); ReportSummary(); } }

    void Check(string name, bool pass, string details)
    {
        Debug.Log("[CircularNodeQA] " + (pass ? "PASS " : "FAIL ") + name + " | " + details);
        if (!pass) failed = true;
    }

    bool failed;
    void Fail(string text)
    {
        failed = true;
        Debug.LogError("[CircularNodeQA] FAIL " + text);
    }

    void ReportSummary()
    {
        if (summaryLogged) return;
        summaryLogged = true;
        string result = failed ? "FAIL" : (completed && cleanupVerified ? "PASS" : "INCONCLUSIVE");
        Debug.Log("[CircularNodeQA] SUMMARY " + result +
            " | completed=" + completed + " cleanupVerified=" + cleanupVerified +
            " | detached-vs-stock test only | no burn, no warp command, no save write");
    }
    bool summaryLogged;

    static global::Vector3d Cross(global::Vector3d a, global::Vector3d b)
    {
        return new global::Vector3d(a.y * b.z - a.z * b.y,
            a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
    }
    static double Dot(global::Vector3d a, global::Vector3d b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
    static bool IsFinite(double x) { return !double.IsNaN(x) && !double.IsInfinity(x); }
    static string F(double x) { return IsFinite(x) ? x.ToString("F6", System.Globalization.CultureInfo.InvariantCulture) : x.ToString(); }
}
