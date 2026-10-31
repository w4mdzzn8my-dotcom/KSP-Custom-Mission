using System;

namespace MegaCrew
{
    /// <summary>Finite common-frame position/vector in meters.</summary>
    public struct DockingGeometryVector
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Z;

        public DockingGeometryVector(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        internal bool IsFinite
        {
            get { return Finite(X) && Finite(Y) && Finite(Z); }
        }

        internal static bool Finite(double value)
        {
            return !Double.IsNaN(value) && !Double.IsInfinity(value);
        }

        internal static DockingGeometryVector Normalize(DockingGeometryVector vector, string name)
        {
            if (!vector.IsFinite) throw new ArgumentOutOfRangeException(name, "Direction must be finite.");
            double scale = Math.Max(Math.Abs(vector.X), Math.Max(Math.Abs(vector.Y), Math.Abs(vector.Z)));
            if (scale == 0.0) throw new ArgumentOutOfRangeException(name, "Direction must be nonzero.");
            // Scale first to avoid overflow/underflow in a direct squared norm.
            double x = vector.X / scale;
            double y = vector.Y / scale;
            double z = vector.Z / scale;
            double norm = Math.Sqrt(x * x + y * y + z * z);
            if (!Finite(norm) || norm <= 0.0)
                throw new ArgumentOutOfRangeException(name, "Direction could not be normalized.");
            return new DockingGeometryVector(x / norm, y / norm, z / norm);
        }

        internal static double Magnitude(DockingGeometryVector vector, string name)
        {
            if (!vector.IsFinite) throw new ArgumentOutOfRangeException(name, "Vector must be finite.");
            double scale = Math.Max(Math.Abs(vector.X), Math.Max(Math.Abs(vector.Y), Math.Abs(vector.Z)));
            if (scale == 0.0) return 0.0;
            double x = vector.X / scale;
            double y = vector.Y / scale;
            double z = vector.Z / scale;
            double magnitude = scale * Math.Sqrt(x * x + y * y + z * z);
            if (!Finite(magnitude)) throw new ArgumentOutOfRangeException(name, "Vector magnitude overflowed.");
            return magnitude;
        }
    }

    /// <summary>One port pose in a shared inertial frame. Outward direction is normalized on creation.</summary>
    public sealed class DockingPortObservation
    {
        public DockingGeometryVector Position { get; private set; }
        public DockingGeometryVector OutwardDirection { get; private set; }

        public DockingPortObservation(DockingGeometryVector position, DockingGeometryVector outwardDirection)
        {
            if (!position.IsFinite) throw new ArgumentOutOfRangeException("position", "Position must be finite.");
            Position = position;
            OutwardDirection = DockingGeometryVector.Normalize(outwardDirection, "outwardDirection");
        }
    }

    /// <summary>Geometric measurements only; no readiness, collision, docking, or success inference.</summary>
    public sealed class DockingGeometryResult
    {
        public double SeparationMeters { get; private set; }
        /// <summary>Signed target-minus-own position projected on the own outward axis; positive means ahead.</summary>
        public double AxialDistanceMeters { get; private set; }
        public double LateralOffsetMeters { get; private set; }
        /// <summary>Angle between the own inward-facing axis and target outward axis; zero means axes oppose.</summary>
        public double OpposingFaceAngleDegrees { get; private set; }

        internal DockingGeometryResult(double separation, double axial, double lateral, double angleDegrees)
        {
            SeparationMeters = separation;
            AxialDistanceMeters = axial;
            LateralOffsetMeters = lateral;
            OpposingFaceAngleDegrees = angleDegrees;
        }
    }

