using System;
using MegaCrew;
class OperationsModelTests
{
    static void Yes(bool value) { if (!value) throw new Exception("Condition failed"); }
    static void Main()
    {
        var trend = new ResourceTrend();
        trend.Sample(100,100); Yes(!trend.Ready);
        trend.Sample(102,90); Yes(trend.Ready && trend.Rate == -5 && trend.SecondsRemaining(90) == 18);
        trend.Sample(104,95); Yes(trend.Ready && double.IsPositiveInfinity(trend.SecondsRemaining(95)));
        trend.Sample(106,95); Yes(double.IsPositiveInfinity(trend.SecondsRemaining(95)));
        trend.Sample(1000,50); Yes(!trend.Ready); // warp/load jump must not become consumption
        trend.Sample(1002,40); Yes(trend.Ready && trend.Rate == -5);
        trend.Sample(50,10); Yes(!trend.Ready); // revert / clock regression
        trend.Sample(double.NaN,0); Yes(!trend.Ready);
        trend.Sample(200,10); Yes(!trend.Ready);
        trend.Sample(202,0); Yes(trend.SecondsRemaining(0)==0);
        trend.Reset(); Yes(!trend.Ready && double.IsPositiveInfinity(trend.SecondsRemaining(100)));
        Console.WriteLine("PASS: depletion estimate, recharge, steady state, warp/load gaps, clock regression, invalid samples and reset.");
    }
}
