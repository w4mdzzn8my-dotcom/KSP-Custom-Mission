using System;

namespace MegaCrew.Navigation
{
    /// <summary>Pure two-body estimates. This class does not create nodes or run the KSP clock.</summary>
    public static class MissionControlMath
    {
        public const double LightSpeedMetresPerSecond = 299792458.0;
        public static double RoundTripSeconds(double outboundMetres, double inboundMetres)
        {
            if (!Finite(outboundMetres) || !Finite(inboundMetres) || outboundMetres < 0 || inboundMetres < 0)
                throw new ArgumentOutOfRangeException("Distances must be finite and nonnegative");
            double seconds = outboundMetres / LightSpeedMetresPerSecond + inboundMetres / LightSpeedMetresPerSecond;
            if (!Finite(seconds)) throw new ArgumentOutOfRangeException("Path delay overflow");
            return seconds;
        }
        static bool Finite(double v) { return !double.IsNaN(v) && !double.IsInfinity(v); }
        /// <summary>
        /// An impulsive retrograde burn at a circular-orbit apoapsis reaching desired periapsis.
        /// Inputs are metres and m^3/s^2; returns a signed prograde delta-v in m/s.
        /// This intentionally rejects noncircular or inside-body cases instead of inventing an answer.
        /// </summary>
        public static double CircularApoapsisBurn(double mu, double bodyRadius, double orbitRadius, double targetAltitude)
        {
            if (!Finite(mu) || !Finite(bodyRadius) || !Finite(orbitRadius) || !Finite(targetAltitude) ||
                mu <= 0 || bodyRadius <= 0 || orbitRadius <= bodyRadius || targetAltitude < 0)
                throw new ArgumentOutOfRangeException("Invalid orbit or body");
            double rp = bodyRadius + targetAltitude;
            if (!Finite(rp)) throw new ArgumentOutOfRangeException("Target radius overflow");
            if (rp >= orbitRadius) throw new ArgumentOutOfRangeException("Target must be below the current orbit");
            double current = Math.Sqrt(mu / orbitRadius);
            double transfer = Math.Sqrt(mu * (2 / orbitRadius - 2 / (orbitRadius + rp)));
            return transfer - current;
        }
    }
}
