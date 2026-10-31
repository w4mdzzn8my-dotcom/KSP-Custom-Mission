using System;

namespace MegaCrew
{
    /// <summary>Three-component vector dedicated to the pure rendezvous model; all components share one inertial frame.</summary>
    public struct RendezvousVector
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Z;

        public RendezvousVector(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    /// <summary>
    /// Read-only linear relative-motion estimate. Relative position is target minus
    /// active vessel in one common inertial frame; SI units are metres and m/s.
    /// </summary>
    public sealed class RendezvousEstimate
    {
        public double RangeMetres { get; private set; }
        /// <summary>Positive means range is increasing (receding); negative means closing.</summary>
        public double RangeRateMetresPerSecond { get; private set; }
        /// <summary>Negative range rate; positive means closing/approaching.</summary>
        public double ClosingSpeedMetresPerSecond { get; private set; }
        /// <summary>False only when range is zero, where the radial direction is undefined.</summary>
        public bool RangeRateDefined { get; private set; }
        /// <summary>Same validity as radial range rate; false when coincident.</summary>
        public bool ClosingSpeedDefined { get { return RangeRateDefined; } }
        public double RelativeSpeedMetresPerSecond { get; private set; }
        /// <summary>
        /// Time in [0,horizon] at which straight-line separation is minimal on that
        /// interval. This is not an orbital closest-approach time.
        /// </summary>
        public double ClosestApproachTimeSeconds { get; private set; }
        /// <summary>Minimum straight-line separation on [0,horizon], including endpoints.</summary>
        public double ClosestApproachSeparationMetres { get; private set; }
        /// <summary>
        /// True when the unconstrained linear minimum lies within [0,horizon]. False
        /// when the bounded minimum is at an endpoint because unconstrained CPA is
        /// before now or after the caller's horizon.
        /// </summary>
        public bool UnconstrainedTimeWithinHorizon { get; private set; }

        internal RendezvousEstimate(double rangeMetres, double rangeRateMetresPerSecond,
            bool rangeRateDefined, double relativeSpeedMetresPerSecond,
            double closestApproachTimeSeconds, double closestApproachSeparationMetres,
            bool unconstrainedTimeWithinHorizon)
        {
            RangeMetres = rangeMetres;
            RangeRateMetresPerSecond = rangeRateMetresPerSecond;
            ClosingSpeedMetresPerSecond = -rangeRateMetresPerSecond;
            RangeRateDefined = rangeRateDefined;
            RelativeSpeedMetresPerSecond = relativeSpeedMetresPerSecond;
            ClosestApproachTimeSeconds = closestApproachTimeSeconds;
            ClosestApproachSeparationMetres = closestApproachSeparationMetres;
            UnconstrainedTimeWithinHorizon = unconstrainedTimeWithinHorizon;
        }
    }

    /// <summary>
    /// Computes range, radial range rate, relative speed and the bounded minimum
    /// separation for the local linear model r(t) = r0 + v*t. This is an instrument
    /// for short-horizon relative-motion awareness, not an orbital rendezvous,
    /// collision prediction, or maneuver solution.
    /// </summary>
    public static class RendezvousModel
    {
        /// <param name="relativePositionXMetres">Target position minus vessel position, x component.</param>
        /// <param name="relativePositionYMetres">Target position minus vessel position, y component.</param>
        /// <param name="relativePositionZMetres">Target position minus vessel position, z component.</param>
        /// <param name="relativeVelocityXMetresPerSecond">Target velocity minus vessel velocity, x component.</param>
        /// <param name="relativeVelocityYMetresPerSecond">Target velocity minus vessel velocity, y component.</param>
        /// <param name="relativeVelocityZMetresPerSecond">Target velocity minus vessel velocity, z component.</param>
        /// <param name="horizonSeconds">Positive finite caller-selected horizon.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Any input is nonfinite or invalid, or a required magnitude/prediction
        /// overflows the finite numeric range.
        /// </exception>
        public static RendezvousEstimate Estimate(RendezvousVector relativePositionMetres,
            RendezvousVector relativeVelocityMetresPerSecond, double horizonSeconds)
        {
            return Estimate(relativePositionMetres.X, relativePositionMetres.Y, relativePositionMetres.Z,
                relativeVelocityMetresPerSecond.X, relativeVelocityMetresPerSecond.Y,
                relativeVelocityMetresPerSecond.Z, horizonSeconds);
        }

        public static RendezvousEstimate Estimate(double relativePositionXMetres,
            double relativePositionYMetres, double relativePositionZMetres,
            double relativeVelocityXMetresPerSecond, double relativeVelocityYMetresPerSecond,
            double relativeVelocityZMetresPerSecond, double horizonSeconds)
        {
            RequireFinite(relativePositionXMetres, "relativePositionXMetres");
            RequireFinite(relativePositionYMetres, "relativePositionYMetres");
            RequireFinite(relativePositionZMetres, "relativePositionZMetres");
            RequireFinite(relativeVelocityXMetresPerSecond, "relativeVelocityXMetresPerSecond");
            RequireFinite(relativeVelocityYMetresPerSecond, "relativeVelocityYMetresPerSecond");
            RequireFinite(relativeVelocityZMetresPerSecond, "relativeVelocityZMetresPerSecond");
            RequireFinite(horizonSeconds, "horizonSeconds");
            if (horizonSeconds <= 0.0)
                throw new ArgumentOutOfRangeException("horizonSeconds", "Horizon must be positive.");

            double range = Norm3(relativePositionXMetres, relativePositionYMetres, relativePositionZMetres,
                "relativePosition");
            double speed = Norm3(relativeVelocityXMetresPerSecond, relativeVelocityYMetresPerSecond,
                relativeVelocityZMetresPerSecond, "relativeVelocity");

            bool rangeRateDefined = range > 0.0;
            double rangeRate = 0.0;
            if (rangeRateDefined && speed > 0.0)
            {
                double alignment = UnitDot(relativePositionXMetres, relativePositionYMetres, relativePositionZMetres,
                    range, relativeVelocityXMetresPerSecond, relativeVelocityYMetresPerSecond,
                    relativeVelocityZMetresPerSecond, speed);
                rangeRate = speed * alignment;
                if (!IsFinite(rangeRate))
                    throw new ArgumentOutOfRangeException("relativeVelocity", "Range rate overflowed.");
            }

            double closestTime;
            bool unconstrainedWithin;
            if (speed == 0.0 || range == 0.0)
            {
                // Stationary range is constant; coincident motion has t*=0 at the origin.
                // In either case choose t=0 as a bounded minimizer.
                closestTime = 0.0;
                unconstrainedWithin = true;
            }
            else
            {
                double alignment = UnitDot(relativePositionXMetres, relativePositionYMetres,
                    relativePositionZMetres, range, relativeVelocityXMetresPerSecond,
                    relativeVelocityYMetresPerSecond, relativeVelocityZMetresPerSecond, speed);
                double unconstrainedTime;
                if (alignment == 0.0)
                    unconstrainedTime = 0.0;
                else
                {
                    // The unit-vector form avoids squaring large/small velocities. An
                    // infinite quotient is still safely clamped to the finite horizon.
                    double timeScale = range / speed;
                    double timeMagnitude;
                    if (double.IsPositiveInfinity(timeScale))
                    {
                        // Preserve a representable answer when the quotient overflows
                        // but the direction cosine is tiny (large range, very slow motion).
                        timeMagnitude = (range * Math.Abs(alignment)) / speed;
                    }
                    else
                        timeMagnitude = timeScale * Math.Abs(alignment);
                    unconstrainedTime = alignment > 0.0 ? -timeMagnitude : timeMagnitude;
                }

                unconstrainedWithin = IsFinite(unconstrainedTime) &&
                    unconstrainedTime >= 0.0 && unconstrainedTime <= horizonSeconds;
                if (unconstrainedTime < 0.0) closestTime = 0.0;
                else if (unconstrainedTime > horizonSeconds || double.IsPositiveInfinity(unconstrainedTime))
                    closestTime = horizonSeconds;
                else if (double.IsNaN(unconstrainedTime))
                    throw new ArgumentOutOfRangeException("relativeMotion", "Closest-approach time is indeterminate.");
                else
                    closestTime = unconstrainedTime;
            }

            double closestX = relativePositionXMetres + relativeVelocityXMetresPerSecond * closestTime;
            double closestY = relativePositionYMetres + relativeVelocityYMetresPerSecond * closestTime;
            double closestZ = relativePositionZMetres + relativeVelocityZMetresPerSecond * closestTime;
            if (!IsFinite(closestX) || !IsFinite(closestY) || !IsFinite(closestZ))
                throw new ArgumentOutOfRangeException("horizonSeconds", "Linear relative position overflowed within the requested horizon.");
            double closestRange = Norm3(closestX, closestY, closestZ, "closestApproach");

            return new RendezvousEstimate(range, rangeRate, rangeRateDefined, speed,
                closestTime, closestRange, unconstrainedWithin);
        }

        static double UnitDot(double ax, double ay, double az, double aNorm,
            double bx, double by, double bz, double bNorm)
        {
            double dot = (ax / aNorm) * (bx / bNorm) +
                (ay / aNorm) * (by / bNorm) +
                (az / aNorm) * (bz / bNorm);
            if (!IsFinite(dot)) throw new ArgumentOutOfRangeException("relativeMotion", "Direction calculation overflowed.");
            return Math.Max(-1.0, Math.Min(1.0, dot));
        }

        static double Norm3(double x, double y, double z, string name)
        {
            double scale = Math.Max(Math.Abs(x), Math.Max(Math.Abs(y), Math.Abs(z)));
            if (scale == 0.0) return 0.0;
            double sx = x / scale;
            double sy = y / scale;
            double sz = z / scale;
            double norm = scale * Math.Sqrt(sx * sx + sy * sy + sz * sz);
            if (!IsFinite(norm))
                throw new ArgumentOutOfRangeException(name, "Vector magnitude exceeds the finite numeric range.");
            return norm;
        }

        static void RequireFinite(double value, string name)
        {
            if (!IsFinite(value)) throw new ArgumentOutOfRangeException(name, "Value must be finite.");
        }

        static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
