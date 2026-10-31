using System;
using MegaCrew;

class RendezvousModelTests
{
    static void Near(double actual, double expected, double tolerance, string name)
    {
        if (double.IsNaN(actual) || double.IsInfinity(actual) || Math.Abs(actual - expected) > tolerance)
            throw new Exception(name + ": expected " + expected + " +/- " + tolerance + ", got " + actual);
    }

    static void Is(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
    }

    static void Throws(Action action, string name)
    {
        try { action(); }
        catch (ArgumentOutOfRangeException) { return; }
        throw new Exception(name + ": expected ArgumentOutOfRangeException");
    }

    static RendezvousEstimate Estimate(double rx, double ry, double rz,
        double vx, double vy, double vz, double horizon)
    {
        return RendezvousModel.Estimate(rx, ry, rz, vx, vy, vz, horizon);
    }

    static void Main()
    {
        var approaching = Estimate(100, 0, 0, -5, 0, 0, 40);
        Near(approaching.RangeMetres, 100, 1e-12, "approaching range");
        Near(approaching.RangeRateMetresPerSecond, -5, 1e-12, "approaching range rate sign");
        Near(approaching.ClosingSpeedMetresPerSecond, 5, 1e-12, "positive closing speed");
        Is(approaching.RangeRateDefined, "noncoincident range rate must be defined");
        Near(approaching.RelativeSpeedMetresPerSecond, 5, 1e-12, "approaching relative speed");
        Near(approaching.ClosestApproachTimeSeconds, 20, 1e-12, "head-on time");
        Near(approaching.ClosestApproachSeparationMetres, 0, 1e-12, "head-on separation");
        Is(approaching.UnconstrainedTimeWithinHorizon, "head-on CPA falls inside horizon");

        var receding = Estimate(100, 0, 0, 5, 0, 0, 40);
        Near(receding.RangeRateMetresPerSecond, 5, 1e-12, "receding range rate sign");
        Near(receding.ClosingSpeedMetresPerSecond, -5, 1e-12, "negative closing speed while receding");
        Near(receding.ClosestApproachTimeSeconds, 0, 0, "receding bounded minimum time");
        Near(receding.ClosestApproachSeparationMetres, 100, 1e-12, "receding bounded minimum separation");
        Is(!receding.UnconstrainedTimeWithinHorizon, "receding global minimum is before now");

        // Independent geometry: r=(60,80), v=(-8,-6); r·v=-960, |v|²=100,
        // so t*=9.6s and the perpendicular miss distance is 28m.
        var crossing = Estimate(60, 80, 0, -8, -6, 0, 20);
        Near(crossing.RangeMetres, 100, 1e-12, "crossing initial range");
        Near(crossing.RangeRateMetresPerSecond, -9.6, 1e-12, "crossing radial rate");
        Near(crossing.RelativeSpeedMetresPerSecond, 10, 1e-12, "crossing speed");
        Near(crossing.ClosestApproachTimeSeconds, 9.6, 1e-12, "crossing CPA time");
        Near(crossing.ClosestApproachSeparationMetres, 28, 1e-12, "crossing miss distance");

        var horizonBounded = Estimate(100, 0, 0, -1, 0, 0, 30);
        Near(horizonBounded.ClosestApproachTimeSeconds, 30, 0, "minimum clamps to horizon");
        Near(horizonBounded.ClosestApproachSeparationMetres, 70, 1e-12, "separation at horizon endpoint");
        Is(!horizonBounded.UnconstrainedTimeWithinHorizon, "unconstrained CPA lies beyond horizon");
        var atHorizon = Estimate(100, 0, 0, -1, 0, 0, 100);
        Near(atHorizon.ClosestApproachTimeSeconds, 100, 0, "CPA exactly at horizon");
        Near(atHorizon.ClosestApproachSeparationMetres, 0, 1e-12, "zero separation at horizon");
        Is(atHorizon.UnconstrainedTimeWithinHorizon, "CPA at the horizon is included");

        // The direct range/speed quotient overflows, but a tiny representable radial
        // component yields a finite five-second closest-approach time.
        var extremeSlow = Estimate(1e308, 0, 0, -5e-324, 1e-8, 0, 10);
        Near(extremeSlow.ClosestApproachTimeSeconds, 4.940656458412465, 1e-12, "scaled time when range/speed quotient overflows");
        Is(extremeSlow.UnconstrainedTimeWithinHorizon, "extreme slow-motion CPA remains inside horizon");

        var stationary = Estimate(3, 4, 0, 0, 0, 0, 60);
        Near(stationary.RangeMetres, 5, 1e-12, "stationary range");
        Near(stationary.RangeRateMetresPerSecond, 0, 0, "stationary range rate");
        Near(stationary.RelativeSpeedMetresPerSecond, 0, 0, "stationary speed");
        Near(stationary.ClosestApproachTimeSeconds, 0, 0, "stationary representative minimum time");
        Near(stationary.ClosestApproachSeparationMetres, 5, 1e-12, "stationary minimum separation");
        Is(stationary.UnconstrainedTimeWithinHorizon, "constant separation is minimized in the horizon");

        var coincidentMoving = RendezvousModel.Estimate(new RendezvousVector(0, 0, 0),
            new RendezvousVector(1, 2, 2), 10);
        Near(coincidentMoving.RangeMetres, 0, 0, "coincident range");
        Is(!coincidentMoving.RangeRateDefined && !coincidentMoving.ClosingSpeedDefined, "coincident radial direction is undefined");
        Near(coincidentMoving.RangeRateMetresPerSecond, 0, 0, "coincident neutral range rate");
        Near(coincidentMoving.ClosingSpeedMetresPerSecond, 0, 0, "coincident neutral closing speed");
        Near(coincidentMoving.RelativeSpeedMetresPerSecond, 3, 1e-12, "coincident relative speed");
        Near(coincidentMoving.ClosestApproachTimeSeconds, 0, 0, "coincident minimum at now");
        Near(coincidentMoving.ClosestApproachSeparationMetres, 0, 0, "coincident minimum separation");

        Throws(() => Estimate(1, 0, 0, 0, 0, 0, 0), "zero horizon");
        Throws(() => Estimate(1, 0, 0, 0, 0, 0, -1), "negative horizon");
        Throws(() => Estimate(1, 0, 0, 0, 0, 0, double.PositiveInfinity), "infinite horizon");
        Throws(() => Estimate(double.NaN, 0, 0, 0, 0, 0, 1), "NaN position");
        Throws(() => Estimate(1, 0, 0, 0, double.NegativeInfinity, 0, 1), "infinite velocity");
        Throws(() => Estimate(1.5e308, 1.5e308, 0, 0, 0, 0, 1), "position norm overflow");

        Console.WriteLine("PASS: approach/recede signs, crossing miss, bounded endpoints, stationary/coincident states, invalid horizons, nonfinite inputs, and overflow rejection.");
    }
}
