using System;
namespace MegaCrew
{
    public sealed partial class MissionConsole
    {
        static MissionConsole dutyConsole;
        // Local normal-duty query only. Caller must independently enforce role,
        // room/station and operation rules. This is not multiplayer authority or
        // burn approval, and must not gate stock emergency flight controls.
        public static bool CanPerformDuty(string crewName,Guid vesselId,out string reason)
        {
            try {
                var console=dutyConsole;
                if(console==null||!console.isActiveAndEnabled)
                {reason="Crew status console is unavailable.";return false;}
                return console.CheckCrewDuty(crewName,vesselId,out reason);
            }catch(Exception){reason="Crew status is unavailable; normal duty is on hold.";return false;}
        }
        bool CheckCrewDuty(string crewName,Guid vesselId,out string reason)
        {
            reason="";
            if(!Allowed||!HighLogic.LoadedSceneIsFlight||!FlightGlobals.ready||FlightDriver.Pause||TimeWarp.CurrentRateIndex!=0||TimeWarp.CurrentRate!=1)
            {reason="Normal duty requires an active, unpaused flight at 1x.";return false;}
            var ship=Ship;
            if(ship==null||ship.id!=vesselId||!ship.loaded||ship.packed||ship.isEVA||loadedSave!=HighLogic.SaveFolder||!object.ReferenceEquals(loadedGame,HighLogic.CurrentGame))
            {reason="Return to the loaded mission vessel and wait for crew status.";return false;}
            int campaignModules=0;
            foreach(var proto in HighLogic.CurrentGame.scenarios)
                if(proto.moduleName==typeof(HermesCampaign).Name)
                {
                    campaignModules++;
                    if(!(proto.moduleRef is HermesCampaign))
                    {reason="Campaign state is still loading.";return false;}
                }
            if(campaignModules>1){reason="Campaign state is ambiguous; normal duty is unavailable.";return false;}
            var campaign=Campaign;
            if(campaign!=null)
            {
                if(campaign.LoadError.Length>0||campaign.SupplyError.Length>0)
                {reason="Campaign state needs reconciliation before normal duty.";return false;}
                if(campaign.Started&&(campaign.VesselId!=vesselId||campaign.CrewSnapshot==null||!object.ReferenceEquals(crew,campaign.CrewSnapshot)))
                {reason="Campaign crew snapshot is not bound to this vessel yet.";return false;}
            }
            var person=crew.Find(c=>c.Name==crewName);
            if(person==null){reason="This crew member is not in the mission roster.";return false;}
            bool frozen;
            if(!CryoBridge.TryFrozen(crewName,out frozen))
            {reason="The cryopod crew manifest is still unverified.";return false;}
            if(frozen||!Aboard(crewName))
            {reason="This crew member must be awake aboard the mission vessel.";return false;}
            if(!NeedsModel.Fit(person))
            {reason="This crew member needs rest, food, water, care or injury recovery.";return false;}
            return true;
        }
    }
}
