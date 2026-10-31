using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MegaCrew
{
    public enum PowerSurveyStatus
    {
        AwaitingFirstSample,
        Collecting,
        Completed,
        Cancelled,
        Invalid
    }

    /// <summary>One immutable observation of actual UT, EC amount, and EC capacity.</summary>
    public struct PowerSurveySample
    {
        public readonly double UniversalTime;
        public readonly double Amount;
        public readonly double Capacity;

        public PowerSurveySample(double universalTime, double amount, double capacity)
        {
            UniversalTime = universalTime;
            Amount = amount;
            Capacity = capacity;
        }
    }

    /// <summary>A copied immutable progress or final snapshot of a power survey.</summary>
    public sealed class PowerSurveySnapshot
    {
        readonly ReadOnlyCollection<PowerSurveySample> samples;
        public PowerSurveyStatus Status { get; private set; }
        public string Reason { get; private set; }
        public bool IsComplete { get { return Status == PowerSurveyStatus.Completed; } }
        public bool HasSamples { get { return samples.Count > 0; } }
        public int SampleCount { get { return samples.Count; } }
        public double ObservedElapsedSeconds { get; private set; }
        public double InitialAmount { get; private set; }
        public double FinalAmount { get; private set; }
        public double InitialCapacity { get; private set; }
        public double NetChange { get; private set; }
        public bool HasRate { get; private set; }
        /// <summary>Observed net EC/s over the sample interval, not a forecast.</summary>
        public double NetRatePerSecond { get; private set; }
        public double MinimumAmount { get; private set; }
        public double MaximumAmount { get; private set; }
        public ReadOnlyCollection<PowerSurveySample> Samples { get { return samples; } }

        internal PowerSurveySnapshot(PowerSurveyStatus status, string reason,
            double elapsed, double initial, double final, double capacity,
            double net, double minimum, double maximum, List<PowerSurveySample> source)
        {
            Status = status;
            Reason = reason ?? String.Empty;
            ObservedElapsedSeconds = elapsed;
            InitialAmount = initial;
            FinalAmount = final;
            InitialCapacity = capacity;
            NetChange = net;
            HasRate = elapsed > 0.0 && source.Count > 1;
            NetRatePerSecond = HasRate ? net / elapsed : 0.0;
            MinimumAmount = minimum;
            MaximumAmount = maximum;
            samples = new List<PowerSurveySample>(source).AsReadOnly();
        }
    }

    /// <summary>
    /// Accumulates actual resource observations only. It has no timer, KSP access,
    /// consumption, forecast, or reward behavior. Each accepted interval must be
    /// continuously covered by caller-supplied samples.
    /// </summary>
    public sealed class PowerSurveySession
    {
        const int MaximumSamples = 1024;
        const double MaximumSurveyDurationSeconds = 60.0;
        const double MaximumAllowedGapSeconds = 5.0;
        readonly double requiredDurationSeconds;
        readonly double maximumGapSeconds;
        readonly List<PowerSurveySample> samples = new List<PowerSurveySample>();
        PowerSurveyStatus status = PowerSurveyStatus.AwaitingFirstSample;
        string reason = String.Empty;
        double initialAmount, finalAmount, initialCapacity, minimumAmount, maximumAmount;
        double observedElapsedSeconds;

        public PowerSurveySession(double requiredDurationSeconds = 60.0, double maximumGapSeconds = 5.0)
        {
            if (!Finite(requiredDurationSeconds) || requiredDurationSeconds <= 0.0 ||
                requiredDurationSeconds > MaximumSurveyDurationSeconds)
                throw new ArgumentOutOfRangeException("requiredDurationSeconds");
            if (!Finite(maximumGapSeconds) || maximumGapSeconds <= 0.0 ||
                maximumGapSeconds > MaximumAllowedGapSeconds)
                throw new ArgumentOutOfRangeException("maximumGapSeconds");
            this.requiredDurationSeconds = requiredDurationSeconds;
            this.maximumGapSeconds = maximumGapSeconds;
        }

        /// <summary>Adds one actual sample. Invalid data terminates the session fail-closed.</summary>
        public PowerSurveySnapshot Observe(double universalTime, double amount, double capacity)
        {
            if (IsTerminal()) return Snapshot();
            string invalid = ValidateSample(universalTime, amount, capacity);
            if (invalid != null) return Invalidate(invalid);

            var sample = new PowerSurveySample(universalTime, amount, capacity);
            if (samples.Count == 0)
            {
                double finish = universalTime + requiredDurationSeconds;
                if (!Finite(finish) || finish <= universalTime)
                    return Invalidate("Universal-time range cannot represent the requested survey duration.");
                samples.Add(sample);
                initialAmount = finalAmount = minimumAmount = maximumAmount = amount;
                initialCapacity = capacity;
                status = PowerSurveyStatus.Collecting;
                return Snapshot();
            }

            PowerSurveySample previous = samples[samples.Count - 1];
            if (universalTime == previous.UniversalTime)
            {
                if (amount == previous.Amount && capacity == previous.Capacity)
                    return Snapshot(); // A duplicate timestamp contributes no coverage or sample.
                return Invalidate("Conflicting resource readings share the same universal time.");
            }
            if (universalTime < previous.UniversalTime)
                return Invalidate("Universal time regressed during the survey.");
            if (universalTime - previous.UniversalTime > maximumGapSeconds)
                return Invalidate("Sample gap exceeded the continuous-coverage limit.");
            if (capacity != initialCapacity)
                return Invalidate("ElectricCharge capacity changed during the survey.");
            if (samples.Count >= MaximumSamples)
                return Invalidate("Survey sample limit was exceeded.");

            double newElapsed = universalTime - samples[0].UniversalTime;
            double newNetChange = amount - initialAmount;
            if (!Finite(newElapsed) || newElapsed <= 0.0 || !Finite(newNetChange / newElapsed))
                return Invalidate("Observed net rate cannot be represented as a finite value.");

            samples.Add(sample);
            finalAmount = amount;
            minimumAmount = Math.Min(minimumAmount, amount);
            maximumAmount = Math.Max(maximumAmount, amount);
            observedElapsedSeconds = newElapsed;
            status = observedElapsedSeconds >= requiredDurationSeconds
                ? PowerSurveyStatus.Completed : PowerSurveyStatus.Collecting;
            return Snapshot();
        }

        /// <summary>Ends a partial survey explicitly while preserving its observations.</summary>
        public PowerSurveySnapshot Cancel()
        {
            if (!IsTerminal())
            {
                status = PowerSurveyStatus.Cancelled;
                reason = "Survey cancelled; displayed values are partial observations.";
            }
            return Snapshot();
        }

        /// <summary>Returns a defensive immutable copy of current progress.</summary>
        public PowerSurveySnapshot Snapshot()
        {
            double net = samples.Count > 0 ? finalAmount - initialAmount : 0.0;
            return new PowerSurveySnapshot(status, reason, observedElapsedSeconds,
                initialAmount, finalAmount, initialCapacity, net, minimumAmount,
                maximumAmount, samples);
        }

        bool IsTerminal()
        {
            return status == PowerSurveyStatus.Completed || status == PowerSurveyStatus.Cancelled ||
                status == PowerSurveyStatus.Invalid;
        }

        PowerSurveySnapshot Invalidate(string why)
        {
            status = PowerSurveyStatus.Invalid;
            reason = why;
            return Snapshot();
        }

        static string ValidateSample(double ut, double amount, double capacity)
        {
            if (!Finite(ut) || ut < 0.0) return "Universal time must be finite and nonnegative.";
            if (!Finite(amount) || amount < 0.0) return "ElectricCharge amount must be finite and nonnegative.";
            if (!Finite(capacity) || capacity <= 0.0) return "ElectricCharge capacity must be finite and positive.";
            if (amount > capacity) return "ElectricCharge amount exceeds capacity.";
            return null;
        }

        static bool Finite(double value)
        {
            return !Double.IsNaN(value) && !Double.IsInfinity(value);
        }
    }
}
