using System;

namespace MegaCrew.Navigation
{
    /// <summary>
    /// Constant-thrust, constant-specific-impulse, single-engine-set vacuum estimate.
    /// Does not model staging, throttle variation, pressure effects, gravity losses,
    /// engine spool-up, mixtures, or attitude/trajectory changes.
    /// </summary>
    public static class BurnDurationEstimator
    {
        // KSP's standard specific impulse conversion uses standard gravity.
        public const double StandardGravity = 9.80665;

        public sealed class Result
        {
            public double DurationSeconds { get; private set; }
            public double ConsumedPropellantKg { get; private set; }
            public double ResidualPropellantKg { get; private set; }
            public double FinalMassKg { get; private set; }

            internal Result(double durationSeconds, double consumedPropellantKg,
                double residualPropellantKg, double finalMassKg)
            {
                DurationSeconds = durationSeconds;
                ConsumedPropellantKg = consumedPropellantKg;
                ResidualPropellantKg = residualPropellantKg;
                FinalMassKg = finalMassKg;
            }
        }

        public static Result Estimate(double wetMassKg, double usablePropellantKg,
            double thrustNewtons, double specificImpulseSeconds, double requiredDeltaVMetresPerSecond)
        {
            RequireFinite(wetMassKg, "wetMassKg");
            RequireFinite(usablePropellantKg, "usablePropellantKg");
            RequireFinite(thrustNewtons, "thrustNewtons");
            RequireFinite(specificImpulseSeconds, "specificImpulseSeconds");
            RequireFinite(requiredDeltaVMetresPerSecond, "requiredDeltaVMetresPerSecond");

            if (wetMassKg <= 0.0) throw new ArgumentOutOfRangeException("wetMassKg", "Wet mass must be positive.");
            if (usablePropellantKg < 0.0 || usablePropellantKg >= wetMassKg)
                throw new ArgumentOutOfRangeException("usablePropellantKg", "Usable propellant must be nonnegative and less than wet mass, leaving positive dry mass.");
            if (thrustNewtons <= 0.0) throw new ArgumentOutOfRangeException("thrustNewtons", "Thrust must be positive.");
            if (specificImpulseSeconds <= 0.0) throw new ArgumentOutOfRangeException("specificImpulseSeconds", "Specific impulse must be positive.");
            if (requiredDeltaVMetresPerSecond < 0.0)
                throw new ArgumentOutOfRangeException("requiredDeltaVMetresPerSecond", "Required delta-v must be nonnegative.");

            if (requiredDeltaVMetresPerSecond == 0.0)
                return new Result(0.0, 0.0, usablePropellantKg, wetMassKg);

            double exhaustVelocity = specificImpulseSeconds * StandardGravity;
            if (!IsFinite(exhaustVelocity) || exhaustVelocity <= 0.0)
                throw new ArgumentOutOfRangeException("specificImpulseSeconds", "Effective exhaust velocity is outside the finite numeric range.");

            double exponent = requiredDeltaVMetresPerSecond / exhaustVelocity;
            if (!IsFinite(exponent) || exponent <= 0.0)
                throw new ArgumentOutOfRangeException("requiredDeltaVMetresPerSecond", "Required delta-v is outside the finite numeric range.");

            // Rocket equation: mf = m0 * exp(-dv / ve). This stable form also
            // preserves useful precision when dv/ve is very small.
            double consumed = wetMassKg * OneMinusExpNegative(exponent);
            if (!IsFinite(consumed) || consumed <= 0.0)
                throw new ArgumentOutOfRangeException("requiredDeltaVMetresPerSecond", "Calculated propellant use is outside the finite numeric range.");

            // Do not accept and clamp a real propellant shortfall: doing so
            // would report a successful estimate for a delta-v the available
            // propellant cannot produce. The exact boundary is accepted when
            // the computed required mass is equal to the available amount.
            if (consumed > usablePropellantKg)
                throw new InvalidOperationException("Insufficient usable propellant for the requested delta-v.");

            double finalMass = wetMassKg - consumed;
            double residual = usablePropellantKg - consumed;
            double massFlowKgPerSecond = thrustNewtons / exhaustVelocity;
            double duration = consumed / massFlowKgPerSecond;

            if (!IsFinite(finalMass) || finalMass <= 0.0 || !IsFinite(residual) || residual < 0.0 ||
                !IsFinite(massFlowKgPerSecond) || massFlowKgPerSecond <= 0.0 || !IsFinite(duration) || duration <= 0.0)
                throw new ArgumentOutOfRangeException("requiredDeltaVMetresPerSecond", "Calculated burn result is outside the finite numeric range.");

            return new Result(duration, consumed, residual, finalMass);
        }

        static double OneMinusExpNegative(double x)
        {
            if (x < 1e-5)
            {
                // Taylor series for 1 - exp(-x), avoiding catastrophic
                // cancellation for tiny but valid delta-v requests.
                double x2 = x * x;
                return x * (1.0 - x / 2.0 + x2 / 6.0 - x2 * x / 24.0 + x2 * x2 / 120.0);
            }
            return 1.0 - Math.Exp(-x);
        }

        static void RequireFinite(double value, string parameterName)
        {
            if (!IsFinite(value)) throw new ArgumentOutOfRangeException(parameterName, "Value must be finite.");
        }

        static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
