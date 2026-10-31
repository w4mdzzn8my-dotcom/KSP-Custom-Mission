using System;
using UnityEngine;

namespace MegaCrew.Navigation
{
    /// <summary>Read-only scalar preview snapshot with live KSP Orbit/body references; revalidation is required.</summary>
    public sealed class CircularLowerPeriapsisProposal
    {
        public Guid VesselId { get; private set; }
        public Orbit StartingOrbit { get; private set; }
        public CelestialBody Body { get; private set; }
        public double BurnUT { get; private set; }
        public double SignedProgradeDeltaV { get; private set; }
        public double TargetPeriapsisAltitude { get; private set; }
        public double PredictedPeriapsisAltitude { get; private set; }
        public double StartingEccentricity { get; private set; }
        public double StartingPeA { get; private set; }
        public double StartingApA { get; private set; }
        public double PositionAtBurnRadius { get; private set; }

        internal CircularLowerPeriapsisProposal(Guid vesselId, Orbit startingOrbit,
            CelestialBody body, double burnUT, double signedProgradeDeltaV,
            double targetPeA, double predictedPeA, double startingEccentricity,
            double startingPeA, double startingApA, double positionAtBurnRadius)
        {
            VesselId = vesselId;
            StartingOrbit = startingOrbit;
            Body = body;
            BurnUT = burnUT;
            SignedProgradeDeltaV = signedProgradeDeltaV;
            TargetPeriapsisAltitude = targetPeA;
            PredictedPeriapsisAltitude = predictedPeA;
            StartingEccentricity = startingEccentricity;
            StartingPeA = startingPeA;
            StartingApA = startingApA;
            PositionAtBurnRadius = positionAtBurnRadius;
        }
    }

