using System;
using System.Reflection;
using UnityEngine;

namespace MegaCrew
{
    public sealed partial class MissionConsole
    {
        bool interiorOpen;
        Rect interiorWindow=new Rect(790,70,410,560);
        Vector2 interiorScroll;
        static Type freeIvaType;
        Part CurrentRoom
        {
            get{
                if(!HighLogic.LoadedSceneIsFlight||CameraManager.Instance==null||CameraManager.Instance.currentCameraMode!=CameraManager.CameraMode.IVA)return null;
                if(freeIvaType==null)freeIvaType=Type.GetType("FreeIva.FreeIva, FreeIva");
                var prop=freeIvaType==null?null:freeIvaType.GetProperty("CurrentPart",BindingFlags.Static|BindingFlags.Public);
                var p=prop==null?null:prop.GetValue(null,null) as Part;
                if(p!=null&&p.vessel==Ship)return p;
                var k=ViewedKerbal;
                return k!=null&&k.protoCrewMember!=null&&k.protoCrewMember.seat!=null?k.protoCrewMember.seat.internalModel.part:null;
            }
        }
        Kerbal ViewedKerbal
        {
            get{if(CameraManager.Instance==null)return null;var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;var f=typeof(CameraManager).GetField("ivaCameraActiveKerbal",flags);if(f!=null)return f.GetValue(CameraManager.Instance) as Kerbal;var p=typeof(CameraManager).GetProperty("ivaCameraActiveKerbal",flags);return p==null?null:p.GetValue(CameraManager.Instance,null) as Kerbal;}
        }
        CrewRecord InteriorCrew
        {
            get{var k=CameraManager.Instance==null?null:ViewedKerbal;return k==null?null:crew.Find(c=>c.Name==k.protoCrewMember.name);}
        }
        void UpdateInterior()
        {
            if(!HighLogic.LoadedSceneIsFlight)return;
            if(Input.GetKeyDown(KeyCode.I)&&!FlightDriver.Pause)interiorOpen=!interiorOpen;
            var room=CurrentRoom;
            if(room!=null&&room.vessel==Ship&&InteriorCrew!=null&&checks.Add("visited:"+room.partInfo.name))Save();
        }
        void DrawInterior()
        {
            if(!Allowed||!HighLogic.LoadedSceneIsFlight||!interiorOpen)return;
            var room=CurrentRoom;
            interiorWindow.width=Math.Min(410,Screen.width-20);interiorWindow.height=Math.Min(560,Screen.height-40);
            interiorWindow.x=Mathf.Clamp(interiorWindow.x,0,Math.Max(0,Screen.width-interiorWindow.width));interiorWindow.y=Mathf.Clamp(interiorWindow.y,0,Math.Max(0,Screen.height-interiorWindow.height));
            interiorWindow=GUILayout.Window(719305,interiorWindow,DrawRoom,"HERMES / INTERACT [I]",GUILayout.Width(interiorWindow.width),GUILayout.Height(interiorWindow.height));
        }
        void DrawRoom(int id)
        {
            if(GUILayout.Button("Close [I]"))interiorOpen=false;
            interiorScroll=GUILayout.BeginScrollView(interiorScroll);
            var room=CurrentRoom;var c=InteriorCrew;
            if(room==null||c==null){GUILayout.Label("Enter a mission Kerbal's IVA with C. Walk into a room, then press I. Interior operations require your actual room and viewed Kerbal.");GUILayout.EndScrollView();GUI.DragWindow();return;}
            GUILayout.Label(room.partInfo.title+" | "+c.Name);
            GUILayout.Label("Hunger "+c.Hunger.ToString("F0")+"% | Thirst "+c.Thirst.ToString("F0")+"% | Fatigue "+c.Fatigue.ToString("F0")+"%");
            GUILayout.Label("Station controls operate this vessel. F still operates FreeIVA hatches. Decorative geometry is not a simulated mechanism.");
            if(GUILayout.Button("Inspect the object I am looking at"))InspectInterior(room);
            string name=room.partInfo.name;
            bool quarters=name.Contains("habitation")||name.Contains("centrifuge");
            bool lab=name.Contains("lab");bool cryo=name.Contains("Freezer");bool engineering=name.Contains("tube")||name.Contains("centrifuge");bool cockpit=name.Contains("core");
            if(quarters){GUILayout.Label("GALLEY / BUNKS");GUI.enabled=!c.Resting&&!Frozen(c.Name);if(GUILayout.Button("Eat ration / 0.5 Food"))Eat(c);if(GUILayout.Button("Drink / 0.25 Water"))Drink(c);GUI.enabled=true;if(GUILayout.Button(c.Resting?"Wake from ordinary sleep":"Sleep in crew quarters"))Rest(c);}
            if(quarters)DrawCare(c,room);
            bool fit=NeedsModel.Fit(c)&&!Frozen(c.Name);
            if(!fit)GUILayout.Label("Address food, water, sleep or daily care before engineering/science duty. Wake through the galley/bunk controls; emergency care is available.");
            if(engineering){GUILayout.Label("ENGINEERING / POWER & COOLING");GUI.enabled=fit;foreach(var p in Ship.parts)foreach(PartModule m in p.Modules)if(m.moduleName=="ModuleDeployableSolarPanel"||m.moduleName=="ModuleDeployableRadiator"){
                GUILayout.Label(p.partInfo.title+" #"+p.flightID);GUILayout.BeginHorizontal();if(GUILayout.Button("Extend"))Deploy(m,true);if(GUILayout.Button("Retract"))Deploy(m,false);GUILayout.EndHorizontal();
            }GUI.enabled=true;}
            if(lab){GUILayout.Label("MEDICAL / SCIENCE");foreach(var patient in crew)if(patient.Injury!="None"&&!patient.Treated&&Aboard(patient.Name)&&!Frozen(patient.Name)&&GUILayout.Button("Treat "+patient.Name))Treat(patient);
                GUI.enabled=fit;if(GUILayout.Button("Record orbital / power / temperature survey")){checks.Add("scienceSurvey");message="Survey logged at UT "+Planetarium.GetUniversalTime().ToString("F0")+": Pe "+(Ship.orbit.PeA/1000).ToString("F1")+" km, Ap "+(Ship.orbit.ApA/1000).ToString("F1")+" km. Observation recorded; stock science points are not fabricated.";Debug.Log("[MegaCrew] "+message);Save();}GUI.enabled=true;
            }
            if(cryo){GUILayout.Label("CRYOBAY / ACTUAL DEEPFREEZE CONTROLS");GUILayout.Label("Transfer crew into pods using stock transfer first. Keep another awake crewmember and enough EC/Glykerol.");foreach(var x in crew){if(Frozen(x.Name)){if(GUILayout.Button("Queue thaw: "+x.Name)){if(!wakeQueue.Contains(x.Name))wakeQueue.Enqueue(x.Name);running=true;Save();}}
                else if(Aboard(x.Name)&&GUILayout.Button("Freeze: "+x.Name)){int awake=0;foreach(var y in crew)if(Aboard(y.Name)&&!Frozen(y.Name)&&!y.Resting)awake++;message=awake<2?"Keep another awake watch member.":CryoBridge.Freeze(x.Name)?"Freeze requested. Wait for actual pod completion.":"Transfer into an idle pod; check EC and Glykerol.";Save();}}
            }
            if(cockpit){GUILayout.Label("FLIGHT DECK");DrawTelemetry();GUILayout.Label("Navigation readout only; stock flight controls retain authority.");}
            if(!quarters&&!lab&&!cryo&&!engineering&&!cockpit)GUILayout.Label("Transit / storage compartment. Inspect fittings and use FreeIVA hatch controls.");
            GUILayout.Label(message);GUILayout.EndScrollView();GUI.DragWindow(new Rect(0,0,10000,24));
        }
        void Deploy(PartModule m,bool extend)
        {
            try{var method=m.GetType().GetMethod(extend?"Extend":"Retract",BindingFlags.Public|BindingFlags.Instance,null,Type.EmptyTypes,null);if(method==null){message="This module does not expose the requested deployment operation.";return;}method.Invoke(m,null);message="Requested "+(extend?"extension":"retraction")+" of "+m.part.partInfo.title+"; verify actual state.";}
            catch(Exception e){message="Part rejected deployment: "+e.GetBaseException().Message;}
        }
        void InspectInterior(Part room)
        {
            var camera=InternalCamera.Instance==null?null:InternalCamera.Instance.GetComponentInChildren<Camera>();
            if(camera==null){message="IVA camera unavailable.";return;}
            var hits=Physics.RaycastAll(camera.transform.position,camera.transform.forward,3f,~0,QueryTriggerInteraction.Collide);
            Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits){var prop=hit.collider.GetComponentInParent<InternalProp>();if(prop==null||!prop.transform.IsChildOf(room.internalModel.transform))continue;message="Inspected "+prop.propName+" at "+hit.distance.ToString("F1")+" m. Use this room's station controls for functional operations.";checks.Add("inspected:"+room.partInfo.name);Save();return;}
            message="No nearby prop collider under the crosshair. The room station remains usable; hatches use F.";
        }
    }
}
