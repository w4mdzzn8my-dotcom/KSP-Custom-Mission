using System;
using MegaCrew;

static class DockingGeometryModelTests
{
    static void Is(bool value, string name)
    { if (!value) throw new Exception(name); }

    static void Near(double actual, double expected, double tolerance, string name)
    {
        if (Double.IsNaN(actual) || Double.IsInfinity(actual) || Math.Abs(actual - expected) > tolerance)
            throw new Exception(name + ": expected " + expected + ", got " + actual);
    }

    static void Throws<T>(Action action, string name) where T : Exception
    { try { action(); } catch (T) { return; } throw new Exception(name); }

    static DockingGeometryVector V(double x, double y, double z)
    { return new DockingGeometryVector(x, y, z); }

    static DockingGeometryResult Geometry(DockingGeometryVector ownPosition,
        DockingGeometryVector ownOutward, DockingGeometryVector targetPosition,
        DockingGeometryVector targetOutward)
    {
        return DockingGeometryModel.Measure(new DockingPortObservation(ownPosition, ownOutward),
            new DockingPortObservation(targetPosition, targetOutward));
    }

    static void SameGeometry(DockingGeometryResult a, DockingGeometryResult b, string name)
    {
        Near(b.SeparationMeters, a.SeparationMeters, 1e-12, name + " separation");
        Near(b.AxialDistanceMeters, a.AxialDistanceMeters, 1e-12, name + " axial");
        Near(b.LateralOffsetMeters, a.LateralOffsetMeters, 1e-12, name + " lateral");
        Near(b.OpposingFaceAngleDegrees, a.OpposingFaceAngleDegrees, 1e-12, name + " opposing angle");
    }

