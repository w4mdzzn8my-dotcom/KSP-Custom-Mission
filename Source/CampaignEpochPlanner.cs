using System;
using System.Collections.Generic;
namespace MegaCrew
{
    // Pre-start planning only. Neither this model nor its result edits campaign
    // state, a trajectory, crew, KSP UT, or the fixed four-month watch lengths.
    public struct BurnDutyInterval
    {
        public readonly double StartUT,EndUT;
        public BurnDutyInterval(double ignitionUT,double burnSeconds,double alignSeconds,double verifySeconds)
        {
            if(!Valid(ignitionUT)||!Valid(burnSeconds)||!Valid(alignSeconds)||!Valid(verifySeconds)||burnSeconds<=0)
                throw new ArgumentOutOfRangeException();
            StartUT=ignitionUT-alignSeconds;EndUT=ignitionUT+burnSeconds+verifySeconds;
            if(!Valid(StartUT)||!Valid(EndUT)||EndUT<=StartUT)throw new ArgumentOutOfRangeException();
        }
        static bool Valid(double n){return CampaignTiming.Finite(n)&&n>=0;}
    }
    public struct CampaignEpochWindow
    {
        public readonly double EarliestUT,LatestUT;
        public CampaignEpochWindow(double earliest,double latest){EarliestUT=earliest;LatestUT=latest;}
        public bool Contains(double epoch){return CampaignTiming.Finite(epoch)&&epoch>=EarliestUT&&epoch<=LatestUT;}
    }
    public static class CampaignEpochPlanner
    {
        // Intersect the candidate start range with each burn's three possible
        // human-watch windows. An empty result means this fixed rotation cannot
        // cover the supplied physical trajectory; the trajectory planner must
        // propose and verify another real trajectory, not move ignition labels.
        public static List<CampaignEpochWindow> Find(double earliestEpochUT,double latestEpochUT,IEnumerable<BurnDutyInterval> burns)
        {
            if(burns==null||!Valid(earliestEpochUT)||!Valid(latestEpochUT)||latestEpochUT<earliestEpochUT||
                !Valid(latestEpochUT+CampaignTiming.MissionSeconds)||latestEpochUT+CampaignTiming.MissionSeconds<=latestEpochUT)
                throw new ArgumentOutOfRangeException();
            var feasible=new List<CampaignEpochWindow>{new CampaignEpochWindow(earliestEpochUT,latestEpochUT)};
            int count=0;
            foreach(var burn in burns)
            {
                if(++count>128)throw new ArgumentOutOfRangeException("burns","Review at most 128 burn duty intervals per plan");
                if(!Valid(burn.StartUT)||!Valid(burn.EndUT)||burn.EndUT<=burn.StartUT)throw new ArgumentOutOfRangeException("burns");
                var next=new List<CampaignEpochWindow>();
                foreach(var current in feasible)
                    for(int human=0;human<6;human+=2)
                    {
                        double first=Math.Max(current.EarliestUT,burn.EndUT-(human+1)*CampaignTiming.WatchSeconds);
                        double last=Math.Min(current.LatestUT,burn.StartUT-human*CampaignTiming.WatchSeconds);
                        if(first<=last)next.Add(new CampaignEpochWindow(first,last));
                    }
                next.Sort((a,b)=>a.EarliestUT.CompareTo(b.EarliestUT));
                feasible=new List<CampaignEpochWindow>();
                foreach(var range in next)
                {
                    int i=feasible.Count-1;
                    if(i>=0&&range.EarliestUT<=feasible[i].LatestUT)
                        feasible[i]=new CampaignEpochWindow(feasible[i].EarliestUT,Math.Max(feasible[i].LatestUT,range.LatestUT));
                    else feasible.Add(range);
                }
                // Keep validating remaining input even after a conflict.
            }
            return feasible;
        }
        static bool Valid(double n){return CampaignTiming.Finite(n)&&n>=0;}
    }
}
