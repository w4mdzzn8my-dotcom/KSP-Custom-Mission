using System;
using MegaCrew.Navigation;

internal static class Program
{
    static int assertions;

    static void Assert(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception("FAIL: " + message);
    }

    static void Near(double actual, double expected, double tolerance, string message)
    {
        Assert(Math.Abs(actual - expected) <= tolerance, message + "; expected " + expected.ToString("R") + ", got " + actual.ToString("R"));
    }

    static void Reject(Action action, string message)
    {
        assertions++;
        try { action(); }
        catch (ArgumentOutOfRangeException) { return; }
        catch (InvalidOperationException) { return; }
        throw new Exception("FAIL: expected rejection: " + message);
    }

    // Independent reference equations: final mass from Tsiolkovsky, then duration
    // from propellant mass / constant mass flow (not estimator implementation).
    static void CheckAgainstIndependentEquations(double wetMass, double thrust, double isp, double dv, string label)
    {
        var result = BurnDurationEstimator.Estimate(wetMass, wetMass * 0.999999, thrust, isp, dv);
        double ve = isp * BurnDurationEstimator.StandardGravity;
        double referenceFinalMass = wetMass / Math.Exp(dv / ve);
        double referenceConsumed = wetMass - referenceFinalMass;
        double referenceMassFlow = thrust / ve;
        double referenceDuration = referenceConsumed / referenceMassFlow;
        Near(result.FinalMassKg, referenceFinalMass, Math.Max(1e-12, wetMass * 2e-12), label + " final mass");
        Near(result.ConsumedPropellantKg, referenceConsumed, Math.Max(1e-12, wetMass * 2e-12), label + " consumed mass");
        Near(result.DurationSeconds, referenceDuration, Math.Max(1e-12, referenceDuration * 2e-9), label + " duration");
        Near(result.ResidualPropellantKg, wetMass * 0.999999 - referenceConsumed,
            Math.Max(1e-12, wetMass * 2e-12), label + " residual");
    }

    static void Main()
    {
        Reject(()=>BurnDurationEstimator.Estimate(1,0,1,1,1e-14), "tiny burn cannot use empty propellant");

        // Zero delta-v is valid and consumes no propellant.
        var zero = BurnDurationEstimator.Estimate(1000, 100, 20000, 300, 0);
        Near(zero.DurationSeconds, 0, 0, "zero dv duration");
        Near(zero.ConsumedPropellantKg, 0, 0, "zero dv consumed");
        Near(zero.ResidualPropellantKg, 100, 0, "zero dv residual");
        Near(zero.FinalMassKg, 1000, 0, "zero dv final mass");

        // Typical mono-engine case and broad numeric-scale cases.
        CheckAgainstIndependentEquations(10000, 200000, 320, 2500, "typical");
        CheckAgainstIndependentEquations(50, 10, 900, 0.01, "small delta-v");
        CheckAgainstIndependentEquations(1e6, 3e7, 450, 12000, "large scale");

        // Exact propellant-boundary case passes; slightly more dv must fail.
        double wet = 5000, prop = 1000, thrust = 50000, isp = 280;
        double ve = isp * BurnDurationEstimator.StandardGravity;
        double dvAtBoundary = ve * Math.Log(wet / (wet - prop));
        var boundary = BurnDurationEstimator.Estimate(wet, prop, thrust, isp, dvAtBoundary);
        Near(boundary.ConsumedPropellantKg, prop, 1e-9, "exact propellant boundary consumed");
        Near(boundary.ResidualPropellantKg, 0, 1e-9, "exact propellant boundary residual");
        Reject(() => BurnDurationEstimator.Estimate(wet, prop, thrust, isp, dvAtBoundary * (1 + 1e-8)), "insufficient propellant");
        Reject(() => BurnDurationEstimator.Estimate(wet, prop, thrust, isp, dvAtBoundary * (1 + 1e-13)),
            "sub-tolerance but physically insufficient propellant");

        Reject(() => BurnDurationEstimator.Estimate(0, 1, 1, 1, 1), "zero wet mass");
        Reject(() => BurnDurationEstimator.Estimate(100, -1, 1, 1, 1), "negative propellant");
        Reject(() => BurnDurationEstimator.Estimate(100, 100, 1, 1, 1), "zero dry mass");
        Reject(() => BurnDurationEstimator.Estimate(100, 1, 0, 1, 1), "zero thrust");
        Reject(() => BurnDurationEstimator.Estimate(100, 1, 1, 0, 1), "zero isp");
        Reject(() => BurnDurationEstimator.Estimate(100, 1, 1, 1, -1), "negative delta-v");
        Reject(() => BurnDurationEstimator.Estimate(double.NaN, 1, 1, 1, 1), "NaN wet mass");
        Reject(() => BurnDurationEstimator.Estimate(100, double.PositiveInfinity, 1, 1, 1), "infinite propellant");
        Reject(() => BurnDurationEstimator.Estimate(100, 1, double.NegativeInfinity, 1, 1), "infinite thrust");
        Reject(() => BurnDurationEstimator.Estimate(100, 1, 1, double.NaN, 1), "NaN Isp");
        Reject(() => BurnDurationEstimator.Estimate(100, 1, 1, 1, double.PositiveInfinity), "infinite delta-v");
        Reject(() => BurnDurationEstimator.Estimate(100, 1, double.Epsilon, 1, 1), "underflowed mass flow / duration bound");

        // Smallest positive representable requested delta-v should remain finite
        // when all physical inputs are ordinary and a tiny consumption is valid.
        var tiny = BurnDurationEstimator.Estimate(100, 1, 1000, 300, 1e-12);
        Reject(() => BurnDurationEstimator.Estimate(100, 1, 1000, 300, double.Epsilon), "delta-v below representable exhaust-velocity ratio");
        Assert(!double.IsNaN(tiny.DurationSeconds) && !double.IsInfinity(tiny.DurationSeconds), "tiny dv duration finite");
        Assert(!double.IsNaN(tiny.ConsumedPropellantKg) && !double.IsInfinity(tiny.ConsumedPropellantKg), "tiny dv mass finite");

        // Finite inputs can still underflow required propellant to zero even
        // while exponent and mass flow remain positive. Never report this as
        // a successful zero-duration positive-delta-v burn.
        Reject(() => BurnDurationEstimator.Estimate(1e-300, 1e-301, 1e-300, 1e12, 1e-100),
            "positive dv whose propellant consumption underflows to zero");

        Console.WriteLine("PASS: " + assertions + " assertions; constant-thrust single-engine-set estimator only.");
    }
}