    /// <summary>
    /// Preview an impulsive retrograde burn from a near-circular orbit to a lower
    /// periapsis. This does not add a maneuver node, save state, advance time, or
    /// command engines. Inputs read from Orbit are kept in KSP's Y/Z-flipped
    /// orbital coordinates throughout the detached state-vector calculation.
    /// </summary>
    public static class CircularLowerPeriapsisPreview
    {
        public static CircularLowerPeriapsisProposal Create(Vessel vessel,
            double burnUT, double targetPeriapsisAltitude, double maxEccentricity,
            double periapsisToleranceMetres)
        {
            if (vessel == null) throw new ArgumentNullException("vessel");
            RequireFinite(burnUT, "burnUT");
            RequireFinite(targetPeriapsisAltitude, "targetPeriapsisAltitude");
            RequireFinite(maxEccentricity, "maxEccentricity");
            RequireFinite(periapsisToleranceMetres, "periapsisToleranceMetres");

            if (vessel != FlightGlobals.ActiveVessel)
                throw new InvalidOperationException("Only the active vessel can be previewed.");
            if (vessel.patchedConicSolver == null)
                throw new InvalidOperationException("The active vessel has no patched-conic solver.");
            if (vessel.situation != Vessel.Situations.ORBITING)
                throw new InvalidOperationException("Vessel must be in a stable orbiting situation.");
            if (vessel.patchedConicSolver.maneuverNodes == null || vessel.patchedConicSolver.maneuverNodes.Count != 0)
                throw new InvalidOperationException("Preview currently requires no existing maneuver nodes.");

            Orbit startingOrbit = vessel.orbit;
            if (startingOrbit == null || !startingOrbit.activePatch)
                throw new InvalidOperationException("Current orbit is not a valid active patch.");
            CelestialBody body = startingOrbit.referenceBody;
            if (body == null) throw new InvalidOperationException("Current orbit has no reference body.");

            double now = Planetarium.GetUniversalTime();
            if (!IsFinite(now) || burnUT <= now)
                throw new ArgumentOutOfRangeException("burnUT", "Burn UT must be a finite future time.");
            RequireFinite(startingOrbit.StartUT, "startingOrbit.StartUT");
            if (burnUT < startingOrbit.StartUT)
                throw new ArgumentOutOfRangeException("burnUT", "Burn UT must not precede the current orbit patch.");
            // FINAL means there is no following patch. The API documentation does not
            // define a sentinel or bound convention for EndUT in that case, so
            // validate final-patch times through finite future state propagation.
            if (startingOrbit.patchEndTransition != Orbit.PatchTransitionType.FINAL)
            {
                RequireFinite(startingOrbit.EndUT, "startingOrbit.EndUT");
                if (burnUT >= startingOrbit.EndUT)
                    throw new ArgumentOutOfRangeException("burnUT", "Burn UT must precede the end of the current orbit patch.");
            }
            if (maxEccentricity < 0.0 || maxEccentricity >= 1.0)
                throw new ArgumentOutOfRangeException("maxEccentricity", "Circularity tolerance must be in [0,1).");
            if (periapsisToleranceMetres <= 0.0)
                throw new ArgumentOutOfRangeException("periapsisToleranceMetres", "Periapsis tolerance must be positive.");

            RequireFinite(startingOrbit.eccentricity, "startingOrbit.eccentricity");
            RequireFinite(startingOrbit.PeA, "startingOrbit.PeA");
            RequireFinite(startingOrbit.ApA, "startingOrbit.ApA");
            RequireFinite(body.Radius, "body.Radius");
            RequireFinite(body.gravParameter, "body.gravParameter");
            if (body.Radius <= 0.0 || body.gravParameter <= 0.0)
                throw new InvalidOperationException("Reference body has invalid radius or gravitational parameter.");
            if (startingOrbit.eccentricity < 0.0 || startingOrbit.eccentricity > maxEccentricity)
                throw new InvalidOperationException("Starting eccentricity is negative or exceeds the configured near-circular limit.");
            if (targetPeriapsisAltitude <= 0.0)
                throw new ArgumentOutOfRangeException("targetPeriapsisAltitude", "Target periapsis must be above the body's surface.");
            if (targetPeriapsisAltitude >= startingOrbit.PeA)
                throw new ArgumentOutOfRangeException("targetPeriapsisAltitude", "Target periapsis must be below the current periapsis altitude.");

            // Orbit API state vectors are returned in KSP orbital coordinates (Y/Z flipped).
            // UpdateFromStateVectors expects the same coordinate convention; do not mix in
            // Unity world-space or Planetarium-transformed vectors here.
            global::Vector3d position = startingOrbit.getRelativePositionAtUT(burnUT);
            global::Vector3d velocity = startingOrbit.getOrbitalVelocityAtUT(burnUT);
            RequireFinite(position.x, "position.x"); RequireFinite(position.y, "position.y"); RequireFinite(position.z, "position.z");
            RequireFinite(velocity.x, "velocity.x"); RequireFinite(velocity.y, "velocity.y"); RequireFinite(velocity.z, "velocity.z");
            double radius = position.magnitude;
            double speed = velocity.magnitude;
            if (!IsFinite(radius) || radius <= body.Radius || !IsFinite(speed) || speed <= 0.0)
                throw new InvalidOperationException("Future orbit state is invalid or intersects the body.");

            double targetRadius = body.Radius + targetPeriapsisAltitude;
            if (!IsFinite(targetRadius) || targetRadius <= body.Radius || targetRadius >= radius)
                throw new ArgumentOutOfRangeException("targetPeriapsisAltitude", "Target periapsis radius must be above surface and below the burn radius.");

            // Circular-apoapsis vis-viva estimate. On a near-circular orbit the
            // burn point is approximately the new ellipse's apoapsis. Use the actual
            // future speed as the baseline, then let the detached Orbit prediction
            // reject cases whose eccentricity makes the approximation inadequate.
            double targetSpeed = Math.Sqrt(body.gravParameter * (2.0 / radius - 2.0 / (radius + targetRadius)));
            double signedProgradeDv = targetSpeed - speed;
            if (!IsFinite(targetSpeed) || targetSpeed <= 0.0 || !IsFinite(signedProgradeDv) || signedProgradeDv >= 0.0)
                throw new InvalidOperationException("Candidate is not a finite retrograde burn.");

            global::Vector3d afterBurnVelocity = velocity + velocity.normalized * signedProgradeDv;
            if (!IsFinite(afterBurnVelocity.x) || !IsFinite(afterBurnVelocity.y) || !IsFinite(afterBurnVelocity.z))
                throw new InvalidOperationException("Post-burn velocity is nonfinite.");

            Orbit predictedOrbit = new Orbit();
            predictedOrbit.UpdateFromStateVectors(position, afterBurnVelocity, body, burnUT);
            if (predictedOrbit == null || !IsFinite(predictedOrbit.PeA) || !IsFinite(predictedOrbit.eccentricity))
                throw new InvalidOperationException("Detached two-body orbit prediction is invalid.");
            if (predictedOrbit.eccentricity < 0.0 || predictedOrbit.eccentricity >= 1.0)
                throw new InvalidOperationException("Candidate produces an escape or non-elliptic orbit; this preview only supports bound lower-periapsis ellipses.");
            RequireFinite(predictedOrbit.PeR, "predictedOrbit.PeR");
            if (predictedOrbit.PeR <= body.Radius)
                throw new InvalidOperationException("Predicted periapsis intersects the reference body's surface.");
            if (Math.Abs(predictedOrbit.PeA - targetPeriapsisAltitude) > periapsisToleranceMetres)
                throw new InvalidOperationException("Detached prediction misses the requested periapsis tolerance; reject rather than present an unverified target.");

            return new CircularLowerPeriapsisProposal(vessel.id, startingOrbit, body,
                burnUT, signedProgradeDv, targetPeriapsisAltitude, predictedOrbit.PeA,
                startingOrbit.eccentricity, startingOrbit.PeA, startingOrbit.ApA, radius);
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
