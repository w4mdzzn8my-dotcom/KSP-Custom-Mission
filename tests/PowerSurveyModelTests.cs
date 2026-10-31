using System;
using System.Collections.Generic;
using MegaCrew;

static class PowerSurveyModelTests
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

    static PowerSurveySession DrainingSession(double duration)
    {
        var session = new PowerSurveySession(duration, 5.0);
        for (int t = 0; t <= 60; t += 2)
        {
            double amount = 100.0 - 0.15 * t;
            PowerSurveySnapshot snapshot = session.Observe(t, amount, 100.0);
            if (t < duration) Is(!snapshot.IsComplete, "drain stays incomplete before requested duration");
        }
        return session;
    }

    static void InvalidFirst(double ut, double amount, double capacity, string name)
    {
        var result = new PowerSurveySession().Observe(ut, amount, capacity);
        Is(result.Status == PowerSurveyStatus.Invalid && !String.IsNullOrWhiteSpace(result.Reason), name);
        Is(!result.HasSamples && !result.IsComplete, name + " has no accepted data");
    }

    static void Main()
    {
        var drain = DrainingSession(60).Snapshot();
        Is(drain.Status == PowerSurveyStatus.Completed && drain.IsComplete, "60-second net-drain survey completes");
        Is(drain.SampleCount == 31 && drain.Samples.Count == 31, "every caller sample retained");
        Near(drain.ObservedElapsedSeconds, 60, 0, "actual observed duration");
        Near(drain.InitialAmount, 100, 0, "drain initial amount");
        Near(drain.FinalAmount, 91, 1e-12, "drain final amount");
        Near(drain.NetChange, -9, 1e-12, "drain net change");
        Near(drain.NetRatePerSecond, -0.15, 1e-12, "drain observed net rate");
        Near(drain.MinimumAmount, 91, 1e-12, "drain minimum");
        Near(drain.MaximumAmount, 100, 1e-12, "drain maximum");
        Near(drain.InitialCapacity, 100, 0, "unchanged capacity recorded");

        var recharge = new PowerSurveySession(6, 5);
        recharge.Observe(0, 20, 100);
        recharge.Observe(2, 22, 100);
        recharge.Observe(4, 23, 100);
        var rechargeResult = recharge.Observe(6, 26, 100);
        Is(rechargeResult.IsComplete, "recharge sample sequence completes");
        Near(rechargeResult.NetChange, 6, 0, "recharge net amount");
        Near(rechargeResult.NetRatePerSecond, 1, 0, "recharge observed net rate");
        Near(rechargeResult.MinimumAmount, 20, 0, "recharge minimum");
        Near(rechargeResult.MaximumAmount, 26, 0, "recharge maximum");

        var steady = new PowerSurveySession(6, 5);
        steady.Observe(10, 55, 100);
        steady.Observe(13, 55, 100);
        var steadyResult = steady.Observe(16, 55, 100);
        Is(steadyResult.IsComplete && steadyResult.HasRate, "steady sample sequence completes with measured rate");
        Near(steadyResult.NetChange, 0, 0, "steady net change");
        Near(steadyResult.NetRatePerSecond, 0, 0, "steady net rate");

        var varying = new PowerSurveySession(6, 5);
        varying.Observe(0, 50, 100);
        varying.Observe(2, 49, 100);
        varying.Observe(4, 48.8, 100);
        var varyingResult = varying.Observe(6, 50, 100);
        Near(varyingResult.NetChange, 0, 1e-12, "varying instantaneous rates may net to zero");
        Near(varyingResult.MinimumAmount, 48.8, 1e-12, "varying minimum uses all samples");
        Near(varyingResult.MaximumAmount, 50, 1e-12, "varying maximum uses all samples");

        // There is no timer-based completion or extrapolation: only supplied samples move elapsed time.
        var noTimer = new PowerSurveySession(6, 5);
        var first = noTimer.Observe(100, 40, 100);
        Is(first.Status == PowerSurveyStatus.Collecting && first.ObservedElapsedSeconds == 0 && first.SampleCount == 1,
            "one sample never completes from elapsed wall-clock time");
        var held = noTimer.Snapshot();
        Is(held.ObservedElapsedSeconds == 0 && held.SampleCount == 1, "snapshot without a new sample makes no progress");

        var boundary = new PowerSurveySession(60, 5);
        boundary.Observe(0, 80, 100);
        for (int t = 5; t <= 55; t += 5) boundary.Observe(t, 80, 100);
        var beforeBoundary = boundary.Observe(59, 80, 100);
        Is(!beforeBoundary.IsComplete && beforeBoundary.ObservedElapsedSeconds == 59,
            "duration below 60 seconds remains incomplete");
        var duplicate = boundary.Observe(59, 80, 100);
        Is(duplicate.SampleCount == beforeBoundary.SampleCount &&
            duplicate.ObservedElapsedSeconds == beforeBoundary.ObservedElapsedSeconds && !duplicate.IsComplete,
            "identical duplicate UT contributes no sample or elapsed progress");
        var atBoundary = boundary.Observe(60, 80, 100);
        Is(atBoundary.IsComplete && atBoundary.ObservedElapsedSeconds == 60,
            "exact 60-second continuous boundary completes");
        var completedHeld = boundary.Observe(62, 10, 100);
        Is(completedHeld.FinalAmount == 80 && completedHeld.SampleCount == atBoundary.SampleCount,
            "completed survey is terminal and cannot absorb later samples");

        var exactGap = new PowerSurveySession(10, 5);
        exactGap.Observe(0, 1, 2);
        Is(exactGap.Observe(5, 1, 2).Status == PowerSurveyStatus.Collecting,
            "gap exactly at limit remains valid");
        Is(exactGap.Observe(10, 1, 2).IsComplete, "continuous exact-gap samples complete at threshold");

        var gap = new PowerSurveySession();
        gap.Observe(0, 1, 2);
        var gapResult = gap.Observe(5.000001, 1, 2);
        Is(gapResult.Status == PowerSurveyStatus.Invalid && !gapResult.IsComplete && gapResult.SampleCount == 1,
            "gap over limit invalidates without accepting the gap sample");

        var regression = new PowerSurveySession();
        regression.Observe(10, 1, 2);
        regression.Observe(12, 1, 2);
        var regressed = regression.Observe(11, 1, 2);
        Is(regressed.Status == PowerSurveyStatus.Invalid && regressed.SampleCount == 2,
            "regressed clock invalidates and preserves accepted prefix");

        var conflictingDuplicate = new PowerSurveySession();
        conflictingDuplicate.Observe(10, 1, 2);
        var conflictResult = conflictingDuplicate.Observe(10, 1.5, 2);
        Is(conflictResult.Status == PowerSurveyStatus.Invalid && conflictResult.SampleCount == 1,
            "same-time conflicting values invalidate");

        var changedCapacity = new PowerSurveySession();
        changedCapacity.Observe(0, 1, 2);
        Is(changedCapacity.Observe(2, 1, 2.1).Status == PowerSurveyStatus.Invalid,
            "capacity change invalidates");
        InvalidFirst(Double.NaN, 1, 2, "NaN UT rejected");
        InvalidFirst(-1, 1, 2, "negative UT rejected");
        InvalidFirst(0, Double.NaN, 2, "NaN amount rejected");
        InvalidFirst(0, Double.PositiveInfinity, 2, "infinite amount rejected");
        InvalidFirst(0, -1, 2, "negative amount rejected");
        InvalidFirst(0, 3, 2, "amount above capacity rejected");
        InvalidFirst(0, 0, 0, "zero capacity rejected");
        InvalidFirst(0, 0, Double.PositiveInfinity, "infinite capacity rejected");
        InvalidFirst(0, 0, Double.NaN, "NaN capacity rejected");
        Throws<ArgumentOutOfRangeException>(() => new PowerSurveySession(0, 5), "zero duration rejected");
        Throws<ArgumentOutOfRangeException>(() => new PowerSurveySession(60.1, 5), "survey longer than bounded window rejected");
        Throws<ArgumentOutOfRangeException>(() => new PowerSurveySession(60, Double.NaN), "NaN gap rejected");
        Throws<ArgumentOutOfRangeException>(() => new PowerSurveySession(60, 5.1), "gap tolerance over five seconds rejected");

        var nonfiniteRate = new PowerSurveySession(60, 5);
        nonfiniteRate.Observe(0, 0, Double.MaxValue);
        Is(nonfiniteRate.Observe(Double.Epsilon, Double.MaxValue, Double.MaxValue).Status == PowerSurveyStatus.Invalid,
            "unrepresentable derived net rate invalidates instead of emitting infinity");

        var cancel = new PowerSurveySession();
        cancel.Observe(20, 12, 40);
        var partial = cancel.Cancel();
        Is(partial.Status == PowerSurveyStatus.Cancelled && !partial.IsComplete && partial.HasSamples &&
            !String.IsNullOrWhiteSpace(partial.Reason), "cancellation is explicit and preserves partial data");
        Is(cancel.Observe(22, 10, 40).SampleCount == 1, "cancelled session is terminal");

        var mutableSession = new PowerSurveySession(10, 5);
        var oldSnapshot = mutableSession.Observe(0, 5, 10);
        mutableSession.Observe(5, 4, 10);
        Is(oldSnapshot.SampleCount == 1 && oldSnapshot.FinalAmount == 5,
            "previous snapshot does not change when session receives later sample");
        Throws<NotSupportedException>(() => ((IList<PowerSurveySample>)mutableSession.Snapshot().Samples)
            .Add(new PowerSurveySample(6, 4, 10)), "sample snapshot collection is read-only");

        Console.WriteLine("PASS: observed drain/recharge/steady/varying readings, exact completion, sample-only progress, duplicate/gap/clock/capacity guards, invalid numbers, terminal cancellation, and immutable snapshots.");
    }
}
