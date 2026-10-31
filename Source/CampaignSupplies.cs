using System;
using System.Globalization;
using UnityEngine;
namespace MegaCrew
{
    // Fictional game tuning: routine meals/drinks are distributed continuously
    // over a standard day. Frozen crew use neither. Extra manual meals cost extra.
    public static class CampaignRations
    {
        public const double FoodPerCrewDay=1.5, WaterPerCrewDay=0.75;
        public static double Demand(double seconds,int unfrozenCrew,double perCrewDay)
        {
            if(!CampaignTiming.Finite(seconds)||seconds<0||unfrozenCrew<0||unfrozenCrew>4||!CampaignTiming.Finite(perCrewDay)||perCrewDay<0)throw new ArgumentOutOfRangeException();
            double amount=(seconds/86400)*unfrozenCrew*perCrewDay;
            if(!CampaignTiming.Finite(amount))throw new ArgumentOutOfRangeException();return amount;
        }
        public static double Take(double available,double demand)
        {
            if(!CampaignTiming.Finite(available)||available<0||!CampaignTiming.Finite(demand)||demand<0)throw new ArgumentOutOfRangeException();
            return Math.Min(available,demand);
        }
    }
    public sealed partial class MissionConsole
    {
        void UpdateCampaignSupplies(double dt)
        {
            var c=Campaign;
            if(c==null||!c.Started||!c.RationsEnabled||!HighLogic.LoadedSceneIsFlight||Ship==null||Ship.id!=c.VesselId||FlightDriver.Pause)return;
            double now=Planetarium.GetUniversalTime();
            if(!CampaignTiming.Finite(now)||now<c.SupplyUT){c.SupplyError="Supply clock regressed. Restore a consistent KSP save.";return;}
            if(c.SupplyError.Length>0)return;
            double elapsed=now-c.SupplyUT;
            // Do not infer unknown crew histories across a scene load or offline
            // interval. The future background adapter must reconcile these.
            if(elapsed>60||dt<0||dt>60){c.SupplyError="Unobserved supply interval. Background reconciliation required; coast blocked.";return;}
            if(elapsed<=0)return;
            int awake=0,mask=0,crewIndex=0;
            foreach(var person in crew)
            {
                bool frozen;
                if(!CryoBridge.TryFrozen(person.Name,out frozen)||(!frozen&&!Aboard(person.Name)))
                {c.SupplyError="Crew/pod state unavailable. Supply history requires reconciliation.";return;}
                if(!frozen){awake++;mask|=1<<crewIndex;}crewIndex++;
            }
            // Charge the higher awake count on transitions. No free food from
            // freezing at the end of an interval or double credit on thaw.
            int observedMask=c.UnfrozenMask|mask,charge=0;
            for(int i=0;i<4;i++)if((observedMask&(1<<i))!=0)charge++;
            double food=CampaignRations.Demand(elapsed,charge,CampaignRations.FoodPerCrewDay);
            double water=CampaignRations.Demand(elapsed,charge,CampaignRations.WaterPerCrewDay+(c.DailyRoutineEnabled?0.25:0));
            c.FoodUsed+=ConsumeCampaignSupply("Food",food);
            c.WaterUsed+=ConsumeCampaignSupply("Water",water);
            // A shortfall becomes a durable unmet balance, never negative stores.
            c.FoodMissing+=Math.Max(0,food-c.LastTakenFood);
            c.WaterMissing+=Math.Max(0,water-c.LastTakenWater);
            if(c.DailyRoutineEnabled)
            {
                double foodFraction=food>0?c.LastTakenFood/food:1,waterFraction=water>0?c.LastTakenWater/water:1;
                for(int i=0;i<crew.Count;i++)DailyRoutine.Advance(crew[i],c.EpochUT,c.SupplyUT,now,i,(observedMask&(1<<i))==0,(observedMask&(1<<(i^1)))!=0,foodFraction,waterFraction);
            }
            c.SupplyUT=now;c.PreviousUnfrozen=awake;c.UnfrozenMask=mask;
            // Shortage is a known observed state, not missing history. Keep
            // accounting and physiology alive; never synthesize a meal or repay
            // old unmet demand by silently taking extra stores after resupply.
            c.SupplyDepleted=food-c.LastTakenFood>1e-8||water-c.LastTakenWater>1e-8;
        }
        double ConsumeCampaignSupply(string name,double demand)
        {
            double left=demand;
            foreach(var p in Ship.parts)foreach(PartResource r in p.Resources)
                if(r.resourceName==name&&r.flowState){double take=CampaignRations.Take(r.amount,left);r.amount-=take;left-=take;}
            double taken=demand-left;var c=Campaign;
            if(name=="Food")c.LastTakenFood=taken;else c.LastTakenWater=taken;
            return taken;
        }
        void DrawCampaignSupplies(HermesCampaign c)
        {
            GUILayout.Label("Routine ration plan: 1.5 Food + 0.75 Water per unfrozen crew per standard day. Frozen crew consume neither; manual meals/drinks are additional. This accounts stores, not automatic task completion or physiological care.");
            if(!c.RationsEnabled)
            {
                GUI.enabled=HighLogic.LoadedSceneIsFlight&&Ship!=null&&Ship.id==c.VesselId;
                if(GUILayout.Button("Enable finite routine ration plan"))
                {c.RationsEnabled=true;c.SupplyUT=Planetarium.GetUniversalTime();c.PreviousUnfrozen=4;c.UnfrozenMask=15;message="Routine ration plan enabled. Save the game to persist the plan and ledger.";}
                GUI.enabled=true;
            }
            if(c.RationsEnabled&&!c.DailyRoutineEnabled)
            {
                GUI.enabled=HighLogic.LoadedSceneIsFlight&&Ship!=null&&Ship.id==c.VesselId&&c.SupplyError.Length==0;
                if(GUILayout.Button("Schedule daily sleep, meals and crew care")){c.DailyRoutineEnabled=true;message="Daily routine scheduled: staggered eight-hour ordinary sleep and finite supplies; save to persist. No animated crew movement is implemented.";}
                GUI.enabled=true;
            }
            if(c.DailyRoutineEnabled&&GUILayout.Button("Suspend daily routine for manual operations")){c.DailyRoutineEnabled=false;message="Automatic daily routine suspended; campaign clock continues. Wake crew manually as required. Routine rations remain finite.";}
            if(c.DailyRoutineEnabled)GUILayout.Label("Daily routine active: each unfrozen crew rests eight hours per standard day, staggered within the pair. Modeled hygiene uses an extra 0.25 Water per crew/day; exercise/social care require time and awake coverage. No maintenance repairs are automatic.");
            GUILayout.Label("Routine used: Food "+c.FoodUsed.ToString("F3")+", Water "+c.WaterUsed.ToString("F3")+" | Cumulative unmet: "+c.FoodMissing.ToString("F3")+" / "+c.WaterMissing.ToString("F3"));
            if(c.SupplyDepleted)GUILayout.Label("Routine rations depleted. Observed needs and cumulative unmet demand continue; restore accessible supplies. Past shortfalls remain in the log. Coast blocked.");
            if(c.SupplyError.Length>0)GUILayout.Label(c.SupplyError);
            GUILayout.Label("24-month baseline with two unfrozen crew: 2160 Food + 1080 Water (1440 Water with daily hygiene), before reserve, handovers and extra meals. Existing small pantry alone is insufficient. No free provisions are added by this plan.");
        }
    }
}
