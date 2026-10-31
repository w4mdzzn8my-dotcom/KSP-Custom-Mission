using System;

namespace MegaCrew.WatchCoverage
{
    // Half-open intervals [Start, End): the start is included and the end is
    // excluded. An operation ending exactly at End is contained; any
    // positive extension beyond End is rejected.
    public struct TimeInterval
    {
        public readonly double Start;
        public readonly double End;

        public TimeInterval(double start, double end)
        {
            if (!IsFinite(start) || !IsFinite(end))
                throw new ArgumentOutOfRangeException("interval", "Endpoints must be finite.");
            if (start < 0 || end <= start)
                throw new ArgumentOutOfRangeException("interval", "Expected 0 <= start < end.");
            Start = start;
            End = end;
        }

        internal static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        public bool IsValid
        {
            get { return IsFinite(Start) && IsFinite(End) && Start >= 0 && End > Start; }
        }

        public bool Contains(TimeInterval other)
        {
            // Struct default values bypass the validating constructor, so
            // validate both operands at this public entry point as well.
            if (!IsValid) throw new ArgumentException("Containing interval is invalid.", "interval");
            if (!other.IsValid) throw new ArgumentException("Contained interval is invalid.", "other");
            // Equal end boundaries are valid because both intervals exclude End.
            return other.Start >= Start && other.End <= End;
        }

        public override string ToString()
        {
            return "[" + Start + ", " + End + ")";
        }
    }

    public sealed class BurnWindow
    {
        public readonly double IgnitionUT;
        public readonly double BurnSeconds;
        public readonly double PreMarginSeconds;
        public readonly double PostMarginSeconds;

        public BurnWindow(double ignitionUT, double burnSeconds,
                          double preMarginSeconds, double postMarginSeconds)
        {
            if (!TimeInterval.IsFinite(ignitionUT) || ignitionUT < 0)
                throw new ArgumentOutOfRangeException("ignitionUT");
            if (!TimeInterval.IsFinite(burnSeconds) || burnSeconds <= 0)
                throw new ArgumentOutOfRangeException("burnSeconds");
            if (!TimeInterval.IsFinite(preMarginSeconds) || preMarginSeconds < 0)
                throw new ArgumentOutOfRangeException("preMarginSeconds");
            if (!TimeInterval.IsFinite(postMarginSeconds) || postMarginSeconds < 0)
                throw new ArgumentOutOfRangeException("postMarginSeconds");

            double start = ignitionUT - preMarginSeconds;
            double end = ignitionUT + burnSeconds + postMarginSeconds;
            if (!TimeInterval.IsFinite(start) || !TimeInterval.IsFinite(end) || start < 0 || end <= start)
                throw new ArgumentOutOfRangeException("burnWindow", "Burn interval overflowed or is invalid.");

            IgnitionUT = ignitionUT;
            BurnSeconds = burnSeconds;
            PreMarginSeconds = preMarginSeconds;
            PostMarginSeconds = postMarginSeconds;
        }

        public TimeInterval FullInterval
        {
            get
            {
                return new TimeInterval(IgnitionUT - PreMarginSeconds,
                    IgnitionUT + BurnSeconds + PostMarginSeconds);
            }
        }
    }

    public static class WatchCoverage
    {
        // Returns true only when the complete fixed burn window is covered by
        // the specified awake watch and by each reviewer's awake/fit window.
        // The method never adjusts ignitionUT, changes watch time, or pauses a
        // shift. These intervals are snapshots: callers must rebuild and
        // recheck them at each approval and dispatch after refreshing crew
        // awake/fit state. A prior true result is not durable authorization.
        // Callers must replan if coverage is false.
        public static bool Covers(BurnWindow burn, TimeInterval awakeWatch,
                                  TimeInterval hamishAwakeAndFit,
                                  TimeInterval basilAwakeAndFit)
        {
            if (burn == null) throw new ArgumentNullException("burn");
            TimeInterval full = burn.FullInterval;
            return awakeWatch.Contains(full)
                && hamishAwakeAndFit.Contains(full)
                && basilAwakeAndFit.Contains(full);
        }
    }
}
