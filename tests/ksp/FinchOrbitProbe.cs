// Test-only one-shot KSP orbital fixture. Never install in a user's normal game.
using System;
using System.Collections;
using System.IO;
using UnityEngine;

static class FinchOrbitContext
{
    public const string SaveFolderName = "MegaCrew Finch Spawn4 QA";
    public static string ExpectedSave = "";
    public static Guid PracticeGuid = Guid.Empty;
    public static Guid SpawnedGuid = Guid.Empty;
    public static CelestialBody ExpectedBody;
    public static bool Attempted;
    public static FinchOrbitCheck ActiveObserver;
}

[KSPAddon(KSPAddon.Startup.MainMenu, false)]
public sealed class FinchOrbitStart : MonoBehaviour
{
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(5);
        string root = KSPUtil.ApplicationRootPath;
        string marker = Path.Combine(root, "FinchSpawn4QA.enable");
        if (!File.Exists(marker)) yield break;
        File.Move(marker, Path.Combine(root, "FinchSpawn4QA.consumed"));
        try
        {
            FinchOrbitContext.ExpectedSave = FinchOrbitContext.SaveFolderName;
            FinchOrbitContext.PracticeGuid = Guid.Empty;
            FinchOrbitContext.SpawnedGuid = Guid.Empty;
            FinchOrbitContext.ExpectedBody = FlightGlobals.GetHomeBody();
            FinchOrbitContext.Attempted = false;
            FinchOrbitContext.ActiveObserver = null;
            var game = GamePersistence.LoadGame("persistent", FinchOrbitContext.SaveFolderName, true, false);
            if (game == null) throw new Exception("Disposable save could not load");
            if (game.flightState == null || game.flightState.protoVessels == null || game.flightState.protoVessels.Count == 0)
                throw new Exception("Disposable save contains no practice ProtoVessel");
            FinchOrbitContext.PracticeGuid = game.flightState.protoVessels[0].vesselID;
            if (FinchOrbitContext.PracticeGuid == Guid.Empty || FinchOrbitContext.ExpectedBody == null)
                throw new Exception("Practice vessel GUID or home body is unavailable");
            Debug.Log("[FinchOrbitQA] Captured practice vessel GUID=" + FinchOrbitContext.PracticeGuid);
            HighLogic.SaveFolder = FinchOrbitContext.SaveFolderName;
            HighLogic.CurrentGame = game;
            Debug.Log("[FinchOrbitQA] Loading unmodified practice vessel from isolated QA save");
            FlightDriver.StartAndFocusVessel(game, 0);
        }
        catch (Exception e) { Debug.LogError("[FinchOrbitQA] Fixture failed: " + e); }
    }
}

[KSPAddon(KSPAddon.Startup.Flight, false)]
public sealed class FinchOrbitCheck : MonoBehaviour
{
    bool observing;