    static void Main()
    {
        var facing = Geometry(V(0, 0, 0), V(0, 0, 1), V(3, 4, 10), V(0, 0, -7));
        Near(facing.SeparationMeters, Math.Sqrt(125), 1e-12, "3-4-10 separation");
        Near(facing.AxialDistanceMeters, 10, 1e-12, "positive ahead on own outward axis");
        Near(facing.LateralOffsetMeters, 5, 1e-12, "3-4 lateral offset");
        Near(facing.OpposingFaceAngleDegrees, 0, 1e-12, "opposing outward axes face one another");

        var behind = Geometry(V(0, 0, 0), V(0, 0, 1), V(3, 4, -10), V(0, 0, 1));
        Near(behind.SeparationMeters, Math.Sqrt(125), 1e-12, "behind separation");
        Near(behind.AxialDistanceMeters, -10, 1e-12, "negative axial means behind");
        Near(behind.LateralOffsetMeters, 5, 1e-12, "behind lateral offset");
        Near(behind.OpposingFaceAngleDegrees, 180, 1e-12, "parallel outward axes are 180 degree face error");

        var lateralOnly = Geometry(V(0, 0, 0), V(0, 0, 1), V(3, 4, 0), V(0, 0, -1));
        Near(lateralOnly.SeparationMeters, 5, 1e-12, "pure lateral separation");
        Near(lateralOnly.AxialDistanceMeters, 0, 0, "no axial separation");
        Near(lateralOnly.LateralOffsetMeters, 5, 1e-12, "pure lateral offset");

        var coincident = Geometry(V(4, -2, 9), V(1, 0, 0), V(4, -2, 9), V(0, 1, 0));
        Near(coincident.SeparationMeters, 0, 0, "coincident separation");
        Near(coincident.AxialDistanceMeters, 0, 0, "coincident axial");
        Near(coincident.LateralOffsetMeters, 0, 0, "coincident lateral");
        Near(coincident.OpposingFaceAngleDegrees, 90, 1e-12, "orthogonal port axes");

        var normalized = Geometry(V(0, 0, 0), V(0, 0, 1000), V(3, 4, 10), V(0, 0, -Double.MaxValue));
        SameGeometry(facing, normalized, "finite input directions normalized");
        var basePose = Geometry(V(1, 2, 3), V(0, 0, 1), V(4, 6, 13), V(0, 0, -1));
        var rotated = Geometry(V(3, 2, -1), V(1, 0, 0), V(13, 6, -4), V(-1, 0, 0));
        SameGeometry(basePose, rotated, "common-frame 90 degree rotation");
        var reflected = Geometry(V(-1, 2, 3), V(0, 0, 1), V(-4, 6, 13), V(0, 0, -1));
        SameGeometry(basePose, reflected, "common-frame reflection");

        Throws<ArgumentOutOfRangeException>(() => new DockingPortObservation(V(Double.NaN, 0, 0), V(1, 0, 0)),
            "nonfinite position rejected");
        Throws<ArgumentOutOfRangeException>(() => new DockingPortObservation(V(0, 0, 0), V(Double.PositiveInfinity, 0, 0)),
            "infinite axis rejected");
        Throws<ArgumentOutOfRangeException>(() => new DockingPortObservation(V(0, 0, 0), V(0, 0, 0)),
            "degenerate axis rejected");
        Throws<ArgumentNullException>(() => DockingGeometryModel.Measure(null,
            new DockingPortObservation(V(0, 0, 0), V(1, 0, 0))), "missing own port rejected");
        Throws<ArgumentOutOfRangeException>(() => Geometry(V(Double.MaxValue, 0, 0), V(1, 0, 0),
            V(-Double.MaxValue, 0, 0), V(-1, 0, 0)), "relative position overflow rejected");
        Throws<ArgumentOutOfRangeException>(() => Geometry(V(0, 0, 0), V(1, 1, 1),
            V(Double.MaxValue, Double.MaxValue, Double.MaxValue), V(-1, -1, -1)),
            "nonfinite vector magnitude rejected");

        var approach = DockingClosingModel.Measure(new DockingRangeSample(10, 100),
            new DockingRangeSample(12, 94), 2);
        Near(approach.ElapsedSeconds, 2, 0, "approach sample interval");
        Near(approach.ClosingMetersPerSecond, 3, 0, "positive means measured range closing");
        var recede = DockingClosingModel.Measure(new DockingRangeSample(1, 20),
            new DockingRangeSample(2, 23), 1);
        Near(recede.ClosingMetersPerSecond, -3, 0, "negative means measured range increasing");
        var steady = DockingClosingModel.Measure(new DockingRangeSample(0, 5),
            new DockingRangeSample(0.5, 5), 0.5);
        Near(steady.ClosingMetersPerSecond, 0, 0, "steady measured range");
        Throws<ArgumentOutOfRangeException>(() => DockingClosingModel.Measure(new DockingRangeSample(1, 4),
            new DockingRangeSample(1, 3), 1), "duplicate UT rejected");
        Throws<ArgumentOutOfRangeException>(() => DockingClosingModel.Measure(new DockingRangeSample(2, 4),
            new DockingRangeSample(1, 3), 1), "regressed UT rejected");
        Throws<ArgumentOutOfRangeException>(() => DockingClosingModel.Measure(new DockingRangeSample(1, 4),
            new DockingRangeSample(3.0001, 3), 2), "caller gap bound enforced");
        Throws<ArgumentOutOfRangeException>(() => DockingClosingModel.Measure(new DockingRangeSample(1, 4),
            new DockingRangeSample(2, 3), Double.NaN), "nonfinite caller gap rejected");
        Throws<ArgumentOutOfRangeException>(() => new DockingRangeSample(-1, 4), "negative sample UT rejected");
        Throws<ArgumentOutOfRangeException>(() => new DockingRangeSample(1, -1), "negative range rejected");
        Throws<ArgumentOutOfRangeException>(() => new DockingRangeSample(1, Double.NaN), "NaN range rejected");
        Throws<ArgumentOutOfRangeException>(() => DockingClosingModel.Measure(
            new DockingRangeSample(0, Double.MaxValue), new DockingRangeSample(Double.Epsilon, 0), Double.Epsilon),
            "unrepresentable measured range rate rejected");

        Console.WriteLine("PASS: common-frame separation/axial/lateral/face geometry, normalization, rotation/reflection invariance, invalid/overflow guards, and bounded measured closing.");
    }
}
