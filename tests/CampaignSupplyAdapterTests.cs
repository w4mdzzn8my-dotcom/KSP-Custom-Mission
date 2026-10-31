using System;
using System.Collections.Generic;
using MegaCrew;
namespace UnityEngine
{
    public static class GUI{public static bool enabled;}
    public static class GUILayout{public static void Label(string s){}public static bool Button(string s){return false;}}
}
public static class HighLogic{public static bool LoadedSceneIsFlight=true;}
public static class FlightDriver{public static bool Pause;}
public static class Planetarium{public static double UT;public static double GetUniversalTime(){return UT;}}
public class PartResource{public string resourceName;public bool flowState=true;public double amount;}
public class Part{public List<PartResource> Resources=new List<PartResource>();}
public class Vessel{public Guid id=Guid.NewGuid();public List<Part> parts=new List<Part>();}
namespace MegaCrew
{
    public class HermesCampaign
    {
        public bool Started=true,RationsEnabled=true,DailyRoutineEnabled,SupplyDepleted;
        public double EpochUT;public int UnfrozenMask=15;
        public Guid VesselId;
        public double SupplyUT,FoodUsed,WaterUsed,FoodMissing,WaterMissing,LastTakenFood,LastTakenWater;
        public int PreviousUnfrozen;
        public string SupplyError="";
    }
    public static class CryoBridge
    {
        public static bool Verified=true;
        public static HashSet<string> Frozen=new HashSet<string>();
        public static bool TryFrozen(string name,out bool frozen){frozen=Frozen.Contains(name);return Verified;}
    }
    public sealed partial class MissionConsole
    {
        HermesCampaign Campaign;Vessel Ship;string message;
        List<CrewRecord> crew=new List<CrewRecord>();
        bool Aboard(string n){return true;}
        public MissionConsole(HermesCampaign c,Vessel v){Campaign=c;Ship=v;for(int i=0;i<4;i++)crew.Add(new CrewRecord(i.ToString(),"test"));}
        public CrewRecord GetCrew(int i){return crew[i];}
        public void Step(double dt){UpdateCampaignSupplies(dt);}
    }
}
class CampaignSupplyAdapterTests
{
    static void Yes(bool x){if(!x)throw new Exception("Assertion failed");}
    static void Eq(double x,double y){if(Math.Abs(x-y)>1e-10)throw new Exception(x+" != "+y);}
    static void Main()
    {
        var food=new PartResource{resourceName="Food",amount=10};var water=new PartResource{resourceName="Water",amount=10};
        var part=new Part();part.Resources.Add(food);part.Resources.Add(water);
        var locked=new PartResource{resourceName="Food",amount=100,flowState=false};part.Resources.Add(locked);
        var ship=new Vessel();ship.parts.Add(part);
        var state=new HermesCampaign{VesselId=ship.id,PreviousUnfrozen=2,UnfrozenMask=3};
        var console=new MissionConsole(state,ship);CryoBridge.Frozen.Add("2");CryoBridge.Frozen.Add("3");
        Planetarium.UT=30;console.Step(30);
        double demand=CampaignRations.Demand(30,2,1.5);Eq(state.FoodUsed,demand);Eq(food.amount,10-demand);Eq(locked.amount,100);
        console.Step(0);Eq(state.FoodUsed,demand); // duplicate UT must not double charge
        CryoBridge.Frozen.Add("0");CryoBridge.Frozen.Add("1");Planetarium.UT=60;console.Step(30);
        Eq(state.FoodUsed,2*demand); // conservative old/new transition count
        Planetarium.UT=90;console.Step(30);Eq(state.FoodUsed,2*demand); // all genuinely frozen
        CryoBridge.Frozen.Clear();Planetarium.UT=120;console.Step(30);Eq(state.FoodUsed,4*demand);
        food.amount=0;water.amount=0;Planetarium.UT=150;console.Step(30);
        Yes(state.FoodMissing>0&&state.WaterMissing>0);Eq(food.amount,0);Eq(water.amount,0);Yes(state.SupplyDepleted);Yes(state.SupplyError=="");
        double used=state.FoodUsed,missing=state.FoodMissing;Planetarium.UT=180;console.Step(30);Eq(state.FoodUsed,used);Yes(state.FoodMissing>missing);Eq(state.SupplyUT,180); // observed depletion is still accounted
        food.amount=5;water.amount=5;missing=state.FoodMissing;Planetarium.UT=210;console.Step(30);Yes(!state.SupplyDepleted);Eq(state.FoodMissing,missing);Eq(state.FoodUsed,used+2*demand); // four crew, no retroactive extra charge
        state.SupplyError="";state.SupplyUT=180;Planetarium.UT=300;console.Step(120);Yes(state.SupplyError.Contains("Unobserved"));Eq(state.SupplyUT,180);
        state.SupplyError="";Planetarium.UT=170;console.Step(-10);Yes(state.SupplyError.Contains("regressed"));
        state.SupplyError="";state.SupplyUT=300;Planetarium.UT=330;CryoBridge.Verified=false;console.Step(30);Yes(state.SupplyError.Contains("unavailable"));Eq(state.SupplyUT,300);
        CryoBridge.Verified=true;CryoBridge.Frozen.Add("2");CryoBridge.Frozen.Add("3");food.amount=5;water.amount=5;
        var dailyState=new HermesCampaign{EpochUT=0,VesselId=ship.id,DailyRoutineEnabled=true,SupplyUT=330,UnfrozenMask=3,PreviousUnfrozen=2};
        var daily=new MissionConsole(dailyState,ship);daily.GetCrew(0).Hunger=50;daily.GetCrew(0).Thirst=50;
        Planetarium.UT=360;daily.Step(30);
        Eq(daily.GetCrew(0).Hunger,50-20.0*30/86400);Eq(daily.GetCrew(0).Thirst,50-20.0*30/86400);
        Yes(daily.GetCrew(0).Resting);Yes(!daily.GetCrew(1).Resting);Yes(!daily.GetCrew(2).Resting);
        Eq(dailyState.WaterUsed,30.0*2/86400);Eq(water.amount,5-dailyState.WaterUsed);Yes(dailyState.SupplyError=="");
        double unchanged=daily.GetCrew(0).Hunger;daily.Step(0);Eq(daily.GetCrew(0).Hunger,unchanged);
        food.amount=0;water.amount=0;Planetarium.UT=390;daily.Step(30);
        Yes(dailyState.SupplyDepleted);Yes(dailyState.SupplyError=="");
        double hunger=daily.GetCrew(0).Hunger,thirst=daily.GetCrew(0).Thirst,debt=dailyState.FoodMissing;
        Planetarium.UT=420;daily.Step(30);Yes(daily.GetCrew(0).Hunger>hunger);Yes(daily.GetCrew(0).Thirst>thirst);Yes(dailyState.FoodMissing>debt);Eq(dailyState.SupplyUT,420);
        food.amount=5;water.amount=5;hunger=daily.GetCrew(0).Hunger;debt=dailyState.FoodMissing;Planetarium.UT=450;daily.Step(30);
        Yes(!dailyState.SupplyDepleted);Yes(daily.GetCrew(0).Hunger<hunger);Eq(dailyState.FoodMissing,debt); // finite meals recover gradually, history retained
        double priorUT=dailyState.SupplyUT;hunger=daily.GetCrew(0).Hunger;dailyState.SupplyError="Unknown history";Planetarium.UT=480;daily.Step(30);Eq(dailyState.SupplyUT,priorUT);Eq(daily.GetCrew(0).Hunger,hunger); // unknown intervals remain quarantined
        Console.WriteLine("PASS: production supply adapter transactions, locked stores, duplicate UT, freeze/thaw transitions, depletion, unknown intervals and API failure (KSP stubs; runtime still required).");
    }
}
