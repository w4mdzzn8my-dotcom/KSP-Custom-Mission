using System;

namespace MegaCrew
{
    // Pure UT contract for the future warp controller and maneuver planner.
    // No method here changes KSP time, crew, resources, nodes or throttle.
    public static class CampaignTiming
    {
        public const double MonthSeconds = 30 * 24 * 3600;
        public const double WatchSeconds = 4 * MonthSeconds;
        public const double MissionSeconds = 24 * MonthSeconds;
        public const double HumanCoastRate = MonthSeconds / (80 * 60);
        public const double NpcCoastRate = WatchSeconds / 60;
        public const double BaselinePlaySeconds = 3 * (4 * 80 * 60 + 60);
        public static bool Finite(double value){return !double.IsNaN(value)&&!double.IsInfinity(value);}
        static void Time(double value,string name){if(!Finite(value)||value<0)throw new ArgumentOutOfRangeException(name);}
        // Returns 6 at/after completion; never wraps back to the first watch.
        public static int WatchAt(double epochUT,double currentUT)
        {
            Time(epochUT,"epochUT");Time(currentUT,"currentUT");
            if(currentUT<epochUT)throw new ArgumentOutOfRangeException("currentUT","Clock is before the recorded campaign epoch");
            return (int)Math.Min(6,Math.Floor((currentUT-epochUT)/WatchSeconds));
        }
        public static bool HumanWatch(int watch){if(watch<0||watch>5)throw new ArgumentOutOfRangeException("watch");return watch%2==0;}
        public static double WatchEndUT(double epochUT,int watch)
        {
            Time(epochUT,"epochUT");if(watch<0||watch>5)throw new ArgumentOutOfRangeException("watch");
            double end=epochUT+(watch+1)*WatchSeconds;if(!Finite(end))throw new ArgumentOutOfRangeException("epochUT");return end;
        }
        // The entire alignment/burn/verification interval must lie in one human watch.
        // The solver must revise the watch plan if this fails; never move a burn epoch arbitrarily.
        public static bool CoversBurn(double epochUT,double ignitionUT,double burnSeconds,double alignSeconds,double verifySeconds)
        {
            Time(epochUT,"epochUT");Time(ignitionUT,"ignitionUT");Time(burnSeconds,"burnSeconds");Time(alignSeconds,"alignSeconds");Time(verifySeconds,"verifySeconds");
            double start=ignitionUT-alignSeconds,end=ignitionUT+burnSeconds+verifySeconds;
            if(!Finite(end)||start<epochUT)return false;
            int watch=WatchAt(epochUT,start);
            return watch<6&&HumanWatch(watch)&&end<=WatchEndUT(epochUT,watch);
        }
        public static double ExtraBurnPlaySeconds(double burnSeconds,double executionRate=1)
        {
            Time(burnSeconds,"burnSeconds");if(!Finite(executionRate)||executionRate<=0||executionRate>HumanCoastRate)throw new ArgumentOutOfRangeException("executionRate");
            double extra=burnSeconds/executionRate-burnSeconds/HumanCoastRate;
            if(!Finite(extra))throw new ArgumentOutOfRangeException("burnSeconds");return extra;
        }
    }
}
