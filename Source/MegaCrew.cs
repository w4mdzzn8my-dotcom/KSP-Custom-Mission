using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace MegaCrew
{
    public static class Science
    {
        public const double G0 = 9.80665;
        public static double SpinG(double radius, double rpm) { double w = rpm * Math.PI / 30; return w*w*radius/G0; }
        public static double Rpm(double radius, double g) { return Math.Sqrt(g*G0/radius)*30/Math.PI; }
        public static double DeltaV(double isp, double wet, double dry) { return isp*G0*Math.Log(wet/dry); }
    }

    public sealed class CrewRecord
    {
        public string Name, Role, Injury = "None";
        public double Fatigue, Recovery, Hunger, Thirst;
        public double Hygiene=100, Conditioning=100, Morale=100;
        public double HygieneDoneUT=-1, ExerciseDoneUT=-1, SocialDoneUT=-1;
        public bool Resting;
        public bool Treated;
        public CrewRecord(string name, string role) { Name=name; Role=role; }
    }

    // Deliberately single-player. Saves mission state inside the current KSP save only.
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public sealed partial class MissionConsole : MonoBehaviour
    {
        Rect window = new Rect(30, 65, 720, 520);
        Vector2 scroll;
        GUISkin statusStyleSkin;
        GUIStyle statusLabelStyle;
        bool visible, running, drillActive;
        Queue<string> wakeQueue = new Queue<string>();
        float nextWakePoll;
        string loadedSave = "", message = "Create a new Sandbox named MegaCrew. F8 opens this console.";
        Game loadedGame;
        int tab, phase;
        double lastUt, shiftElapsed, nextSave, alarmTime;
        string pilot = "Hamish Kerman";
        string radius = "15", rpm = "4.76", isp = "800", wet = "300", dry = "150";
        HashSet<string> checks = new HashSet<string>();
        List<CrewRecord> crew = NewCrew();
        static List<CrewRecord> NewCrew(){return new List<CrewRecord> {
            new CrewRecord("Hamish Kerman", "Commander / navigation"),
            new CrewRecord("Basil Kerman", "Flight engineer"),
            new CrewRecord("Ada Kerman", "NPC physician / relief commander"),
            new CrewRecord("Vikram Kerman", "NPC systems engineer")
        };}
        readonly string[] tabs = {"Mission", "Crew & cryosleep", "Engineering", "Emergency", "Practice", "Crew needs", "Campaign", "Setup"};
        readonly string[] titles = {"01 / Design and assemble", "02 / Launch and rendezvous", "03 / Commission Hermes", "04 / Cryogenic handover", "05 / Emergency exercise", "06 / Orbital science", "07 / Prepare Duna expedition", "08 / Return and recovery"};
        readonly string[][] tasks = {
            new[]{"Design a Hermes-inspired long spine, rotating habitat, command deck and cryobay.","Fit four crew berths, four cryopod spaces, reserve batteries and an independent control core.","Choose the radius from the installed habitat's geometry; calculate rpm and gravity.","Provide docking, radiators, redundant power and a return vehicle. Record mass and delta-v."},
            new[]{"Launch modules; use a stable parking orbit above Kerbin's atmosphere.","Rendezvous and dock with low relative speed; inspect every connection.","Board Hamish, Basil, Ada and Vikram. Confirm all four on the vessel.","Keep an operational return vehicle and reserve propellant."},
            new[]{"Pilot: check attitude control, manoeuvre planning and control reference.","Engineer: deploy radiators, establish power generation and check reserve charge.","Start the centrifuge using the habitat controls; verify the ring's actual rated radius.","Inspect internal routes, airlocks, cryobay and medical access. Conduct a 30-minute real-time watch."},
            new[]{"Keep one qualified operator awake and power available before freezing anyone.","Move relief crew into a real DeepFreeze cryopod and freeze them using the pod's controls.","Set a short wake alarm; the console requests thaw using the pod's API when available.","Wake the relief pair FIRST, assess them, then freeze the outgoing pair. Confirm handover."},
            new[]{"Run the simulated power-loss drill in the Emergency tab.","Diagnose loss of generation, isolate nonessential loads and check reserve power.","Restore generation, assess cooling and verify safe battery trend.","Treat the simulated sprain, keep one awake operator, and close the drill."},
            new[]{"Measure battery trend over a real-time observation interval.","Compare calculated spin gravity to the habitat's geometry and speed.","Record orbital period, periapsis, apoapsis and crew condition.","Complete a full watch handover without time warp."},
            new[]{"Long-duration objective: plot a Kerbin-Duna transfer using the game's orbital system.","Budget departure, correction, capture and return delta-v plus reserve; no fictional instant travel.","Test long-term cooling, cryopod power and autonomous control BEFORE departure.","Duna travel takes real in-game time. This phase is a long-term campaign, not a short session."},
            new[]{"Wake and medically assess every crew member before recovery.","Return to Kerbin on a planned trajectory with a thermal protection and landing system.","Recover the crew and archive the mission log.","Review incidents and update the next mission's design."}
        };

        public void Awake() { dutyConsole=this; DontDestroyOnLoad(this); Debug.Log("[MegaCrew] Mission console 0.3.7-medical-candidate loaded; single-player / campaign clock candidate; accelerated coast disabled."); }
        bool InGame { get { return HighLogic.CurrentGame != null && HighLogic.LoadedScene != GameScenes.MAINMENU; } }
        bool Allowed { get { return InGame && (HighLogic.SaveFolder ?? "").StartsWith("MegaCrew", StringComparison.OrdinalIgnoreCase); } }
        string StatePath { get { return Path.Combine(KSPUtil.ApplicationRootPath, "saves", HighLogic.SaveFolder, "MegaCrewMission.cfg"); } }
        Vessel Ship { get { return FlightGlobals.ActiveVessel; } }
        bool ActiveVesselIsEva { get { return HighLogic.LoadedSceneIsFlight && Ship != null && Ship.isEVA; } }
        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8)) visible=!visible;
            UpdateConsoleInputLock();
            if (!Allowed) return;
            string key=HighLogic.SaveFolder;
            if (key!=loadedSave||!object.ReferenceEquals(loadedGame,HighLogic.CurrentGame)) { ResetState(); loadedSave=key; loadedGame=HighLogic.CurrentGame; Load(); lastUt=Planetarium.GetUniversalTime(); }
            // This rule applies only to the new MegaCrew save. Cryosleep never advances the clock.
            if ((HighLogic.LoadedSceneIsFlight || HighLogic.LoadedScene==GameScenes.TRACKSTATION) && TimeWarp.CurrentRateIndex>0) TimeWarp.SetRate(0,true);
            double ut=Planetarium.GetUniversalTime(), dt=ut-lastUt; lastUt=ut;
            BindCampaignCrew();
            UpdateCampaignSupplies(dt);
            UpdateNeeds(dt);
            UpdateRecovery(dt);
            UpdateInterior();
            UpdateCare(dt);
            UpdatePractice(dt);
            // Do not count closed-game time, scene-load jumps, or a skipped/warped interval as a watch.
            if (dt<=0 || dt>5 || !HighLogic.LoadedSceneIsFlight || !running) return;
            shiftElapsed+=dt;
            if(alarmTime>0 && ut>=alarmTime) { alarmTime=0; WakeRelief(); }
            if(wakeQueue.Count>0 && Time.realtimeSinceStartup>nextWakePoll) {
                nextWakePoll=Time.realtimeSinceStartup+5;
                string who=wakeQueue.Peek();
                if(!Frozen(who)){wakeQueue.Dequeue();message=who+" is awake. Assess before completing the handover.";}
                else if(!CryoBridge.Busy && CryoBridge.Thaw(who))message="Thaw requested for "+who+". Awaiting actual pod completion.";
            }
            if(Time.realtimeSinceStartup>nextSave){ Save();nextSave=Time.realtimeSinceStartup+15; }
        }
        bool Aboard(string name) {
            if(Ship==null)return false;
            foreach(var p in Ship.parts)foreach(var c in p.protoModuleCrew)if(c.name==name)return true;
            return false;
        }
        bool Frozen(string name) { return CryoBridge.IsFrozen(name); }
        void ResetState(){crew=NewCrew();careAction="";ResetPractice();checks.Clear();wakeQueue.Clear();phase=0;running=false;shiftElapsed=0;drillActive=false;alarmTime=0;pilot=crew[0].Name;foreach(var c in crew){c.Fatigue=0;c.Hunger=0;c.Thirst=0;c.Resting=false;c.Recovery=0;c.Injury="None";c.Treated=false;} }
        void WakeRelief() {
            wakeQueue.Clear();
            foreach(var c in crew)if(Frozen(c.Name))wakeQueue.Enqueue(c.Name);
            message=wakeQueue.Count>0?"NPC watch alarm: sequential thaw queued for "+wakeQueue.Count+" frozen crew. Keep this vessel active and pod power available.":"WAKE ALARM: no frozen mission crew detected on this vessel. Verify DeepFreeze pod state.";
            ScreenMessages.PostScreenMessage(message,12,ScreenMessageStyle.UPPER_CENTER); Save();
        }
        public void OnGUI()
        {
            if(!HighLogic.LoadedSceneIsFlight && HighLogic.LoadedScene!=GameScenes.SPACECENTER && HighLogic.LoadedScene!=GameScenes.EDITOR && HighLogic.LoadedScene!=GameScenes.MAINMENU)return;
            GUISkin previousSkin=GUI.skin;bool previousEnabled=GUI.enabled;
            try {
            GUI.skin=HighLogic.Skin;
            if(!object.ReferenceEquals(statusStyleSkin,GUI.skin)||statusLabelStyle==null){statusStyleSkin=GUI.skin;statusLabelStyle=new GUIStyle(GUI.skin.label);statusLabelStyle.wordWrap=true;}
            ClearConsoleFocusOutside();
            if(GUI.Button(new Rect(12,Screen.height-95,165,34),"MEGACREW  /  F8"))visible=!visible;
            DrawInterior();if(!visible)return;
            window.width=Math.Min(720,Math.Max(1,Screen.width-24));window.height=Math.Min(520,Math.Max(1,Screen.height-48));
            window.x=Mathf.Clamp(window.x,0,Math.Max(0,Screen.width-window.width));window.y=Mathf.Clamp(window.y,0,Math.Max(0,Screen.height-window.height));
            window=GUI.Window(719304,window,Draw,"MEGACREW | HERMES OPERATIONS 0.3.7 candidate");
            }
            finally { GUI.skin=previousSkin;GUI.enabled=previousEnabled;RefreshConsoleGuiFocus(); }
        }
        void Draw(int id)
        {
            float width=Math.Max(1,window.width-20),height=Math.Max(1,window.height-35);
            const float headerHeight=32, evaHeight=22, tabHeight=54, statusHeight=52;
            float contextHeight=ActiveVesselIsEva?evaHeight:0;
            float bodyY=headerHeight+contextHeight+tabHeight;
            float statusY=Math.Max(bodyY+1,height-statusHeight);
            float bodyHeight=Math.Max(1,statusY-bodyY);

            GUILayout.BeginArea(new Rect(10,25,width,headerHeight));
            try
            {
                GUI.Label(new Rect(0,2,width-82,28),"COOPERATIVE CREW  /  SINGLE PLAYER FIRST  /  1x TIME");
                bool enabledBeforeClose=GUI.enabled;
                GUI.enabled=true;
                if(GUI.Button(new Rect(width-76,1,76,29),"Close"))visible=false;
                GUI.enabled=enabledBeforeClose;
            }
            finally { GUILayout.EndArea(); }

            if(contextHeight>0)
            {
                GUILayout.BeginArea(new Rect(10,25+headerHeight,width,evaHeight));
                try { GUI.Label(new Rect(0,0,width,22),"ACTIVE VESSEL IS EVA — CREW / CRYO CONTROLS READ-ONLY"); }
                finally { GUILayout.EndArea(); }
            }

            GUILayout.BeginArea(new Rect(10,25+headerHeight+contextHeight,width,tabHeight));
            try { tab=GUI.SelectionGrid(new Rect(0,0,width,tabHeight),tab,tabs,4); }
            finally { GUILayout.EndArea(); }

            GUILayout.BeginArea(new Rect(10,25+bodyY,width,bodyHeight));
            try
            {
                scroll=GUILayout.BeginScrollView(scroll,false,true,GUILayout.Width(width),GUILayout.Height(bodyHeight));
                try
                {
                    if(!Allowed)GUILayout.Label("MISSION EDITING LOCKED: create/load a Sandbox save whose name begins MegaCrew. Other saves are unaffected.");
                    if(tab==0)DrawMission();else if(tab==1)DrawCrew();else if(tab==2)DrawEngineering();else if(tab==3)DrawEmergency();else if(tab==4)DrawPractice();else if(tab==5)DrawNeeds();else if(tab==6)DrawCampaign();else DrawSetup();
                }
                finally { GUILayout.EndScrollView(); }
            }
            finally { GUILayout.EndArea(); }

            GUILayout.BeginArea(new Rect(10,25+statusY,width,statusHeight));
            try
            {
                bool enabledBeforeStatus=GUI.enabled;GUI.enabled=true;
                GUI.Label(new Rect(0,0,width,statusHeight-2),message,statusLabelStyle);
                GUI.enabled=enabledBeforeStatus;
            }
            finally { GUILayout.EndArea(); }

            // Restrict dragging to the native title bar; content rectangles never intercept it.
            GUI.DragWindow(new Rect(0,0,window.width,22));
        }
        void DrawMission()
        {
            GUILayout.Label("HERMES / FOUR-KERBAL EXPEDITION");
            GUILayout.Label("A stock-scale KSP expedition inspired by The Martian. Initial mission: assemble and commission in Kerbin orbit, test cryosleep, rotate watches, handle a medical/power drill. Duna is the long-duration campaign.");
            GUILayout.BeginHorizontal();if(GUILayout.Button("< Phase")&&phase>0){phase--;Save();}GUILayout.Label(titles[phase]);if(GUILayout.Button("Phase >")&&phase<titles.Length-1){phase++;Save();}GUILayout.EndHorizontal();
            for(int i=0;i<tasks[phase].Length;i++) {
                string key=phase+":"+i;bool old=checks.Contains(key);
                GUI.enabled=Allowed;bool value=GUILayout.Toggle(old,tasks[phase][i]);GUI.enabled=true;
                if(old!=value){if(value)checks.Add(key);else checks.Remove(key);Save();}
            }
            GUILayout.Label("Checkmarks are crew sign-offs, not automatic proof. Vessel telemetry appears below; DeepFreeze status is read from its actual API when available.");
            DrawTelemetry();
            GUI.enabled=Allowed&&HighLogic.LoadedSceneIsFlight;
            if(GUILayout.Button(running?"Pause watch tracking":"Begin / resume real-time watch")){running=!running;lastUt=Planetarium.GetUniversalTime();Save();}
            GUI.enabled=true;GUILayout.Label("Tracked awake watch: "+(shiftElapsed/60).ToString("F1")+" minutes. Time warp is prevented in MegaCrew flight saves.");
        }
        void DrawCrew()
        {
            bool guiWasEnabled=GUI.enabled;
            bool eva=ActiveVesselIsEva;
            bool crewActionsEnabled=guiWasEnabled&&Allowed&&!eva;
            if(eva)GUILayout.Label("Read-only EVA view: return to the loaded ship to use crew or cryopod controls.");
            GUILayout.Label("Actual cryosleep uses DeepFreeze parts. Freezing is always deliberate via the pod controls; the console never fabricates frozen crew or removes them from the roster.");
            GUILayout.Label("Relief cycle: Hamish + Basil awake / Ada + Vikram frozen -> thaw Ada + Vikram -> medical assessment -> freeze Hamish + Basil -> relief alarm thaws outgoing watch.");
            GUILayout.Label("Current selected crew role: "+pilot+". NPCs perform timed watch/alert duties; autonomous walking, docking and repairs are not implemented.");
            foreach(var c in crew) {
                GUILayout.BeginVertical(GUI.skin.box);
                string state=Frozen(c.Name)?"CRYOSLEEP":Aboard(c.Name)?"AWAKE ON VESSEL":"NOT ON ACTIVE VESSEL";
                GUILayout.Label(c.Name+" | "+c.Role+" | "+state);
                GUILayout.Label("Watch fatigue "+c.Fatigue.ToString("F0")+"% | Injury: "+c.Injury+(c.Treated?" (treated, "+(c.Recovery/60).ToString("F1")+" min simulated recovery)":""));
                GUI.enabled=crewActionsEnabled&&Aboard(c.Name)&&!Frozen(c.Name);
                if(GUILayout.Button("Select as watch lead")){pilot=c.Name;message="Watch lead selected: "+pilot+". Use stock crew portraits / IVA controls to view or EVA this Kerbal.";Save();}
                if(GUILayout.Button("Enter cryosleep in current pod")) {
                    int awake=0;foreach(var x in crew)if(Aboard(x.Name)&&!Frozen(x.Name))awake++;
                    if(awake<2)message="Keep another awake watch member. Thaw relief crew before freezing the outgoing watch.";
                    else message=CryoBridge.Freeze(c.Name)?"Freeze requested for "+c.Name+". Confirm completion in pod UI.":"Transfer this Kerbal into an idle DeepFreeze cryopod first; verify power and Glykerol.";
                }
                if(c.Injury!="None"&&!c.Treated && GUILayout.Button("Treat at medical station (simulation)"))Treat(c);
                GUI.enabled=crewActionsEnabled;GUILayout.EndVertical();
            }
            GUI.enabled=crewActionsEnabled&&HighLogic.LoadedSceneIsFlight;
            if(GUILayout.Button("Set relief wake alarm: 10 real-time minutes")){alarmTime=Planetarium.GetUniversalTime()+600;message="Ten-minute wake alarm set. Watch tracking must be running; keep this vessel loaded.";Save();}
            if(GUILayout.Button("Wake frozen crew now"))WakeRelief();GUI.enabled=guiWasEnabled;
            DrawCryoPowerAudit();
            GUILayout.Label("Cryo API: "+CryoBridge.Status+". Thaw requests still require pod power and other DeepFreeze conditions. Always verify pod status.");
            GUILayout.Label("Health model: fatigue and minor injury records are custom gameplay abstractions, not physiological predictions. Cryosleep is fictional technology; no claim of real human feasibility.");
        }
        void Treat(CrewRecord patient) {
            int medics=0;foreach(var c in crew)if(c!=patient&&Aboard(c.Name)&&!Frozen(c.Name)&&!c.Resting&&c.Injury=="None")medics++;
            if(medics==0){message="An awake, uninjured second crew member is required to treat this injury.";return;}
            patient.Treated=true;patient.Recovery=300;message="Simulated treatment complete. Five-minute recovery watch; no global flight controls or Kerbal abilities were changed.";Save();
        }
        void DrawEngineering()
        {
            GUILayout.Label("CENTRIFUGE DESIGN  |  a = (2*pi*rpm/60)^2 * r");
            InputRow("Habitat radius [m]",ref radius);InputRow("Rotation [rpm]",ref rpm);
            double r,n;
            if(Parse(radius,out r)&&Parse(rpm,out n)&&r>0&&n>=0){GUILayout.Label("Calculated artificial gravity: "+Science.SpinG(r,n).ToString("F3")+" g | Mars-reference 0.38 g needs "+Science.Rpm(r,0.38).ToString("F2")+" rpm.");GUILayout.Label("Rim speed: "+(2*Math.PI*r*n/60).ToString("F2")+" m/s. Use actual part geometry, not the illustrative radius. This calculator does not spin the ship.");}
            else GUILayout.Label("Enter a positive radius and nonnegative rpm using a decimal point.");
            GUILayout.Space(12);GUILayout.Label("PROPULSION BUDGET  |  delta-v = Isp * g0 * ln(wet / dry)");
            InputRow("Isp [seconds]",ref isp);InputRow("Wet mass [tonnes]",ref wet);InputRow("Final mass [tonnes]",ref dry);
            double i,w,d;if(Parse(isp,out i)&&Parse(wet,out w)&&Parse(dry,out d)&&i>0&&d>0&&w>d)GUILayout.Label("Ideal single-stage delta-v: "+Science.DeltaV(i,w,d).ToString("F0")+" m/s. Excludes gravity/steering losses, reserves and stage changes.");
            GUILayout.Label("Defaults are design examples, not measurements of your ship. No manoeuvre or throttle commands are executed by this version.");DrawTelemetry();
        }
        void InputRow(string label,ref string value){GUILayout.BeginHorizontal();GUILayout.Label(label,GUILayout.Width(200));GUI.SetNextControlName(ConsoleFieldPrefix+label);value=GUILayout.TextField(value,16,GUILayout.Width(120));GUILayout.EndHorizontal();}
        static bool Parse(string s,out double v){return double.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out v)&&!double.IsNaN(v)&&!double.IsInfinity(v);}
        void DrawTelemetry()
        {
            if(!HighLogic.LoadedSceneIsFlight||Ship==null){GUILayout.Label("LIVE TELEMETRY: enter flight with a vessel.");return;}
            var v=Ship;GUILayout.Label("LIVE | "+v.vesselName+" | "+v.mainBody.bodyName+" | "+v.situation);
            GUILayout.Label("Altitude "+(v.altitude/1000).ToString("F1")+" km | mass "+v.GetTotalMass().ToString("F1")+" t | crew "+v.GetCrewCount()+" awake");
            if(v.orbit!=null)GUILayout.Label("Pe "+(v.orbit.PeA/1000).ToString("F1")+" km | Ap "+(v.orbit.ApA/1000).ToString("F1")+" km | period "+(v.orbit.period/60).ToString("F1")+" min");
            double charge=0,cap=0;foreach(var p in v.parts)foreach(PartResource res in p.Resources)if(res.resourceName=="ElectricCharge"){charge+=res.amount;cap+=res.maxAmount;}
            GUILayout.Label("ElectricCharge "+charge.ToString("F0")+" / "+cap.ToString("F0")+" EC. EC is a game resource, not assumed to equal joules.");
        }
        void DrawEmergency()
        {
            GUILayout.Label("TRAINING DRILLS | No random injury generator is enabled. Drills create mission records; they do not silently damage parts.");
            GUI.enabled=Allowed&&HighLogic.LoadedSceneIsFlight;
            if(GUILayout.Button("Start POWER LOSS training drill")){drillActive=true;message="SIMULATED power loss: engineer investigates generation; pilot checks attitude, battery reserve and safe abort. Perform the checklist against the actual craft.";Save();}
            if(GUILayout.Button("Record minor sprain for watch lead (training)")){var c=crew.Find(x=>x.Name==pilot);if(c!=null&&Aboard(c.Name)&&!Frozen(c.Name)){c.Injury="Minor sprain (training)";c.Treated=false;c.Recovery=0;message="Medical task opened. An awake second crew member must assist treatment.";Save();}else message="Select an awake crew member on this vessel first.";}
            if(drillActive&&GUILayout.Button("Close drill after generation, cooling and crew checks")){drillActive=false;checks.Add("drillComplete");message="Drill closed by crew sign-off; no automatic hardware repair was claimed.";Save();}GUI.enabled=true;
            GUILayout.Label(drillActive?"ACTIVE: Power loss drill":"No active drill.");
            GUILayout.Label("Medical loop: diagnose -> wake medic if needed -> treat -> observed recovery -> reassess before freezing or EVA.");
        }
        void DrawSetup()
        {
            GUILayout.Label("1. Start a NEW Sandbox named MegaCrew (or MegaCrew Test). This isolates the mission rules from other saves.");
            GUILayout.Label("2. Press Recruit four mission Kerbals here, then assign them in the editor's Crew tab. Hamish/Basil are player roles; Ada/Vikram are NPC relief crew.");
            GUI.enabled=Allowed&&!HighLogic.LoadedSceneIsFlight;
            if(GUILayout.Button("Recruit the four mission crew into this save"))Recruit();GUI.enabled=true;
            GUILayout.Label("3. Build the Hermes-inspired ship using SSPX centrifuges, habitation, command, power, radiators, cryopods and a return vehicle. The initial orbital mission is playable without an interplanetary time skip.");
            GUILayout.Label("4. Crew rotation needs functioning DeepFreeze pods. Thaw incoming watch before freezing outgoing watch. Keep a control core and enough electric reserve.");
            GUILayout.Label("5. F8 toggles this console. Checklists and health/shift records save per KSP save. No Luna or multiplayer synchronisation in this first version.");
            GUILayout.Label("This is an initial custom mission console and integrated mod foundation. It does not yet provide a prebuilt launchable Hermes, autonomous NPC navigation or a full medical simulation.");
        }
        void Recruit()
        {
            try {
                var roster=HighLogic.CurrentGame.CrewRoster;
                foreach(var c in crew){if(roster[c.Name]!=null)continue;var k=roster.GetNewKerbal(ProtoCrewMember.KerbalType.Crew);k.ChangeName(c.Name);KerbalRoster.SetExperienceTrait(k,c.Name=="Basil Kerman"||c.Name=="Vikram Kerman"?"Engineer":c.Name=="Ada Kerman"?"Scientist":"Pilot");k.rosterStatus=ProtoCrewMember.RosterStatus.Available;}
                message="Four mission Kerbals are in this save's roster. Assign them to the ship in the editor.";Save();
            } catch(Exception e){message="Recruitment failed: "+e.Message;Debug.LogError("[MegaCrew] "+e);}
        }
        void Save()
        {
            if(!Allowed||loadedSave!=HighLogic.SaveFolder)return;
            try {
                var n=new ConfigNode("MEGACREW");n.AddValue("version",1);n.AddValue("phase",phase);n.AddValue("pilot",pilot);n.AddValue("running",running);n.AddValue("watchSeconds",shiftElapsed);n.AddValue("wakeUT",alarmTime);n.AddValue("drill",drillActive);
                foreach(var k in checks)n.AddValue("check",k);
                foreach(var c in crew){var x=n.AddNode("CREW");x.AddValue("name",c.Name);x.AddValue("fatigue",c.Fatigue);x.AddValue("injury",c.Injury);x.AddValue("treated",c.Treated);x.AddValue("recovery",c.Recovery);}
                foreach(var queued in wakeQueue)n.AddValue("wakeQueued",queued);SavePractice(n);SaveNeeds(n);n.Save(StatePath);
            }catch(Exception e){Debug.LogError("[MegaCrew] State save failed: "+e.Message);message="Mission state could not save; see KSP.log.";}
        }
        void Load()
        {
            try {
                if(!File.Exists(StatePath)){message="Open Practice for the orbital maintenance exercise, or Mission for the longer campaign.";return;}var n=ConfigNode.Load(StatePath);if(n==null)return;if(n.HasNode("MEGACREW"))n=n.GetNode("MEGACREW");LoadPractice(n);LoadNeeds(n);foreach(var queued in n.GetValues("wakeQueued"))if(crew.Exists(c=>c.Name==queued)&&!wakeQueue.Contains(queued))wakeQueue.Enqueue(queued);
                int.TryParse(n.GetValue("phase"),out phase);phase=Math.Max(0,Math.Min(titles.Length-1,phase));pilot=n.GetValue("pilot")??crew[0].Name;
                bool.TryParse(n.GetValue("running"),out running);double.TryParse(n.GetValue("watchSeconds"),out shiftElapsed);double.TryParse(n.GetValue("wakeUT"),out alarmTime);bool.TryParse(n.GetValue("drill"),out drillActive);
                foreach(var k in n.GetValues("check"))checks.Add(k);
                foreach(var x in n.GetNodes("CREW")){var c=crew.Find(z=>z.Name==x.GetValue("name"));if(c==null)continue;double.TryParse(x.GetValue("fatigue"),out c.Fatigue);c.Injury=x.GetValue("injury")??"None";bool.TryParse(x.GetValue("treated"),out c.Treated);double.TryParse(x.GetValue("recovery"),out c.Recovery);}
                message="Mission state restored for "+loadedSave+".";
            }catch(Exception e){message="Could not restore mission state: "+e.Message;}
        }
        public void OnApplicationQuit(){Save();}
    }

    // Integrates only documented public DeepFreeze members; never edits its crew dictionary.
    public static class CryoBridge
    {
        static Type DfType {
            get { foreach(var a in AppDomain.CurrentDomain.GetAssemblies()){var t=a.GetType("DF.DeepFreeze");if(t!=null)return t;}return null; }
        }
        static object Instance { get { var t=DfType;return t==null?null:t.GetField("Instance").GetValue(null); } }
        static bool Ready {
            get {try{var t=DfType;var f=t==null?null:t.GetField("APIReady");return f!=null&&f.GetValue(null) is bool&&(bool)f.GetValue(null)&&Instance!=null;}catch{return false;}}
        }
        static System.Collections.IDictionary FrozenCrew {
            get { if(!Ready)return null;var i=Instance;return i.GetType().GetProperty("FrozenKerbals").GetValue(i,null) as System.Collections.IDictionary; }
        }
        static object Info(string name) { try{var d=FrozenCrew;return d!=null&&d.Contains(name)?d[name]:null;}catch{return null;} }
        public static string Status { get { return DfType==null?"DeepFreeze not loaded":!Ready?"DeepFreeze initializing; crew state unverified":"DeepFreeze connected"; } }
        public static bool TryPowerPolicy(out bool required,out bool fatal)
        {
            required=false;fatal=false;
            try{return Ready&&CryoAudit.Read(Instance,"DFIECReqd",out required)&&CryoAudit.Read(Instance,"DFIDeathFatal",out fatal);}catch{return false;}
        }
        public static bool TryFrozen(string name,out bool frozen)
        {
            frozen=false;
            try {
                var d=FrozenCrew;var v=FlightGlobals.ActiveVessel;
                if(d==null||v==null)return false;
                if(!d.Contains(name))return true;
                var info=d[name];if(info==null)return false;
                var f=info.GetType().GetField("vesselID");if(f==null)return false;
                if((Guid)f.GetValue(info)!=v.id)return false;
                frozen=true;return true;
            }catch{return false;}
        }
        public static bool IsFrozen(string name) {
            var info=Info(name);var v=FlightGlobals.ActiveVessel;if(info==null||v==null)return false;
            var field=info.GetType().GetField("vesselID");return field!=null&&(Guid)field.GetValue(info)==v.id;
        }
        public static bool Busy {
            get {
                try {
                    var v=FlightGlobals.ActiveVessel;if(!Ready||v==null||!v.loaded||v.packed)return true;
                    foreach(var p in v.parts)foreach(PartModule m in p.Modules)if(m.moduleName=="DeepFreezer")
                        foreach(string name in new[]{"DFIIsFreezeActive","DFIIsThawActive","DFIcrewXferTOActive","DFIcrewXferFROMActive"}) {
                            bool active;if(!CryoAudit.Read(m,name,out active)||active)return true;
                        }
                    return false;
                }catch{return true;}
            }
        }
        public static bool Thaw(string name) {
            try {
                if(!IsFrozen(name)||Busy)return false;var info=Info(name);uint id=(uint)info.GetType().GetField("partID").GetValue(info);
                foreach(var p in FlightGlobals.ActiveVessel.parts)if(p.flightID==id && p.protoModuleCrew.Count<p.CrewCapacity)
                    foreach(PartModule m in p.Modules)if(m.moduleName=="DeepFreezer"){m.GetType().GetMethod("beginThawKerbal").Invoke(m,new object[]{name});return true;}
            }catch(Exception e){Debug.LogError("[MegaCrew] Thaw API: "+e.Message);}return false;
        }
        public static bool Freeze(string name) {
            try {
                if(Busy||FlightGlobals.ActiveVessel==null)return false;
                foreach(var p in FlightGlobals.ActiveVessel.parts)foreach(var c in p.protoModuleCrew)if(c.name==name)
                    foreach(PartModule m in p.Modules)if(m.moduleName=="DeepFreezer"){m.GetType().GetMethod("beginFreezeKerbal").Invoke(m,new object[]{c});return true;}
            }catch(Exception e){Debug.LogError("[MegaCrew] Freeze API: "+e.Message);}return false;
        }
    }
}
