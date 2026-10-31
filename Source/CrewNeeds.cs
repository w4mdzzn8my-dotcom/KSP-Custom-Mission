using System;
using UnityEngine;

namespace MegaCrew
{
    // Resources are serialized by KSP; the one-time flag prevents replenishment on reload.
    public sealed class MegaCrewPantry : PartModule
    {
        [KSPField(isPersistant=true)] public bool provisioned;
        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if(!HighLogic.LoadedSceneIsFlight||HighLogic.CurrentGame==null||!(HighLogic.SaveFolder??"").StartsWith("MegaCrew",StringComparison.OrdinalIgnoreCase)||provisioned)return;
            foreach(string resource in new[]{"Food","Water"})
            {
                if(part.Resources.Contains(resource))continue;
                if(PartResourceLibrary.Instance.GetDefinition(resource)==null){Debug.LogError("[MegaCrew] Missing CRP resource: "+resource);return;}
                var n=new ConfigNode("RESOURCE");n.AddValue("name",resource);n.AddValue("amount",resource=="Food"?84:42);n.AddValue("maxAmount",resource=="Food"?84:42);part.AddResource(n);
            }
            provisioned=true;
        }
    }

    public static class NeedsModel
    {
        public static double Clamp(double n){return Math.Max(0,Math.Min(100,n));}
        public static void Tick(CrewRecord c,double seconds,bool frozen)
        {
            if(c==null||double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<0)throw new ArgumentOutOfRangeException("seconds");
            if(frozen){c.Resting=false;c.Fatigue=Clamp(c.Fatigue-seconds*100/7200);return;}
            c.Hygiene=Clamp(c.Hygiene-seconds*25/86400);
            c.Conditioning=Clamp(c.Conditioning-seconds*15/86400);
            c.Morale=Clamp(c.Morale-seconds*10/86400);
            c.Hunger=Clamp(c.Hunger+seconds*100/21600);
            c.Thirst=Clamp(c.Thirst+seconds*100/14400);
            c.Fatigue=Clamp(c.Fatigue+seconds*(c.Resting?-100.0/7200:100.0/14400));
        }
        public static bool CareReady(CrewRecord c){return c!=null&&!c.Resting&&c.Hunger<90&&c.Thirst<90&&c.Fatigue<90;}
        public static bool Fit(CrewRecord c){return CareReady(c)&&c.Injury=="None"&&c.Hygiene>10&&c.Conditioning>10&&c.Morale>10;}
    }

    public sealed partial class MissionConsole
    {
        float nextNeedsSave;
        void UpdateNeeds(double dt)
        {
            var campaign=Campaign;if(campaign!=null&&campaign.Started&&campaign.DailyRoutineEnabled)return;
            if(!HighLogic.LoadedSceneIsFlight||Ship==null||dt<=0||dt>5||TimeWarp.CurrentRateIndex!=0||FlightDriver.Pause)return;
            foreach(var c in crew)if(Aboard(c.Name)||Frozen(c.Name))NeedsModel.Tick(c,dt,Frozen(c.Name));
            if(Time.realtimeSinceStartup>nextNeedsSave){Save();nextNeedsSave=Time.realtimeSinceStartup+15;}
        }
        void SaveNeeds(ConfigNode n)
        {
            foreach(var c in crew){var x=n.AddNode("NEEDS");x.AddValue("name",c.Name);x.AddValue("hunger",c.Hunger);x.AddValue("thirst",c.Thirst);x.AddValue("resting",c.Resting);SaveCare(x,c);}
        }
        void LoadNeeds(ConfigNode n)
        {
            foreach(var x in n.GetNodes("NEEDS")){var c=crew.Find(z=>z.Name==x.GetValue("name"));if(c==null)continue;double h,t;double.TryParse(x.GetValue("hunger"),out h);double.TryParse(x.GetValue("thirst"),out t);c.Hunger=NeedsModel.Clamp(h);c.Thirst=NeedsModel.Clamp(t);bool.TryParse(x.GetValue("resting"),out c.Resting);LoadCare(x,c);}
        }
        bool UseSupply(string name,double amount)
        {
            if(Ship==null)return false;
            double available=0;foreach(var p in Ship.parts)foreach(PartResource r in p.Resources)if(r.resourceName==name&&r.flowState)available+=r.amount;
            if(available+1e-8<amount){message="Insufficient accessible "+name+". No supply was consumed.";return false;}
            // Explicit vessel-wide pantry access; do not silently borrow from another vessel.
            double remaining=amount;foreach(var p in Ship.parts)foreach(PartResource r in p.Resources)if(r.resourceName==name&&r.flowState){double take=Math.Min(remaining,r.amount);r.amount-=take;remaining-=take;if(remaining<=1e-8)return true;}
            return false;
        }
        void Eat(CrewRecord c){if(c==null||Frozen(c.Name)||c.Resting||!Aboard(c.Name))return;if(UseSupply("Food",0.5)){c.Hunger=NeedsModel.Clamp(c.Hunger-40);message=c.Name+" ate one ration (0.5 Food units).";Save();}}
        void Drink(CrewRecord c){if(c==null||Frozen(c.Name)||c.Resting||!Aboard(c.Name))return;if(UseSupply("Water",0.25)){c.Thirst=NeedsModel.Clamp(c.Thirst-40);message=c.Name+" drank 0.25 Water units.";Save();}}
        void Rest(CrewRecord c)
        {
            if(c==null||!Aboard(c.Name)||Frozen(c.Name))return;
            if(!c.Resting){int awake=0;foreach(var other in crew)if(other!=c&&Aboard(other.Name)&&!other.Resting&&!Frozen(other.Name))awake++;if(awake==0){message="Keep another crew member awake on watch.";return;}}
            c.Resting=!c.Resting;message=c.Name+(c.Resting?" is resting in crew quarters. Time remains 1x.":" returned to duty.");Save();
        }
        void DrawNeeds()
        {
            GUILayout.Label("CREW NEEDS / 1x TIME");
            GUILayout.Label("Quarters also offer timed hygiene, exercise and a paired social check-in. These are gameplay activities; character animations are not implemented.");
            GUILayout.Label("Walk to the quarters/galley and press I to eat, drink or rest. Meters advance during loaded flight, even with watch tracking paused. No offline catch-up or instant sleep recovery.");
            foreach(var c in crew)GUILayout.Label(c.Name+" | Hunger "+c.Hunger.ToString("F0")+"% | Thirst "+c.Thirst.ToString("F0")+"% | Fatigue "+c.Fatigue.ToString("F0")+"% | "+(Frozen(c.Name)?"FROZEN":c.Resting?"RESTING":NeedsModel.Fit(c)?"FIT":"NEEDS ATTENTION"));
            foreach(var c in crew)GUILayout.Label(c.Name+" | Hygiene "+c.Hygiene.ToString("F0")+" | Conditioning "+c.Conditioning.ToString("F0")+" | Morale "+c.Morale.ToString("F0"));
            if(Ship!=null)foreach(string name in new[]{"Food","Water"}){double a=0,b=0;foreach(var p in Ship.parts)foreach(PartResource r in p.Resources)if(r.resourceName==name){a+=r.amount;b+=r.maxAmount;}GUILayout.Label(name+": "+a.ToString("F2")+" / "+b.ToString("F2"));}
            GUILayout.Label("Game tuning: empty-to-urgent hunger in 6 h, thirst/fatigue in 4 h; full fatigue recovery in 2 h of ordinary rest. Frozen crew pause food/water needs. Hunger/thirst/fatigue at 90%, or hygiene/conditioning/morale at 10% or below, or a recorded injury block custom engineering/science work until addressed. Daily care remains available when basic food/water/sleep needs are met. Stock flight controls and emergency treatment remain available. No deaths are simulated. These are fictional Kerbal gameplay rates.");
        }
    }
}
