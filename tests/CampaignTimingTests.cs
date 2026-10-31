using System;
using MegaCrew;
class CampaignTimingTests
{
    static void Yes(bool v){if(!v)throw new Exception("Assertion failed");}
    static void Equal(double a,double b){if(Math.Abs(a-b)>1e-7)throw new Exception("Expected "+b+", got "+a);}
    static void Reject(Action f){try{f();}catch(ArgumentOutOfRangeException){return;}throw new Exception("Invalid time accepted");}
    static void Main()
    {
        Equal(CampaignTiming.HumanCoastRate,540);Equal(CampaignTiming.NpcCoastRate,172800);Equal(CampaignTiming.BaselinePlaySeconds,57780);
        double epoch=100000,w=CampaignTiming.WatchSeconds;
        for(int i=0;i<6;i++){Equal(CampaignTiming.WatchAt(epoch,epoch+i*w),i);Yes(CampaignTiming.HumanWatch(i)==(i%2==0));Equal(CampaignTiming.WatchEndUT(epoch,i),epoch+(i+1)*w);}
        Equal(CampaignTiming.WatchAt(epoch,epoch+CampaignTiming.MissionSeconds),6);
        Equal(CampaignTiming.WatchAt(epoch,epoch+CampaignTiming.MissionSeconds+100),6);
        Yes(CampaignTiming.CoversBurn(epoch,epoch+1000,60,120,60));
        Yes(!CampaignTiming.CoversBurn(epoch,epoch+w+1000,60,120,60));
        Yes(!CampaignTiming.CoversBurn(epoch,epoch+w-10,60,0,0));
        Yes(!CampaignTiming.CoversBurn(epoch,epoch+10,60,120,60));
        Yes(CampaignTiming.CoversBurn(epoch,epoch+w-60,60,0,0));
        Equal(CampaignTiming.ExtraBurnPlaySeconds(540),539);
        Equal(CampaignTiming.ExtraBurnPlaySeconds(540,540),0);
        // Actual UT continues through a 1x burn: it may cross a shift boundary.
        Equal(CampaignTiming.WatchAt(epoch,epoch+w-1),0);Equal(CampaignTiming.WatchAt(epoch,epoch+w+1),1);
        Reject(()=>CampaignTiming.WatchAt(epoch,epoch-1));Reject(()=>CampaignTiming.WatchAt(0,double.NaN));Reject(()=>CampaignTiming.WatchEndUT(0,6));Reject(()=>CampaignTiming.HumanWatch(6));Reject(()=>CampaignTiming.ExtraBurnPlaySeconds(1,0));Reject(()=>CampaignTiming.CoversBurn(0,100,double.PositiveInfinity,0,0));
        Equal(CampaignRations.Demand(86400,2,CampaignRations.FoodPerCrewDay),3);
        Equal(CampaignRations.Demand(86400,2,CampaignRations.WaterPerCrewDay),1.5);
        Equal(CampaignRations.Demand(CampaignTiming.MissionSeconds,2,CampaignRations.FoodPerCrewDay),2160);
        Equal(CampaignRations.Demand(CampaignTiming.MissionSeconds,2,CampaignRations.WaterPerCrewDay),1080);
        Equal(CampaignRations.Demand(86400,0,1.5),0);
        Equal(CampaignRations.Take(1,3),1);Equal(CampaignRations.Take(5,3),3);
        Equal(CampaignRations.Demand(30,2,1.5)*2,CampaignRations.Demand(60,2,1.5));
        Reject(()=>CampaignRations.Demand(double.NaN,2,1.5));Reject(()=>CampaignRations.Demand(1,5,1.5));Reject(()=>CampaignRations.Take(-1,1));
        Console.WriteLine("PASS: finite routine demand, frozen exclusion, partitioning, depletion and 24-month supply budget.");
        Equal(CryoPowerModel.PodLoad(10,2),1.0/3);
        Equal(CryoPowerModel.PodLoad(10,0),0);
        Equal(CryoPowerModel.TimeToReserve(1000,100,1.0/3,0),2700);
        Equal(CryoPowerModel.TimeToReserve(100,100,1,0),0);
        Yes(double.IsPositiveInfinity(CryoPowerModel.TimeToReserve(1000,100,1,1)));
        Equal(CryoPowerModel.StoredAfter(100,200,1,0,1000),0);
        Equal(CryoPowerModel.StoredAfter(100,200,0,1,1000),200);
        double battery=1000;for(int n=0;n<60;n++)battery=CryoPowerModel.StoredAfter(battery,2000,1,0.5,1);
        Equal(battery,CryoPowerModel.StoredAfter(1000,2000,1,0.5,60));
        Reject(()=>CryoPowerModel.PodLoad(10,-1));Reject(()=>CryoPowerModel.StoredAfter(201,200,1,0,1));Reject(()=>CryoPowerModel.TimeToReserve(double.NaN,0,1,0));
        Console.WriteLine("PASS: cryopod EC/min conversion, reserve horizon, depletion/recharge bounds and finite-input rejection. Forecast only; does not certify generation or authorize warp.");
        var state=new CoastObservation {EpochUT=epoch,CurrentUT=epoch,ObservedUT=epoch,NextStopUT=epoch+w+100,ResourcesVerifiedThroughUT=epoch+w,CampaignVessel=true,HumanPairAwake=true,ReliefPairFrozen=true,CryoStateVerified=true,BackgroundAccountingVerified=true};
        var coast=CampaignCoast.Decide(state);Yes(coast.CanAccelerate);Equal(coast.TargetRate,540);Equal(coast.StopUT,epoch+w);
        state.ObservedUT--;Yes(!CampaignCoast.Decide(state).CanAccelerate);state.ObservedUT=state.CurrentUT;
        state.ResourcesVerifiedThroughUT--;Yes(!CampaignCoast.Decide(state).CanAccelerate);state.ResourcesVerifiedThroughUT=epoch+2*w;
        state.BackgroundAccountingVerified=false;Yes(!CampaignCoast.Decide(state).CanAccelerate);state.BackgroundAccountingVerified=true;
        state.CurrentUT=state.ObservedUT=epoch+w;state.NextStopUT=epoch+2*w;
        Yes(!CampaignCoast.Decide(state).CanAccelerate);state.HumanPairAwake=false;state.HumanPairFrozen=true;
        Yes(!CampaignCoast.Decide(state).CanAccelerate);state.ReliefPairAwake=true;state.ReliefPairFrozen=false;
        Equal(CampaignCoast.Decide(state).TargetRate,172800);
        state.NextStopUT=state.CurrentUT;Yes(!CampaignCoast.Decide(state).CanAccelerate);
        state.NextStopUT=epoch+2*w;state.HaltRequested=true;Yes(!CampaignCoast.Decide(state).CanAccelerate);state.HaltRequested=false;
        state.CurrentUT=state.ObservedUT=epoch+CampaignTiming.MissionSeconds;Yes(!CampaignCoast.Decide(state).CanAccelerate);
        state.CurrentUT=double.NaN;Yes(!CampaignCoast.Decide(state).CanAccelerate);
        // All pair flags at every watch: only the exact completed handover
        // may grant a candidate rate. Contradictory and unobserved pairs refuse.
        for(int watch=0;watch<6;watch++)for(int flags=0;flags<16;flags++)
        {
            var handover=new CoastObservation {
                EpochUT=epoch,CurrentUT=epoch+watch*w,ObservedUT=epoch+watch*w,
                NextStopUT=epoch+(watch+1)*w,ResourcesVerifiedThroughUT=epoch+(watch+1)*w,
                CampaignVessel=true,CryoStateVerified=true,BackgroundAccountingVerified=true,
                HumanPairAwake=(flags&1)!=0,HumanPairFrozen=(flags&2)!=0,
                ReliefPairAwake=(flags&4)!=0,ReliefPairFrozen=(flags&8)!=0
            };
            bool expected=flags==(CampaignTiming.HumanWatch(watch)?9:6);
            Yes(CampaignCoast.Decide(handover).CanAccelerate==expected);
            handover.CryoStateVerified=false;Yes(!CampaignCoast.Decide(handover).CanAccelerate);
        }
        Console.WriteLine("PASS: 96 handover flag/watch combinations and refusal when cryo verification is missing; pure decision only.");
        Console.WriteLine("PASS: coast preconditions, fresh observations, resource horizon, awake/frozen watch handovers, stop boundaries and fail-closed invalid state.");
        Console.WriteLine("PASS: campaign rates, six watch boundaries, completion, full burn windows, non-paused UT, added burn time and invalid inputs.");
    }
}
