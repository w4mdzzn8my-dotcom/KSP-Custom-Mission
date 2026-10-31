using System;
namespace MegaCrew
{
    // Existing fictional five-minute treatment recovery uses observed KSP time.
    // Practice watch controls do not pause biology; frozen crew do not heal.
    public static class MedicalRecovery
    {
        public static bool Advance(CrewRecord c,double seconds,bool frozen)
        {
            if(c==null||!CampaignTiming.Finite(seconds)||seconds<0)throw new ArgumentOutOfRangeException();
            if(frozen||!c.Treated||c.Injury=="None"||seconds==0)return false;
            if(!CampaignTiming.Finite(c.Recovery)||c.Recovery<0)return false;
            c.Recovery=Math.Max(0,c.Recovery-seconds);
            if(c.Recovery>0)return false;
            c.Injury="None";c.Treated=false;return true;
        }
    }
    public sealed partial class MissionConsole
    {
        void UpdateRecovery(double dt)
        {
            if(!Allowed||!HighLogic.LoadedSceneIsFlight||!FlightGlobals.ready||FlightDriver.Pause||
                TimeWarp.CurrentRateIndex!=0||TimeWarp.CurrentRate!=1||!CampaignTiming.Finite(dt)||dt<=0||dt>5)return;
            var ship=Ship;
            if(ship==null||!ship.loaded||ship.packed||ship.isEVA||loadedSave!=HighLogic.SaveFolder||
                !object.ReferenceEquals(loadedGame,HighLogic.CurrentGame)||CryoBridge.Busy)return;
            if(HighLogic.CurrentGame==null||HighLogic.CurrentGame.scenarios==null)return;
            int modules=0;
            foreach(var proto in HighLogic.CurrentGame.scenarios)
                if(proto.moduleName==typeof(HermesCampaign).Name)
                {if(!(proto.moduleRef is HermesCampaign)||++modules>1)return;}
            var campaign=Campaign;
            if(campaign!=null&&(campaign.LoadError.Length>0||campaign.SupplyError.Length>0||
                (campaign.Started&&(campaign.VesselId!=ship.id||!object.ReferenceEquals(crew,campaign.CrewSnapshot)))))return;
            foreach(var person in crew)
            {
                bool frozen;
                if(!CryoBridge.TryFrozen(person.Name,out frozen)||!Aboard(person.Name))continue;
                if(MedicalRecovery.Advance(person,dt,frozen))message=person.Name+" completed simulated injury recovery.";
            }
        }
    }
}
