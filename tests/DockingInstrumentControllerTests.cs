using System;
using UnityEngine;
using MegaCrew;

static class ControllerSelectionTests
{
    static void Is(bool value, string message) { if (!value) throw new Exception(message); }
    static bool HasLabel(string prefix)
    {
        foreach (string label in GUILayout.Labels) if (label.StartsWith(prefix, StringComparison.Ordinal)) return true;
        return false;
    }
    static void Draw(OnboardDockingInstruments ui, Vessel own, string save, object game, double now, params int[] selections)
    {
        GUILayout.Labels.Clear();
        GUILayout.Choose(selections);
        ui.Draw(own, save, game, now);
    }
    static ModuleDockingNode Port(Part part, float z, float axisZ)
    {
        var node = new ModuleDockingNode {
            nodeTransformName = "node_stack_top",
            state = "Ready",
            transform = new Transform {
                position = new Vector3(0, 0, z),
                forward = new Vector3(0, 0, axisZ)
            }
        };
        part.Modules.Add(node);
        return node;
    }
    static Vessel MakeVessel(string name, Guid id, CelestialBody body, uint partId, float z, float axisZ)
    {
        var vessel = new Vessel { vesselName = name, id = id, mainBody = body };
        var part = new Part { flightID = partId, name = name + " port", partInfo = new PartInfo { title = name + " docking port" } };
        Port(part, z, axisZ);
        vessel.parts.Add(part);
        return vessel;
    }

    static void Main()
    {
        var body = new CelestialBody { bodyName = "Kerbin" };
        var own = MakeVessel("Own", Guid.NewGuid(), body, 10, 0, 1);
        var targetA = MakeVessel("Target A", Guid.NewGuid(), body, 20, 10, -1);
        var targetB = MakeVessel("Target B", Guid.NewGuid(), body, 30, 40, -1);
        FlightGlobals.ActiveVessel = own;
        FlightGlobals.Vessels = new System.Collections.Generic.List<Vessel> { own, targetA, targetB };
        ((ModuleDockingNode)own.parts[0].Modules[0]).MutationCount = 0;
        ((ModuleDockingNode)targetA.parts[0].Modules[0]).MutationCount = 0;
        ((ModuleDockingNode)targetB.parts[0].Modules[0]).MutationCount = 0;
        FlightGlobals.ready = true;
        FlightDriver.Pause = false;
        TimeWarp.CurrentRateIndex = 0;
        TimeWarp.CurrentRate = 1.0f;
        string save = "Disposable QA";
        object game = new object();
        var ui = new OnboardDockingInstruments();

        // Establish the live own-vessel context before simulating the three actual picker selections.
        ui.Update(own, save, game, 100.0, false, false);
        Draw(ui, own, save, game, 100.0, 0, 0, 0);
        Is(HasLabel("Target: Target A"), "select-own/target/target-port flow keeps chosen target in the controller");
        Is(HasLabel("Waiting for a live port snapshot"), "pickers alone do not fabricate a geometry sample");
        Is(!HasLabel("Separation "), "no separation before actual Update sample");

        ui.Update(own, save, game, 100.5, false, false);
        Draw(ui, own, save, game, 100.5, 0, 0, 0);
        Is(HasLabel("Separation 10.000 m"), "actual controller Update reads the selected own/target ports");
        Is(HasLabel("Opposing-face angle 0.00 deg"), "controller presents actual opposed port axes");
        Is(!HasLabel("Observed closing +"), "first sample does not invent a closing rate");

        // Switch the target via the controller's real selection grid. The new target must
        // survive the structure-signature checks in the same Draw and sample on Update.
        Draw(ui, own, save, game, 100.75, 0, 1, 0);
        Is(HasLabel("Target: Target B"), "target picker selection survives same-frame validation");
        Is(!HasLabel("Separation "), "changing target clears prior geometry immediately");
        ui.Update(own, save, game, 101.0, false, false);
        Draw(ui, own, save, game, 101.0, 0, 1, 0);
        Is(HasLabel("Target: Target B"), "target remains selected after sample update");
        Is(HasLabel("Separation 40.000 m"), "new target gets a fresh target-specific range");
        Is(HasLabel("Observed closing rate unavailable"), "one sample of new target cannot reuse old target closing history");

        // A target-vessel part change invalidates its stored port key and readings.
        targetB.parts[0].flightID = 31;
        ui.Update(own, save, game, 101.5, false, false);
        Draw(ui, own, save, game, 101.5);
        Is(!HasLabel("Separation "), "target part signature change clears displayed geometry");
        Is(HasLabel("Selected docking port changed or disappeared"), "target structure invalidation clears the selected port");

        // Reselect and sample again, then make an own-vessel structural change.
        Draw(ui, own, save, game, 101.5, 0, 1, 0);
        ui.Update(own, save, game, 102.0, false, false);
        Draw(ui, own, save, game, 102.0, 0, 1, 0);
        Is(HasLabel("Separation 40.000 m"), "readings resume after target reselection and fresh sample: " + String.Join(" | ", GUILayout.Labels.ToArray()));
        own.parts[0].flightID = 11;
        ui.Update(own, save, game, 102.5, false, false);
        Draw(ui, own, save, game, 102.5);
        Is(!HasLabel("Separation "), "own part signature change clears displayed geometry");
        Is(HasLabel("Active vessel parts changed"), "own structure invalidation is visible");

        // Re-select and take another real sample; pause must clear the result.
        Draw(ui, own, save, game, 102.5, 0, 1, 0);
        ui.Update(own, save, game, 103.0, false, false);
        Draw(ui, own, save, game, 103.0, 0, 1, 0);
        Is(HasLabel("Separation 40.000 m"), "readings resume only after reselecting and sampling");
        FlightDriver.Pause = true;
        ui.Update(own, save, game, 103.25, true, false);
        Draw(ui, own, save, game, 103.25);
        Is(!HasLabel("Separation "), "pause clears displayed geometry");
        Is(HasLabel("Paused; resume before selecting ports"), "paused picker state is visible");
        FlightDriver.Pause = false;

        Is(own.parts[0].Modules.Count == 1 && targetA.parts[0].Modules.Count == 1 && targetB.parts[0].Modules.Count == 1,
            "controller did not add or remove docking modules");
        Is(((ModuleDockingNode)own.parts[0].Modules[0]).MutationCount == 0 &&
            ((ModuleDockingNode)targetA.parts[0].Modules[0]).MutationCount == 0 &&
            ((ModuleDockingNode)targetB.parts[0].Modules[0]).MutationCount == 0,
            "controller did not mutate docking node state or invoke actuation");
        Console.WriteLine("PASS: actual controller picker flow, target persistence/fresh range, part-change invalidation, pause invalidation, and no docking-module mutation.");
    }
}