    IEnumerator Start()
    {
        if (HighLogic.SaveFolder != FinchOrbitContext.ExpectedSave || String.IsNullOrEmpty(FinchOrbitContext.ExpectedSave)) yield break;
        if (FinchOrbitContext.SpawnedGuid != Guid.Empty)
        {
            yield return StartCoroutine(StartObservation());
            yield break;
        }
        if (FinchOrbitContext.Attempted)
        {
            Fail("startup", "previous spawn attempt ended before its GUID was recorded; refusing to spawn a duplicate");
            yield break;
        }

        FinchOrbitContext.Attempted = true;
        yield return new WaitForSecondsRealtime(12);
        Vessel active;
        if (!InExpectedPractice("after initial practice-vessel wait", out active)) yield break;
        float practiceDeadline = Time.realtimeSinceStartup + 30;
        while (!PracticeReady(active))
        {
            if (Time.realtimeSinceStartup >= practiceDeadline)
            {
                Fail("practice readiness", "exact saved practice vessel did not become loaded/unpacked on the expected home body");
                yield break;
            }
            yield return new WaitForSecondsRealtime(1);
            if (!InExpectedPractice("after practice readiness wait", out active)) yield break;
        }
        if (!InExpectedPractice("before spawning lander", out active) || !PracticeReady(active)) yield break;
        Debug.Log("[FinchOrbitQA] Practice vessel loaded: " + active.vesselName);

        // Spawn far from the practice vessel, never on top of the original station.
        bool observeAfterSpawn = false;
        try
        {
            var game = HighLogic.CurrentGame;
            string path = Path.Combine(KSPUtil.ApplicationRootPath, "Ships", "VAB", "Hermes Finch Docking Cradle.craft");
            var craft = ShipConstruction.LoadShip(path);
            if (craft == null || craft.parts.Count != 35) throw new Exception("Unexpected craft load");
            uint launch = game.launchID++;
            for (int i = 0; i < craft.parts.Count; i++)
            {
                var p = craft.parts[i];
                p.flightID = 2700000000u + (uint)i;
                p.missionID = 54321;
                p.launchID = launch;
                p.flagURL = "Squad/Flags/default";
                p.temperature = 293;
            }
            var dummyProto = new ProtoVessel(new ConfigNode(), null);
            var dummyObject = new GameObject("FinchOrbitDummy");
            var dummy = dummyObject.AddComponent<Vessel>();
            dummy.parts = craft.parts;
            dummyProto.vesselRef = dummy;
            foreach (var p in craft.parts) dummyProto.protoPartSnapshots.Add(new ProtoPartSnapshot(p, dummyProto));
            foreach (var snapshot in dummyProto.protoPartSnapshots) snapshot.storePartRefs();
            var nodes = new ConfigNode[dummyProto.protoPartSnapshots.Count];
            for (int i = 0; i < nodes.Length; i++)
            {
                nodes[i] = new ConfigNode("PART");
                dummyProto.protoPartSnapshots[i].Save(nodes[i]);
            }
            // ShipConstruct snapshots are editor-state: KSP has not yet initialized
            // the two mated ports. Reproduce the stock launched "PreAttached"
            // state in this test-only fixture, then verify actual flight behavior.
            foreach (var partNode in nodes)
                foreach (var module in partNode.GetNodes("MODULE"))
                    if (module.GetValue("name") == "ModuleDockingNode") module.SetValue("state", "PreAttached", true);
            var orbit = new Orbit(0, 0, 900000, 0, 0, 0, Planetarium.GetUniversalTime(), FinchOrbitContext.ExpectedBody);
            var node = ProtoVessel.CreateVesselNode("Finch Orbit QA", VesselType.Station, orbit, 0, nodes);
            var proto = game.AddVessel(node);
            Destroy(dummyObject);
            if (proto == null || proto.vesselRef == null) throw new Exception("AddVessel returned no vessel");

            FinchOrbitContext.SpawnedGuid = proto.vesselRef.id;
            Debug.Log("[FinchOrbitQA] Spawned disposable vessel guid=" + FinchOrbitContext.SpawnedGuid);
            bool switched = FlightGlobals.SetActiveVessel(proto.vesselRef);
            Debug.Log("[FinchOrbitQA] Switch accepted=" + switched);
            if (!switched) { Fail("spawn selection", "KSP rejected active-vessel switch"); yield break; }
            observeAfterSpawn = true;
        }
        catch (Exception e) { Fail("spawn", e.ToString()); }
        if (observeAfterSpawn) yield return StartCoroutine(StartObservation());
    }

    IEnumerator StartObservation()
    {
        if (observing) yield break;
        if (FinchOrbitContext.ActiveObserver != null && !System.Object.ReferenceEquals(FinchOrbitContext.ActiveObserver, this)) yield break;
        observing = true;
        FinchOrbitContext.ActiveObserver = this;
        try { yield return StartCoroutine(Observe()); }
        finally
        {
            observing = false;
            if (System.Object.ReferenceEquals(FinchOrbitContext.ActiveObserver, this)) FinchOrbitContext.ActiveObserver = null;
        }
    }