    public static class DockingGeometryModel
    {
        /// <summary>Measures two port poses already expressed in the same common frame.</summary>
        public static DockingGeometryResult Measure(DockingPortObservation own, DockingPortObservation target)
        {
            if (own == null) throw new ArgumentNullException("own");
            if (target == null) throw new ArgumentNullException("target");
            DockingGeometryVector displacement = new DockingGeometryVector(
                target.Position.X - own.Position.X,
                target.Position.Y - own.Position.Y,
                target.Position.Z - own.Position.Z);
            if (!displacement.IsFinite)
                throw new ArgumentOutOfRangeException("positions", "Relative position overflowed.");

            double separation = DockingGeometryVector.Magnitude(displacement, "separation");
            double axial = Dot(displacement, own.OutwardDirection);
            if (!DockingGeometryVector.Finite(axial))
                throw new ArgumentOutOfRangeException("positions", "Axial projection overflowed.");
            // Enforce the projection bound against floating-point unit-vector roundoff.
            axial = Math.Max(-separation, Math.Min(separation, axial));
            DockingGeometryVector lateralVector = new DockingGeometryVector(
                displacement.X - own.OutwardDirection.X * axial,
                displacement.Y - own.OutwardDirection.Y * axial,
                displacement.Z - own.OutwardDirection.Z * axial);
            double lateral = DockingGeometryVector.Magnitude(lateralVector, "lateralOffset");

            double outwardDot = Dot(own.OutwardDirection, target.OutwardDirection);
            if (!DockingGeometryVector.Finite(outwardDot))
                throw new ArgumentOutOfRangeException("directions", "Direction comparison is invalid.");
            outwardDot = Math.Max(-1.0, Math.Min(1.0, outwardDot));
            double angle = Math.Acos(-outwardDot) * (180.0 / Math.PI);
            if (!DockingGeometryVector.Finite(angle))
                throw new ArgumentOutOfRangeException("directions", "Opposing-face angle is invalid.");
            return new DockingGeometryResult(separation, axial, lateral, angle);
        }

        static double Dot(DockingGeometryVector left, DockingGeometryVector right)
        {
            return left.X * right.X + left.Y * right.Y + left.Z * right.Z;
        }
    }

    /// <summary>One measured separation and UT sample for range-based closing.</summary>
    public struct DockingRangeSample
    {
        public readonly double UniversalTime;
        public readonly double SeparationMeters;

        public DockingRangeSample(double universalTime, double separationMeters)
        {
            if (!DockingGeometryVector.Finite(universalTime) || universalTime < 0.0)
                throw new ArgumentOutOfRangeException("universalTime");
            if (!DockingGeometryVector.Finite(separationMeters) || separationMeters < 0.0)
                throw new ArgumentOutOfRangeException("separationMeters");
            UniversalTime = universalTime;
            SeparationMeters = separationMeters;
        }
    }

    /// <summary>Observed finite-difference range rate; positive means the measured range is closing.</summary>
    public struct DockingClosingResult
    {
        public readonly double ElapsedSeconds;
        public readonly double ClosingMetersPerSecond;

        internal DockingClosingResult(double elapsedSeconds, double closingMetersPerSecond)
        {
            ElapsedSeconds = elapsedSeconds;
            ClosingMetersPerSecond = closingMetersPerSecond;
        }
    }

    public static class DockingClosingModel
    {
        /// <summary>
        /// Computes (previous range - current range) / elapsed UT. This is a measured
        /// interval average, not an instantaneous velocity or future prediction.
        /// </summary>
        public static DockingClosingResult Measure(DockingRangeSample previous,
            DockingRangeSample current, double maximumGapSeconds)
        {
            if (!DockingGeometryVector.Finite(maximumGapSeconds) || maximumGapSeconds <= 0.0)
                throw new ArgumentOutOfRangeException("maximumGapSeconds");
            double elapsed = current.UniversalTime - previous.UniversalTime;
            if (!DockingGeometryVector.Finite(elapsed) || elapsed <= 0.0)
                throw new ArgumentOutOfRangeException("current", "Sample times must be strictly increasing.");
            if (elapsed > maximumGapSeconds)
                throw new ArgumentOutOfRangeException("current", "Sample interval exceeds the caller's maximum gap.");
            double closing = (previous.SeparationMeters - current.SeparationMeters) / elapsed;
            if (!DockingGeometryVector.Finite(closing))
                throw new ArgumentOutOfRangeException("samples", "Observed closing rate overflowed.");
            return new DockingClosingResult(elapsed, closing);
        }
    }
}
