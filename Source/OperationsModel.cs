using System;
namespace MegaCrew
{
    public sealed class ResourceTrend
    {
        double lastTime, lastAmount;
        public double Rate { get; private set; }
        public bool Ready { get; private set; }
        public void Reset() { Ready = false; lastTime = 0; Rate = 0; }
        public void Sample(double time, double amount)
        {
            if (double.IsNaN(time) || double.IsNaN(amount) || double.IsInfinity(time) || double.IsInfinity(amount)) { Reset(); return; }
            double dt = time - lastTime;
            if (lastTime > 0 && dt >= 1 && dt <= 5) { Rate = (amount - lastAmount) / dt; Ready = true; }
            else Ready = false;
            lastTime = time; lastAmount = amount;
        }
        public double SecondsRemaining(double amount) { return Ready && Rate < -0.000001 ? Math.Max(0, amount) / -Rate : double.PositiveInfinity; }
    }
}