    void OnDestroy()
    {
        observing = false;
        if (System.Object.ReferenceEquals(FinchOrbitContext.ActiveObserver, this)) FinchOrbitContext.ActiveObserver = null;
    }

    IEnumerator Observe()
    {
        Vessel vessel;
        yield return new WaitForSecondsRealtime(12);
        if (!InExpectedVessel("after initial observation wait", out vessel)) yield break;

        float deadline = Time.realtimeSinceStartup + 30;
        while (true)
        {
            if (!InExpectedVessel("before load-state check", out vessel)) yield break;
            if (vessel.loaded && !vessel.packed) break;
            if (Time.realtimeSinceStartup >= deadline)
            {
                Fail("load gate", "target remained packed or unloaded after bounded wait");
                yield break;
            }
            if (!InExpectedVessel("before load poll wait", out vessel)) yield break;
            yield return new WaitForSecondsRealtime(1);
            if (!InExpectedVessel("after load poll wait", out vessel)) yield break;
        }

        int liveParts, snapshotParts;
        if (!ReadRequiredPartCounts(vessel, "load gate", out liveParts, out snapshotParts)) yield break;
        Debug.Log("[FinchOrbitQA] LOAD_GATE guid=" + vessel.id + " loaded=" + vessel.loaded + " packed=" + vessel.packed + " liveParts=" + liveParts + " snapshotParts=" + snapshotParts);

        if (!ReadyForObservation("before vessel state", out vessel)) yield break;
        Debug.Log("[FinchOrbitQA] Vessel guid=" + vessel.id + " parts=" + liveParts + " situation=" + vessel.situation + " Pe=" + vessel.orbit.PeA + " Ap=" + vessel.orbit.ApA);

        // Docking output is allowed only after the expected target passed the full
        // loaded/unpacked and 35-live/35-snapshot gate, with identity rechecked now.
        if (!ReadRequiredPartCounts(vessel, "before docking observations", out liveParts, out snapshotParts)) yield break;
        foreach (var part in vessel.parts)
        {
                if (part == null) { Fail("docking observations", "target contains a null part after the 35/35 gate"); yield break; }
            if (!InExpectedVessel("during docking observations", out vessel)) yield break;
            foreach (PartModule module in part.Modules)
            {
                if (module == null || module.moduleName != "ModuleDockingNode") continue;
                if (!ReadyForObservation("before docking module read", out vessel)) yield break;
                var node = module as ModuleDockingNode;
                if (node == null) { Fail("docking observations", "ModuleDockingNode name did not match its runtime type"); yield break; }
                var decouple = module.Events["Decouple"];
                var undock = module.Events["Undock"];
                Debug.Log("[FinchOrbitQA] DOCK vesselGuid=" + vessel.id + " part=" + part.flightID + " state=" + node.state + " decouple=" + (decouple != null && decouple.active) + " undock=" + (undock != null && undock.active));
            }
        }

        if (!ReadyForObservation("before final wait", out vessel)) yield break;
        yield return new WaitForSecondsRealtime(20);
        if (!ReadyForObservation("after final wait before counts", out vessel)) yield break;
        if (!ReadRequiredPartCounts(vessel, "after final wait", out liveParts, out snapshotParts)) yield break;
        Debug.Log("[FinchOrbitQA] AFTER20 guid=" + vessel.id + " parts=" + liveParts + " snapshotParts=" + snapshotParts + " situation=" + vessel.situation);
    }

    bool ReadyForObservation(string stage, out Vessel vessel)
    {
        if (!InExpectedVessel(stage, out vessel)) return false;
        int liveParts, snapshotParts;
        return ReadRequiredPartCounts(vessel, stage, out liveParts, out snapshotParts);
    }

