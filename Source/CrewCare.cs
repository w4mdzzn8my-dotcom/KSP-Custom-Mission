using System;
using System.Globalization;
using UnityEngine;
namespace MegaCrew
{
    public sealed partial class MissionConsole
    {
        string careAction="", careCrew="",carePartner="";
        uint careRoom;
        double careElapsed;
        static void SaveCare(ConfigNode x,CrewRecord c)
        {
            x.AddValue("hygiene",c.Hygiene.ToString("R",CultureInfo.InvariantCulture));
            x.AddValue("conditioning",c.Conditioning.ToString("R",CultureInfo.InvariantCulture));
            x.AddValue("morale",c.Morale.ToString("R",CultureInfo.InvariantCulture));
            x.AddValue("hygieneDoneUT",c.HygieneDoneUT.ToString("R",CultureInfo.InvariantCulture));
            x.AddValue("exerciseDoneUT",c.ExerciseDoneUT.ToString("R",CultureInfo.InvariantCulture));
            x.AddValue("socialDoneUT",c.SocialDoneUT.ToString("R",CultureInfo.InvariantCulture));
        }
        static double CareValue(ConfigNode n,string key,double fallback,bool timestamp=false)
        {
            double v;if(!double.TryParse(n.GetValue(key),NumberStyles.Float,CultureInfo.InvariantCulture,out v)||double.IsNaN(v)||double.IsInfinity(v))return fallback;
            return timestamp?Math.Max(-1,v):NeedsModel.Clamp(v);
        }
        static void LoadCare(ConfigNode x,CrewRecord c)
        {
            c.Hygiene=CareValue(x,"hygiene",100);c.Conditioning=CareValue(x,"conditioning",100);c.Morale=CareValue(x,"morale",100);
            c.HygieneDoneUT=CareValue(x,"hygieneDoneUT",-1,true);c.ExerciseDoneUT=CareValue(x,"exerciseDoneUT",-1,true);c.SocialDoneUT=CareValue(x,"socialDoneUT",-1,true);
        }
        static double CareDuration(string action){return action=="Exercise"?120:action=="Hygiene"?45:60;}
        void BeginCare(CrewRecord c,Part room,string action)
        {
            if(careAction.Length>0||!NeedsModel.CareReady(c)||Frozen(c.Name)||!Aboard(c.Name))return;
            double done=action=="Exercise"?c.ExerciseDoneUT:action=="Hygiene"?c.HygieneDoneUT:c.SocialDoneUT;
            double now=Planetarium.GetUniversalTime();
            if(done>=0&&now-done<21600){message="This care activity was recently completed. Return after six mission hours.";return;}
            carePartner="";
            if(action=="Social")
            {
                var partner=crew.Find(p=>p!=c&&Aboard(p.Name)&&!Frozen(p.Name)&&!p.Resting);
                if(partner==null){message="Wake an available second crew member for the check-in.";return;}
                carePartner=partner.Name;
            }
            careAction=action;careCrew=c.Name;careRoom=room.flightID;careElapsed=0;
            message=action+" started. Remain in this compartment as this Kerbal. Leaving, pausing or starting warp cancels it without reward.";
        }
        void UpdateCare(double dt)
        {
            if(careAction.Length==0)return;
            var c=InteriorCrew;var room=CurrentRoom;
            var partner=crew.Find(p=>p.Name==carePartner);
            bool valid=HighLogic.LoadedSceneIsFlight&&!FlightDriver.Pause&&TimeWarp.CurrentRateIndex==0&&c!=null&&c.Name==careCrew&&room!=null&&room.flightID==careRoom&&NeedsModel.CareReady(c)&&!Frozen(c.Name)&&Aboard(c.Name);
            if(careAction=="Social")valid=valid&&partner!=null&&Aboard(partner.Name)&&!Frozen(partner.Name)&&!partner.Resting;
            if(!valid||dt<0||dt>5){careAction="";message="Care activity cancelled; no supplies or benefit awarded.";return;}
            careElapsed+=dt;
            if(careElapsed<CareDuration(careAction))return;
            double now=Planetarium.GetUniversalTime();
            if(careAction=="Hygiene")
            {
                if(!UseSupply("Water",0.25)){careAction="";return;}
                c.Hygiene=NeedsModel.Clamp(c.Hygiene+40);c.HygieneDoneUT=now;
            }
            else if(careAction=="Exercise"){c.Conditioning=NeedsModel.Clamp(c.Conditioning+30);c.Fatigue=NeedsModel.Clamp(c.Fatigue+5);c.ExerciseDoneUT=now;}
            else {c.Morale=NeedsModel.Clamp(c.Morale+25);c.SocialDoneUT=now;partner.Morale=NeedsModel.Clamp(partner.Morale+15);}
            message=careAction+" completed by "+c.Name+(carePartner.Length>0?" with "+carePartner:"")+". No walking or interaction animation is simulated.";
            careAction="";Save();
        }
        void DrawCare(CrewRecord c,Part room)
        {
            GUILayout.Label("DAILY CREW CARE | Hygiene "+c.Hygiene.ToString("F0")+" | Conditioning "+c.Conditioning.ToString("F0")+" | Morale "+c.Morale.ToString("F0"));
            if(careAction.Length>0)
            {
                GUILayout.Label(careAction+" / "+careCrew+": "+careElapsed.ToString("F0")+" / "+CareDuration(careAction).ToString("F0")+" seconds");
                if(GUILayout.Button("Cancel care activity")){careAction="";message="Care activity cancelled.";}return;
            }
            GUI.enabled=NeedsModel.CareReady(c)&&!Frozen(c.Name)&&!FlightDriver.Pause&&TimeWarp.CurrentRateIndex==0;
            if(GUILayout.Button("Hygiene routine / 45 seconds / 0.25 Water"))BeginCare(c,room,"Hygiene");
            if(GUILayout.Button("Exercise routine / 2 minutes"))BeginCare(c,room,"Exercise");
            if(GUILayout.Button("Social check-in / 1 minute / second awake crew"))BeginCare(c,room,"Social");
            GUI.enabled=true;
            GUILayout.Label("Game abstractions: care meters decay per unfrozen mission day; activities require this actual room and viewed Kerbal. Partner availability is roster-based, not an animated meeting. Each activity has a six-mission-hour cooldown.");
        }
    }
}
