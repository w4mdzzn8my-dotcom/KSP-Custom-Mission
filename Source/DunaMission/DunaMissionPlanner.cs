using System;
using System.Collections.Generic;
using UnityEngine;
using MegaCrew.Navigation;
using NavVector = MegaCrew.Navigation.Vector3d;
namespace MegaCrew.DunaMission
{
    [KSPAddon(KSPAddon.Startup.Flight,false)]
    public sealed class DunaMissionPlanner : MonoBehaviour
    {
        const string Lock="MegaCrew.DunaPlanner";
        Rect window=new Rect(70,100,600,450);Vector2 scroll;bool visible,hover;
        DunaRouteSearch search;DunaSearchInputs inputs;IEnumerator<DunaRoutePlan> worker;
        Game game;Guid vesselId;string save="",status="Plan a Kerbin–Duna–Kerbin expedition from a circular Kerbin parking orbit.";
        bool Allowed {get{return HighLogic.LoadedSceneIsFlight&&HighLogic.CurrentGame!=null&&(HighLogic.SaveFolder??"").StartsWith("MegaCrew",StringComparison.OrdinalIgnoreCase);}}
        void Start(){Debug.Log("[MegaCrew.Duna] Read-only Duna planner 0.1 loaded; no mission execution or warp adapter.");}
        void Update()
        {
            if(!Allowed){Stop("Mission context changed; refresh the Duna plan.");visible=false;Release();return;}
            if(Input.GetKeyDown(KeyCode.D)&&(Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.RightControl))&&(Input.GetKey(KeyCode.LeftAlt)||Input.GetKey(KeyCode.RightAlt)))visible=!visible;
            if(!visible||!hover)Release();
            if(search!=null&&!ContextMatches()){Stop("Snapshot belongs to a different/unavailable vessel or game; refresh the plan.");return;}
            if(worker==null)return;
            if(!ContextMatches()||FlightDriver.Pause||TimeWarp.CurrentRateIndex!=0||TimeWarp.CurrentRate!=1)
            {Stop("Search cancelled after context/pause/warp change; refresh in stable 1x flight.");return;}
            try
            {
                // At most two bounded Lambert evaluations per frame. Nothing
                // blocks waiting for a successful route or writes game state.
                float start=Time.realtimeSinceStartup;
                for(int i=0;i<2;i++)
                {
                    if(!worker.MoveNext()){worker.Dispose();worker=null;status=search.Best==null?"No candidate found in this bounded short-way grid. Returns were tested for the eight lowest-cost outbound samples only; this does not prove no feasible trajectory exists.":"Candidate itinerary found. Patched-conic and finite-burn validation remain required.";break;}
                    if(Time.realtimeSinceStartup-start>.005f)break;
                }
            }
            catch(Exception e){Stop("Duna search stopped: "+e.Message);}
        }
        bool ContextMatches()
        {
            var v=FlightGlobals.ActiveVessel;
            return v!=null&&v.id==vesselId&&v.loaded&&!v.packed&&!v.isEVA&&save==HighLogic.SaveFolder&&object.ReferenceEquals(game,HighLogic.CurrentGame);
        }
        void Stop(string why){if(worker!=null){worker.Dispose();worker=null;}search=null;inputs=null;status=why;}
        void Release(){InputLockManager.RemoveControlLock(Lock);}
        void OnDisable(){Release();}
        void OnDestroy(){if(worker!=null)worker.Dispose();Release();}
        void OnApplicationFocus(bool focus){if(!focus){hover=false;Release();}}
        static NavVector V(global::Vector3d v){return new NavVector(v.x,v.y,v.z);}
        static PlanetState State(CelestialBody body,double ut)
        {
            // Both values stay in the SAME KSP orbital coordinate system,
            // as in the existing detached preview. No world/node frame mapping.
            return new PlanetState(V(body.orbit.getRelativePositionAtUT(ut)),V(body.orbit.getOrbitalVelocityAtUT(ut)));
        }
        static void Vehicle(Vessel v,out double wet,out double dry,out double thrust,out double isp)
        {
            wet=v.GetTotalMass()*1000;dry=wet;thrust=0;isp=0;string fuel=null;int count=0;
            foreach(var part in v.parts)
            {
                if(part.Modules.Contains("MultiModeEngine"))throw new InvalidOperationException("Select and qualify the propulsion mode before planning; mixed-mode hardware is not supported yet.");
                foreach(var engine in part.FindModulesImplementing<ModuleEngines>())
                {
                    if(engine.propellants.Count!=1)throw new InvalidOperationException("This first propulsion estimator supports single-propellant engines only; mixed-propellant/staged reserves need a dedicated model.");
                    string name=engine.propellants[0].name;double currentIsp=engine.atmosphereCurve.Evaluate(0);
                    if(fuel!=null&&(fuel!=name||Math.Abs(isp-currentIsp)>1e-4))throw new InvalidOperationException("Mixed engine/propellant configurations require a staged propulsion model.");
                    fuel=name;isp=currentIsp;thrust+=engine.maxThrust*1000*engine.thrustPercentage/100;count++;
                }
            }
            if(count==0||string.IsNullOrEmpty(fuel)||thrust<=0||isp<=0)throw new InvalidOperationException("A supported fueled propulsion system is required.");
            double propellant=0;
            foreach(var part in v.parts)foreach(PartResource r in part.Resources)
                if(r.resourceName==fuel&&r.flowState)
                {
                    if(!CampaignTiming.Finite(r.amount)||r.amount<0||r.info==null||!CampaignTiming.Finite(r.info.density)||r.info.density<=0)throw new InvalidOperationException("Invalid propellant inventory.");
                    propellant+=r.amount*r.info.density*1000;
                }
            if(propellant<=0||propellant>=wet)throw new InvalidOperationException("No usable finite propellant mass was found.");
            // Nominal vessel-wide inventory; connectivity/staging must later be
            // checked against stock consumption. Reserve is NEVER spent here.
            dry=wet-.85*propellant;
        }
        void Begin()
        {
            Stop("Preparing Duna search.");
            try
            {
                var v=FlightGlobals.ActiveVessel;
                if(v==null||!v.loaded||v.packed||v.isEVA||v.situation!=Vessel.Situations.ORBITING||FlightDriver.Pause||TimeWarp.CurrentRateIndex!=0||TimeWarp.CurrentRate!=1)
                    throw new InvalidOperationException("Use a loaded, unpaused 1x vessel in Kerbin orbit.");
                var kerbin=FlightGlobals.Bodies.Find(b=>b.bodyName=="Kerbin");var duna=FlightGlobals.Bodies.Find(b=>b.bodyName=="Duna");
                if(kerbin==null||duna==null||kerbin.orbit==null||duna.orbit==null||kerbin.orbit.referenceBody==null||kerbin.orbit.referenceBody!=duna.orbit.referenceBody)
                    throw new InvalidOperationException("Kerbin and Duna must orbit the same configured star.");
                if(v.mainBody!=kerbin||v.orbit==null||v.orbit.eccentricity>.01||v.orbit.PeA<=kerbin.atmosphereDepth+10000)
                    throw new InvalidOperationException("Start above Kerbin's atmosphere in a near-circular parking orbit (e ≤ 0.01).");
                foreach(var proto in HighLogic.CurrentGame.scenarios)
                {
                    var active=proto.moduleRef as HermesCampaign;
                    if(active!=null&&active.Started)throw new InvalidOperationException("This preview chooses a PRE-START epoch. An active campaign cannot be rebased; plan a separate Duna mission save.");
                }
                double wet,dry,thrust,isp;Vehicle(v,out wet,out dry,out thrust,out isp);
                inputs=new DunaSearchInputs(Planetarium.GetUniversalTime(),kerbin.orbit.referenceBody.gravParameter,kerbin.gravParameter,duna.gravParameter,
                    v.orbit.semiMajorAxis,duna.Radius+Math.Max(100000,duna.atmosphereDepth+50000),wet,dry,thrust,isp);
                game=HighLogic.CurrentGame;save=HighLogic.SaveFolder;vesselId=v.id;
                search=new DunaRouteSearch(inputs,(isDuna,ut)=>State(isDuna?duna:kerbin,ut));worker=search.Scan().GetEnumerator();
                status="Searching actual planetary ephemerides. Current vehicle performance is a nominal snapshot, not propulsion qualification.";
            }
            catch(Exception e){Stop(e.Message);}
        }
        void OnGUI()
        {
            if(!Allowed)return;
            var oldSkin=GUI.skin;bool oldEnabled=GUI.enabled;
            try
            {
                GUI.skin=HighLogic.Skin;GUI.enabled=true;
                if(GUI.Button(new Rect(20,220,190,30),"DUNA EXPEDITION"))visible=!visible;
                if(!visible){hover=false;Release();return;}
                window.width=Math.Min(600,Math.Max(220,Screen.width-30));window.height=Math.Min(450,Math.Max(220,Screen.height-60));
                window.x=Mathf.Clamp(window.x,0,Math.Max(0,Screen.width-window.width));window.y=Mathf.Clamp(window.y,0,Math.Max(0,Screen.height-window.height));
                var mouse=new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y);hover=window.Contains(mouse);
                if(hover)InputLockManager.SetControlLock(ControlTypes.ALL_SHIP_CONTROLS,Lock);else Release();
                window=GUI.Window(GetInstanceID(),window,Draw,"HERMES / DUNA EXPEDITION PLANNER");
            }
            finally{GUI.skin=oldSkin;GUI.enabled=oldEnabled;}
        }
        static string Day(double ut){return (ut/86400).ToString("F2")+" standard days (UT "+ut.ToString("F0")+")";}
        void Draw(int id)
        {
            float width=Math.Max(0,window.width-16),height=Math.Max(0,window.height-34);
            GUI.BeginGroup(new Rect(8,28,width,height));
            try
            {
                GUILayout.BeginArea(new Rect(0,0,width,height));
                try
                {
                    const float controlsHeight=30, gap=6;
                    float bodyHeight=Math.Max(0,height-controlsHeight-gap);
                    GUILayout.BeginHorizontal(GUILayout.Height(controlsHeight));
                    try
                    {
                        if(GUILayout.Button(worker==null?"Search round trip":"Restart search",GUILayout.Height(controlsHeight)))Begin();
                        if(worker!=null&&GUILayout.Button("Cancel search",GUILayout.Height(controlsHeight)))Stop("Search cancelled; no game state changed.");
                        if(GUILayout.Button("Close",GUILayout.Width(70),GUILayout.Height(controlsHeight))){visible=false;hover=false;Release();}
                    }
                    finally { GUILayout.EndHorizontal(); }
                    scroll=GUILayout.BeginScrollView(scroll,false,true,GUILayout.Width(width),GUILayout.Height(bodyHeight));
                    try
                    {
                        GUILayout.Label(status);
                        GUILayout.Label("24 mission months / six four-month watches. Short-way heliocentric estimates; no nodes, burns, saves, crew changes or warp are executed.");
                        if(search!=null)
                        {
                            GUILayout.Label("Search results use the captured input snapshot at UT "+inputs.NowUT.ToString("F0")+"; inputs are not live-updated during this search.");
                            GUILayout.Label("Scanned: "+search.Attempts+" | solved legs: "+search.SolvedLegs+" | fuel/watch candidates: "+search.FeasiblePlans);
                            GUILayout.Label("Input snapshot at UT "+inputs.NowUT.ToString("F0")+": wet "+(inputs.WetKg/1000).ToString("F2")+" t | retained mass "+(inputs.DryKg/1000).ToString("F2")+" t | nominal vacuum "+inputs.IspSeconds.ToString("F0")+" s / "+(inputs.ThrustN/1000).ToString("F1")+" kN. 15% propellant held for corrections/reserve; flow connectivity, staging and landed mass not yet validated.");
                            var plan=search.Best;
                            if(plan!=null)
                            {
                                GUILayout.Label("LOWEST COST FOUND IN THIS GRID: "+plan.TotalDeltaV.ToString("F0")+" m/s for four propulsive parking-orbit transfers. No aerocapture assumed.");
                                foreach(var burn in plan.Burns)GUILayout.Label(burn.Name+": "+Day(burn.EncounterUT)+" | "+burn.DeltaV.ToString("F0")+" m/s | nominal "+(burn.DurationSeconds/60).ToString("F1")+" min");
                                GUILayout.Label("Pre-start campaign epoch ranges (do not rewrite an active epoch):");
                                foreach(var e in plan.EpochWindows)GUILayout.Label(Day(e.EarliestUT)+" → "+Day(e.LatestUT));
                                GUILayout.Label("Full provisional duty envelopes include burn duration, one-hour alignment/review, and a two-day encounter timing reserve. Encounter UT is NOT a fitted ignition epoch. Refit departure/SOI/capture geometry, actual corrections and every full burn against stock patched conics before any approval.");
                                GUILayout.Label("Coast-only play baseline 16h03m; nominal four-burn time adds "+(plan.EstimatedExtraPlaySeconds/60).ToString("F1")+" minutes, plus alignment, handovers, lander work and maintenance. No physical trajectory or fuel use has happened.");
                            }
                        }
                        GUILayout.Label("MISSION SEQUENCE: commission and provision in Kerbin orbit → dual-check departure → outbound maintenance/crew watches → Duna capture → middle human watch: surface/science work with a qualified atmospheric lander → third human watch: return departure → Kerbin orbital capture and crew recovery. The last NPC watch remains Kerbin-orbit upkeep, then all crew wake for recovery. Surface/ascent, stationkeeping and post-campaign recovery burns require separate budgets and watch coverage; the vacuum Finch alone is not a Duna atmospheric lander.");
                        GUILayout.Label("Exactly one planned emergency remains undisclosed. Stories/tasks are not implemented by this itinerary; no extra emergencies are generated. High-rate cryosleep and two authenticated player approvals remain qualification gates.");
                    }
                    finally { GUILayout.EndScrollView(); }
                }
                finally { GUILayout.EndArea(); }
            }
            finally { GUI.EndGroup(); }
            GUI.DragWindow(new Rect(0,0,window.width,24));
        }
    }
}