    bool InExpectedPractice(string stage, out Vessel active)
    {
        active = null;
        if (String.IsNullOrEmpty(FinchOrbitContext.ExpectedSave) || HighLogic.SaveFolder != FinchOrbitContext.ExpectedSave)
        {
            Fail(stage, "save changed from expected QA folder");
            return false;
        }
        active = FlightGlobals.ActiveVessel;
        if (active == null)
        {
            Fail(stage, "active vessel is unavailable");
            return false;
        }
        if (FinchOrbitContext.PracticeGuid == Guid.Empty || active.id != FinchOrbitContext.PracticeGuid)
        {
            Fail(stage, "active vessel is not the exact saved practice GUID");
            return false;
        }
        if (active.mainBody != null && active.mainBody != FinchOrbitContext.ExpectedBody)
        {
            Fail(stage, "practice vessel changed away from expected home body");
            return false;
        }
        if (active.orbit != null && active.orbit.referenceBody != null && active.orbit.referenceBody != FinchOrbitContext.ExpectedBody)
        {
            Fail(stage, "practice vessel orbit reference body is not expected home body");
            return false;
        }
        return true;
    }

    bool PracticeReady(Vessel active)
    {
        return active != null && active.loaded && !active.packed && active.mainBody == FinchOrbitContext.ExpectedBody && active.orbit != null && active.orbit.referenceBody == FinchOrbitContext.ExpectedBody;
    }

    bool ReadRequiredPartCounts(Vessel vessel, string stage, out int liveParts, out int snapshotParts)
    {
        liveParts = snapshotParts = -1;
        Vessel current;
        if (!InExpectedVessel(stage + " count guard", out current)) return false;
        if (current.id != vessel.id)
        {
            Fail(stage, "target changed before part counts");
            return false;
        }
        liveParts = current.parts == null ? -1 : current.parts.Count;
        snapshotParts = current.protoVessel == null || current.protoVessel.protoPartSnapshots == null ? -1 : current.protoVessel.protoPartSnapshots.Count;
        if (!current.loaded || current.packed || liveParts != 35 || snapshotParts != 35)
        {
            Fail(stage, "INCONCLUSIVE: expected exact target must be loaded/unpacked with 35 live and 35 snapshot parts; got " + liveParts + "/" + snapshotParts);
            return false;
        }
        return true;
    }

    bool InExpectedVessel(string stage, out Vessel target)
    {
        target = null;
        try
        {
            if (String.IsNullOrEmpty(FinchOrbitContext.ExpectedSave) || HighLogic.SaveFolder != FinchOrbitContext.ExpectedSave)
            {
                Fail(stage, "save changed from expected QA folder");
                return false;
            }
            if (FinchOrbitContext.SpawnedGuid == Guid.Empty || FinchOrbitContext.ExpectedBody == null)
            {
                Fail(stage, "spawned target identity is not established");
                return false;
            }
            Vessel active = FlightGlobals.ActiveVessel;
            if (active == null || active.id != FinchOrbitContext.SpawnedGuid)
            {
                Fail(stage, "active vessel switched away from spawned GUID");
                return false;
            }
            if (FlightGlobals.Vessels == null)
            {
                Fail(stage, "vessel list is unavailable");
                return false;
            }
            foreach (Vessel candidate in FlightGlobals.Vessels)
                if (candidate != null && candidate.id == FinchOrbitContext.SpawnedGuid)
                {
                    target = candidate;
                    break;
                }
            if (target == null || target.id != active.id)
            {
                Fail(stage, "spawned GUID is absent or is not the active vessel");
                return false;
            }
            if (target.mainBody != FinchOrbitContext.ExpectedBody || target.orbit == null || target.orbit.referenceBody != FinchOrbitContext.ExpectedBody)
            {
                Fail(stage, "target body/reference body changed");
                target = null;
                return false;
            }
            return true;
        }
        catch (Exception e)
        {
            Fail(stage, "identity validation failed: " + e.Message);
            target = null;
            return false;
        }
    }

    void Fail(string stage, string reason)
    {
        Debug.LogError("[FinchOrbitQA] INCONCLUSIVE " + stage + ": " + reason + "; no docking result accepted for another vessel");
    }
}
