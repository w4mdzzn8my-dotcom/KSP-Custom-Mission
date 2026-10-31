using System;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;

namespace MegaCrew
{
    // Added only on explicit campaign start in a MegaCrew flight save. KSP owns
    // serialization, so quicksave/load restores epoch and vessel together.
    public sealed class HermesCampaign : ScenarioModule
    {
        public List<CrewRecord> CrewSnapshot;
        public bool Started;
        public double EpochUT;
        public Guid VesselId;
        public string LoadError="";
        public bool RationsEnabled, DailyRoutineEnabled, SupplyDepleted;
        public int UnfrozenMask=15;
        public double SupplyUT, FoodUsed, WaterUsed, FoodMissing, WaterMissing;
        public int PreviousUnfrozen;
        public string SupplyError="";
        public double LastTakenFood,LastTakenWater; // transient transaction results
        ConfigNode preservedLoadNode;
        void RejectLoad(ConfigNode node,string reason)
        {Started=false;LoadError=reason;preservedLoadNode=node.CreateCopy();}
        static bool ReadAmount(ConfigNode n,string key,out double value)
        {return double.TryParse(n.GetValue(key),NumberStyles.Float,CultureInfo.InvariantCulture,out value)&&CampaignTiming.Finite(value)&&value>=0;}
        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            // Keep invalid/future data intact. A later save must not normalize
            // rejected flags or discard unknown fields and repeated crew nodes.
            if(preservedLoadNode!=null){node.ClearData();preservedLoadNode.CopyTo(node);return;}
            node.AddValue("schema",1);node.AddValue("started",Started);
            node.AddValue("epochUT",EpochUT.ToString("R",CultureInfo.InvariantCulture));
            node.AddValue("vesselID",VesselId.ToString());
            node.AddValue("dailyRoutineEnabled",DailyRoutineEnabled);node.AddValue("unfrozenMask",UnfrozenMask);
            node.AddValue("supplyDepleted",SupplyDepleted);
            node.AddValue("rationsEnabled",RationsEnabled);node.AddValue("supplyError",SupplyError);
            node.AddValue("supplyUT",SupplyUT.ToString("R",CultureInfo.InvariantCulture));
            node.AddValue("foodUsed",FoodUsed.ToString("R",CultureInfo.InvariantCulture));
            node.AddValue("waterUsed",WaterUsed.ToString("R",CultureInfo.InvariantCulture));
            node.AddValue("foodMissing",FoodMissing.ToString("R",CultureInfo.InvariantCulture));
            node.AddValue("waterMissing",WaterMissing.ToString("R",CultureInfo.InvariantCulture));
            node.AddValue("previousUnfrozen",PreviousUnfrozen);
            if(CrewSnapshot!=null)CrewStateCodec.Save(node,CrewSnapshot);
        }
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            CrewSnapshot=null;Started=false;LoadError="";RationsEnabled=false;DailyRoutineEnabled=false;
            UnfrozenMask=15;SupplyError="";SupplyDepleted=false;preservedLoadNode=null;
            EpochUT=0;VesselId=Guid.Empty;SupplyUT=0;FoodUsed=0;WaterUsed=0;FoodMissing=0;WaterMissing=0;
            PreviousUnfrozen=0;LastTakenFood=0;LastTakenWater=0;
            bool requested;
            if(!node.HasValue("started"))
            {
                if(node.HasValue("schema")||node.HasValue("epochUT")||node.HasValue("rationsEnabled")||node.HasValue("dailyRoutineEnabled")||node.HasValue("supplyUT")||node.HasNode("CAMPAIGN_CREW"))
                    RejectLoad(node,"Missing campaign start flag; original data preserved.");
                return;
            }
            if(!bool.TryParse(node.GetValue("started"),out requested))
            {RejectLoad(node,"Invalid campaign start flag; original data preserved.");return;}
            int schema;
            if(!int.TryParse(node.GetValue("schema"),out schema)||schema!=1)
            {RejectLoad(node,"Unsupported campaign schema; original data preserved.");return;}
            if(!requested)return;
            double epoch;Guid vessel;
            if(!double.TryParse(node.GetValue("epochUT"),NumberStyles.Float,CultureInfo.InvariantCulture,out epoch)||
                !CampaignTiming.Finite(epoch)||epoch<0||!Guid.TryParse(node.GetValue("vesselID"),out vessel)||vessel==Guid.Empty)
            {RejectLoad(node,"Invalid campaign save data; original data preserved.");return;}
            EpochUT=epoch;VesselId=vessel;Started=true;
            if(node.HasNode("CAMPAIGN_CREW"))
            {
                try{CrewSnapshot=CrewStateCodec.Load(node);}
                catch(FormatException){RejectLoad(node,"Invalid campaign crew snapshot; original data preserved.");return;}
            }
            if(node.HasValue("rationsEnabled"))
            {
                if(!bool.TryParse(node.GetValue("rationsEnabled"),out RationsEnabled))
                {RejectLoad(node,"Invalid ration plan flag; original data preserved.");return;}
            }
            else if(node.HasValue("supplyUT")||node.HasValue("foodUsed")||node.HasValue("waterUsed"))
            {RejectLoad(node,"Missing ration plan flag for saved ledger; original data preserved.");return;}
            if(node.HasValue("dailyRoutineEnabled")&&!bool.TryParse(node.GetValue("dailyRoutineEnabled"),out DailyRoutineEnabled))
            {RejectLoad(node,"Invalid daily routine flag; original data preserved.");return;}
            if(DailyRoutineEnabled&&!RationsEnabled)
            {RejectLoad(node,"Daily routine requires its finite ration plan; original data preserved.");return;}
            if(RationsEnabled)
            {
                if(node.HasValue("unfrozenMask")&&(!int.TryParse(node.GetValue("unfrozenMask"),out UnfrozenMask)||UnfrozenMask<0||UnfrozenMask>15))
                {RejectLoad(node,"Invalid saved crew history; original data preserved.");return;}
                SupplyError=node.GetValue("supplyError")??"";
                // Old candidates used depletion as a permanent error. Preserve
                // their recorded shortfall, but resume only observed intervals.
                if(SupplyError=="Routine rations depleted. Unmet supply demand recorded; coast blocked.")
                {SupplyError="";SupplyDepleted=true;}
                if(node.HasValue("supplyDepleted")&&!bool.TryParse(node.GetValue("supplyDepleted"),out SupplyDepleted))
                {RejectLoad(node,"Invalid saved depletion state; original data preserved.");return;}
                if(!ReadAmount(node,"supplyUT",out SupplyUT)||SupplyUT<EpochUT||
                   !ReadAmount(node,"foodUsed",out FoodUsed)||!ReadAmount(node,"waterUsed",out WaterUsed)||
                   !ReadAmount(node,"foodMissing",out FoodMissing)||!ReadAmount(node,"waterMissing",out WaterMissing)||
                   !int.TryParse(node.GetValue("previousUnfrozen"),out PreviousUnfrozen)||PreviousUnfrozen<0||PreviousUnfrozen>4)
                {RejectLoad(node,"Invalid saved supply ledger; original data preserved.");return;}
            }
        }
    }
    public sealed partial class MissionConsole
    {
        HermesCampaign Campaign
        {
            get
            {
                if(!Allowed)return null;
                foreach(var proto in HighLogic.CurrentGame.scenarios)
                    if(proto.moduleName==typeof(HermesCampaign).Name)return proto.moduleRef as HermesCampaign;
                return null;
            }
        }
        void BindCampaignCrew()
        {
            var campaign=Campaign;if(campaign==null||!campaign.Started)return;
            if(campaign.CrewSnapshot==null)campaign.CrewSnapshot=crew;
            else if(!object.ReferenceEquals(crew,campaign.CrewSnapshot)){careAction="";crew=campaign.CrewSnapshot;}
        }
        void BeginCampaign()
        {
            if(!Allowed||!HighLogic.LoadedSceneIsFlight||Ship==null)return;
            var existing=Campaign;
            if(existing!=null&&(existing.Started||existing.LoadError.Length>0))return;
            if(!Aboard(crew[0].Name)||!Aboard(crew[1].Name)||Frozen(crew[0].Name)||Frozen(crew[1].Name))
            {message="Both Hamish and Basil must be awake aboard Hermes to start.";return;}
            foreach(var c in crew)if(!Aboard(c.Name)&&!Frozen(c.Name)){message="All four mission crew must be aboard Hermes, awake or in its pods.";return;}
            double now=Planetarium.GetUniversalTime();
            if(!CampaignTiming.Finite(now)||now<0)return;
            var campaign=existing;
            if(campaign==null){
                var proto=HighLogic.CurrentGame.AddProtoScenarioModule(typeof(HermesCampaign),GameScenes.FLIGHT,GameScenes.SPACECENTER,GameScenes.TRACKSTATION);
                campaign=proto.moduleRef as HermesCampaign;
            }
            if(campaign==null){message="Campaign module is not ready. No clock was started.";return;}
            campaign.EpochUT=now;campaign.VesselId=Ship.id;campaign.CrewSnapshot=crew;campaign.Started=true;
            message="Campaign epoch recorded in this game's scenario state. Save the game to persist it. Accelerated coast is not enabled yet.";
        }
        void DrawCampaign()
        {
            GUILayout.Label("HERMES EXPEDITION / 24 MISSION MONTHS");
            GUILayout.Label("Three rotations: four months Hamish/Basil, then four months Ada/Vikram. Mission months are 30 standard 24-hour days. Actual KSP UT determines progress, including time spent burning.");
            var campaign=Campaign;
            if(campaign==null||(!campaign.Started&&campaign.LoadError.Length==0))
            {
                GUI.enabled=Allowed&&HighLogic.LoadedSceneIsFlight&&Ship!=null;
                if(GUILayout.Button("Start campaign on this vessel"))BeginCampaign();GUI.enabled=true;
                GUILayout.Label("Start once all four crew are aboard. This starts the campaign clock in the current game's scenario state; normal KSP saves/quicksaves preserve it.");return;
            }
            if(!campaign.Started){GUILayout.Label(campaign.LoadError.Length>0?campaign.LoadError:"Campaign state has not been initialized.");return;}
            double now=Planetarium.GetUniversalTime();
            if(!CampaignTiming.Finite(now)||now<campaign.EpochUT){GUILayout.Label("Campaign clock is inconsistent with saved epoch. Coast is disabled.");return;}
            DrawCampaignSupplies(campaign);
            int watch=CampaignTiming.WatchAt(campaign.EpochUT,now);
            double months=Math.Min(24,(now-campaign.EpochUT)/CampaignTiming.MonthSeconds);
            GUILayout.Label("Elapsed: "+months.ToString("F3")+" / 24 mission months");
            if(watch==6){GUILayout.Label("Expedition clock complete. Perform crew recovery and mission review.");return;}
            bool human=CampaignTiming.HumanWatch(watch);
            GUILayout.Label("Watch "+(watch+1)+" / 6: "+(human?"Hamish + Basil":"Ada + Vikram")+" | Remaining mission days: "+((CampaignTiming.WatchEndUT(campaign.EpochUT,watch)-now)/86400).ToString("F2"));
            bool bound=HighLogic.LoadedSceneIsFlight&&Ship!=null&&Ship.id==campaign.VesselId;
            GUILayout.Label(bound?"Campaign vessel active":"Activate the original campaign vessel; vessel changes never silently rebind the campaign.");
            GUILayout.Label("Coast BLOCKED: background needs, cryopod power and resource accounting are not yet certified. Current build retains 1x. Targets after qualification: human 540x, NPC 172800x. Minimum coast-only play time 16h3m; burns and handovers add time.");
            GUILayout.Label("Burn plan and dual verification integration pending. Single-player role selection is rehearsal, not two authenticated player approvals.");
        }
    }
}
