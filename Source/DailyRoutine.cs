using System;
namespace MegaCrew
{
    // Deterministic gameplay routines over actual KSP UT, not a replacement clock.
    // Call only for observed, resource-accounted intervals; no part repair/animation.
    public static class DailyRoutine
    {
        public const double Day=86400, SleepSeconds=8*3600;
        static void Time(double t){if(double.IsNaN(t)||double.IsInfinity(t)||t<0)throw new ArgumentOutOfRangeException("time");}
        static double Phase(double epoch,double ut,int index)
        {
            double phase=(ut-epoch-(index%2)*43200)%Day;return phase<0?phase+Day:phase;
        }
        public static bool RestingAt(double epoch,double ut,int index)
        {
            Time(epoch);Time(ut);if(ut<epoch||index<0||index>3)throw new ArgumentOutOfRangeException();
            return Phase(epoch,ut,index)<SleepSeconds;
        }
        static double UntilBoundary(double epoch,double ut,int index)
        {
            double p=Phase(epoch,ut,index);return p<SleepSeconds?SleepSeconds-p:Day-p;
        }
        public static void Advance(CrewRecord c,double epoch,double start,double end,int index,bool frozen,bool partnerUnfrozen,double foodFraction,double waterFraction)
        {
            Time(epoch);Time(start);Time(end);
            if(c==null||start<epoch||end<start||end-start>CampaignTiming.MissionSeconds||index<0||index>3||
                double.IsNaN(foodFraction)||foodFraction<0||foodFraction>1||double.IsNaN(waterFraction)||waterFraction<0||waterFraction>1)throw new ArgumentOutOfRangeException();
            if(frozen){c.Resting=false;c.Fatigue=NeedsModel.Clamp(c.Fatigue-(end-start)*100/43200);return;}
            double now=start;
            while(now<end)
            {
                // Split at both crewmates' rest boundaries, so the outcome is
                // independent of frame size even over multiple mission days.
                double span=Math.Min(end-now,Math.Min(UntilBoundary(epoch,now,index),UntilBoundary(epoch,now,index^1)));
                if(span<=0||now+span<=now)throw new InvalidOperationException("Routine clock cannot advance");
                double midpoint=now+span/2;
                bool resting=partnerUnfrozen&&RestingAt(epoch,midpoint,index);
                double days=span/Day;
                c.Hunger=NeedsModel.Clamp(c.Hunger+days*(100-120*foodFraction));
                c.Thirst=NeedsModel.Clamp(c.Thirst+days*(100-120*waterFraction));
                c.Fatigue=NeedsModel.Clamp(c.Fatigue+span*(resting?-100.0/43200:100.0/86400));
                c.Hygiene=NeedsModel.Clamp(c.Hygiene+days*(-25+(resting?0:45*waterFraction)));
                c.Conditioning=NeedsModel.Clamp(c.Conditioning+days*(-15+(resting||c.Injury!="None"?0:30*Math.Min(foodFraction,waterFraction))));
                bool together=partnerUnfrozen&&!resting&&!RestingAt(epoch,midpoint,index^1);
                c.Morale=NeedsModel.Clamp(c.Morale+days*(-10+(together?45:0)));
                now+=span;
            }
            c.Resting=partnerUnfrozen&&RestingAt(epoch,end,index);
        }
    }
}
