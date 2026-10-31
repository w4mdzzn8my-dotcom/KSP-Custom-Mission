using System;
namespace MegaCrew
{
    // Pure decision only. A future KSP adapter must refresh every observation on
    // each dispatch, cap stock warp safely, and stop BEFORE StopUT. Never set UT.
    public struct CoastObservation
    {
        public double EpochUT, CurrentUT, ObservedUT, NextStopUT, ResourcesVerifiedThroughUT;
        public bool CampaignVessel, HumanPairAwake, HumanPairFrozen, ReliefPairAwake, ReliefPairFrozen;
        public bool CryoStateVerified, BackgroundAccountingVerified, HaltRequested;
    }
    public struct CoastDecision
    {
        public readonly double TargetRate, StopUT;
        public readonly string Reason;
        public bool CanAccelerate { get { return TargetRate>1; } }
        public CoastDecision(double rate,double stop,string reason){TargetRate=rate;StopUT=stop;Reason=reason;}
    }
    public static class CampaignCoast
    {
        public static CoastDecision Decide(CoastObservation s)
        {
            if(!CampaignTiming.Finite(s.CurrentUT)||s.CurrentUT<0)return new CoastDecision(1,0,"Invalid clock");
            double now=s.CurrentUT;
            if(!CampaignTiming.Finite(s.EpochUT)||s.EpochUT<0||now<s.EpochUT)return Hold(now,"Invalid campaign epoch");
            // Exact sample epoch: stale/future snapshots must never grant warp.
            if(s.ObservedUT!=now)return Hold(now,"Refresh vessel observations");
            if(!s.CampaignVessel||s.HaltRequested)return Hold(now,"Coast not authorized");
            int watch=CampaignTiming.WatchAt(s.EpochUT,now);
            if(watch==6)return Hold(now,"Campaign complete");
            if(!s.CryoStateVerified||!s.BackgroundAccountingVerified)return Hold(now,"Cryo/resource accounting unverified");
            bool human=CampaignTiming.HumanWatch(watch);
            // Pair flags require verified cryopod state for both members. Both
            // pairs must be observed: unknown/mixed/transition states grant no coast.
            if(human&&(!s.HumanPairAwake||s.HumanPairFrozen||!s.ReliefPairFrozen||s.ReliefPairAwake))return Hold(now,"Human watch requires awake human crew and frozen relief crew");
            if(!human&&(!s.HumanPairFrozen||s.HumanPairAwake||!s.ReliefPairAwake||s.ReliefPairFrozen))return Hold(now,"NPC watch requires frozen human crew and awake relief crew");
            // NextStopUT includes alignment, work, handover, and operational limits,
            // not merely ignition. Unknown next event refuses accelerated coast.
            if(!CampaignTiming.Finite(s.NextStopUT)||s.NextStopUT<=now)return Hold(now,"Next operational stop missing or due");
            double stop=Math.Min(s.NextStopUT,CampaignTiming.WatchEndUT(s.EpochUT,watch));
            if(!CampaignTiming.Finite(s.ResourcesVerifiedThroughUT)||s.ResourcesVerifiedThroughUT<stop)return Hold(now,"Insufficient verified resource horizon");
            return new CoastDecision(human?CampaignTiming.HumanCoastRate:CampaignTiming.NpcCoastRate,stop,"Coast candidate; runtime rate limits still apply");
        }
        static CoastDecision Hold(double now,string reason){return new CoastDecision(1,now,reason);}
    }
}
