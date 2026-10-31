using System;
using MegaCrew.WatchCoverage;

internal static class WatchCoverageTests
{
    private static int passed;

    private static void True(bool value, string name)
    {
        if (!value) throw new Exception("FAIL: " + name);
        passed++;
    }

    private static void Throws<T>(Action action, string name) where T : Exception
    {
        try { action(); }
        catch (T) { passed++; return; }
        throw new Exception("FAIL: " + name + " did not throw " + typeof(T).Name);
    }

    private static bool Covers(BurnWindow burn, TimeInterval watch,
                               TimeInterval hamish, TimeInterval basil)
    {
        return WatchCoverage.Covers(burn, watch, hamish, basil);
    }

    public static int Main()
    {
        TimeInterval watch = new TimeInterval(100, 200);
        TimeInterval hamish = new TimeInterval(100, 200);
        TimeInterval basil = new TimeInterval(100, 200);

        True(Covers(new BurnWindow(150, 10, 5, 5), watch, hamish, basil), "all intervals cover burn");
        True(!Covers(new BurnWindow(104, 10, 5, 0), watch, hamish, basil), "pre-margin crosses watch start");
        True(!Covers(new BurnWindow(190, 10, 0, 1), watch, hamish, basil), "post-margin reaches watch end");
        True(!Covers(new BurnWindow(150, 10, 5, 5), watch,
            new TimeInterval(110, 155), basil), "Hamish fit window too short");
        True(!Covers(new BurnWindow(150, 10, 5, 5), watch, hamish,
            new TimeInterval(145, 164)), "Basil fit window too short");
        True(!Covers(new BurnWindow(150, 10, 5, 5),
            new TimeInterval(140, 160), hamish, basil), "awake watch too short");
        True(!Covers(new BurnWindow(150, 10, 5, 5), watch,
            new TimeInterval(100, 160), new TimeInterval(160, 200)), "reviewer fit windows cannot be combined");

        // Half-open semantics: a burn ending exactly at the watch's End is
        // accepted; any post-margin beyond End is rejected. Start is included.
        True(Covers(new BurnWindow(190, 10, 0, 0), watch, hamish, basil), "end exactly at boundary is covered");
        True(!Covers(new BurnWindow(190, 10, 0, 0.0001), watch, hamish, basil), "end beyond boundary rejected");
        True(Covers(new BurnWindow(110, 10, 10, 0), watch, hamish, basil), "start exactly at boundary is covered");
        True(Covers(new BurnWindow(150, 10, 5, 5), watch,
            new TimeInterval(100, 165), basil), "Hamish fit end boundary is covered");
        True(Covers(new BurnWindow(150, 10, 5, 5), watch, hamish,
            new TimeInterval(145, 165)), "Basil fit end boundary is covered");

        // Coverage is a snapshot calculation. Recompute after observed fitness
        // changes rather than reusing an earlier successful result.
        BurnWindow planned = new BurnWindow(150, 10, 5, 5);
        True(Covers(planned, watch, hamish, basil), "fresh reviewer intervals cover burn");
        TimeInterval basilAfterFatigueChange = new TimeInterval(100, 164);
        True(!Covers(planned, watch, hamish, basilAfterFatigueChange),
            "recomputed coverage rejects newly shortened reviewer window");

        Throws<ArgumentOutOfRangeException>(() => new TimeInterval(double.NaN, 1), "NaN endpoint");
        Throws<ArgumentOutOfRangeException>(() => new TimeInterval(0, double.PositiveInfinity), "infinite endpoint");
        Throws<ArgumentOutOfRangeException>(() => new TimeInterval(2, 2), "empty interval");
        Throws<ArgumentOutOfRangeException>(() => new TimeInterval(3, 2), "reversed interval");
        Throws<ArgumentOutOfRangeException>(() => new TimeInterval(-1, 2), "negative UT");
        Throws<ArgumentOutOfRangeException>(() => new BurnWindow(double.NaN, 1, 0, 0), "NaN ignition");
        Throws<ArgumentOutOfRangeException>(() => new BurnWindow(10, double.PositiveInfinity, 0, 0), "infinite burn duration");
        Throws<ArgumentOutOfRangeException>(() => new BurnWindow(10, 1, -1, 0), "negative pre-margin");
        Throws<ArgumentOutOfRangeException>(() => new BurnWindow(double.MaxValue, double.MaxValue, 0, 0), "overflowed end");
        Throws<ArgumentOutOfRangeException>(() => new BurnWindow(1, 1, 2, 0), "negative computed start");
        Throws<ArgumentNullException>(() => WatchCoverage.Covers(null, watch, hamish, basil), "null burn");
        Throws<ArgumentException>(() => default(TimeInterval).Contains(watch), "default containing interval");
        Throws<ArgumentException>(() => watch.Contains(default(TimeInterval)), "default contained interval");

        Console.WriteLine("PASS: " + passed + " watch coverage cases");
        return 0;
    }
}
