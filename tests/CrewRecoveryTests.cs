using System;
using System.Collections.Generic;
using MegaCrew;
public static class HighLogic {public static bool LoadedSceneIsFlight=true;public static string SaveFolder="MegaCrew Test";public static Game CurrentGame=new Game();}
public class ProtoScenarioModule {public string moduleName;public object moduleRef;}
public class Game {public List<ProtoScenarioModule> scenarios=new List<ProtoScenarioModule>();}
public static class FlightGlobals {public static bool ready=true;}
public static class FlightDriver {public static bool Pause;}
public static class TimeWarp {public static int CurrentRateIndex;public static float CurrentRate=1;}
public class Vessel {public Guid id=Guid.NewGuid();public bool loaded=true,packed,isEVA;}
namespace MegaCrew {
 public class HermesCampaign {public string LoadError="",SupplyError="";public bool Started;public Guid VesselId;public List<CrewRecord> CrewSnapshot;}
 public static class CryoBridge {public static bool Verified=true,Frozen,Busy;public static bool TryFrozen(string n,out bool frozen){frozen=Frozen;return Verified;}}
 public sealed partial class MissionConsole {
  string message;
  Game loadedGame=HighLogic.CurrentGame;bool Allowed=true;public bool isActiveAndEnabled=true;string loadedSave="MegaCrew Test";Vessel Ship=new Vessel();HermesCampaign Campaign;
  List<CrewRecord> crew=new List<CrewRecord>{new CrewRecord("Hamish Kerman","Pilot")};bool aboard=true;
  bool Aboard(string name){return aboard;}
  static void Yes(bool b){if(!b)throw new Exception("Medical recovery assertion failed");}
  CrewRecord Patient {get{return crew[0];}}
  void Held(Action change,Action restore){change();double before=Patient.Recovery;UpdateRecovery(5);Yes(Patient.Recovery==before);restore();}
  public static void Main(){
   var c=new MissionConsole();dutyConsole=c;var p=c.Patient;
   p.Injury="Minor sprain";p.Treated=true;p.Recovery=300;
   string reason;Yes(!CanPerformDuty(p.Name,c.Ship.id,out reason));
   c.UpdateRecovery(5);Yes(p.Recovery==295); // no practice watch timer exists in this boundary
   c.Held(()=>FlightDriver.Pause=true,()=>FlightDriver.Pause=false);
   c.Held(()=>HighLogic.LoadedSceneIsFlight=false,()=>HighLogic.LoadedSceneIsFlight=true);
   c.Held(()=>FlightGlobals.ready=false,()=>FlightGlobals.ready=true);
   c.Held(()=>TimeWarp.CurrentRateIndex=1,()=>TimeWarp.CurrentRateIndex=0);
   c.Held(()=>TimeWarp.CurrentRate=540,()=>TimeWarp.CurrentRate=1);
   c.Held(()=>c.Ship.packed=true,()=>c.Ship.packed=false);
   c.Held(()=>c.Ship.loaded=false,()=>c.Ship.loaded=true);
   c.Held(()=>c.Ship.isEVA=true,()=>c.Ship.isEVA=false);
   c.Held(()=>c.aboard=false,()=>c.aboard=true);
   c.Held(()=>c.Allowed=false,()=>c.Allowed=true);
   c.Held(()=>CryoBridge.Verified=false,()=>CryoBridge.Verified=true);
   c.Held(()=>CryoBridge.Frozen=true,()=>CryoBridge.Frozen=false);
   c.Held(()=>CryoBridge.Busy=true,()=>CryoBridge.Busy=false);
   c.Held(()=>c.loadedSave="Other",()=>c.loadedSave=HighLogic.SaveFolder);
   var game=HighLogic.CurrentGame;
   c.Held(()=>HighLogic.CurrentGame=new Game(),()=>HighLogic.CurrentGame=game);
   var pending=new ProtoScenarioModule{moduleName="HermesCampaign"};
   c.Held(()=>game.scenarios.Add(pending),()=>game.scenarios.Clear());
   c.Campaign=new HermesCampaign{Started=true,VesselId=c.Ship.id,CrewSnapshot=c.crew};
   c.Held(()=>c.Campaign.LoadError="Invalid",()=>c.Campaign.LoadError="");
   c.Held(()=>c.Campaign.SupplyError="Unknown",()=>c.Campaign.SupplyError="");
   c.Held(()=>c.Campaign.VesselId=Guid.NewGuid(),()=>c.Campaign.VesselId=c.Ship.id);
   c.Held(()=>c.Campaign.CrewSnapshot=new List<CrewRecord>(),()=>c.Campaign.CrewSnapshot=c.crew);
   foreach(double dt in new[]{0.0,-1,6,double.NaN,double.PositiveInfinity}){double before=p.Recovery;c.UpdateRecovery(dt);Yes(p.Recovery==before);}
   p.Treated=false;c.UpdateRecovery(5);Yes(p.Recovery==295);p.Treated=true;
   for(int i=0;i<58;i++)c.UpdateRecovery(5);Yes(p.Recovery==5);Yes(p.Injury!="None");
   c.UpdateRecovery(5);Yes(p.Recovery==0&&p.Injury=="None"&&!p.Treated);Yes(CanPerformDuty(p.Name,c.Ship.id,out reason));
   c.UpdateRecovery(5);Yes(p.Recovery==0);
   var invalid=new CrewRecord("Test","Engineer"){Injury="Sprain",Treated=true,Recovery=double.NaN};
   Yes(!MedicalRecovery.Advance(invalid,5,false));Yes(invalid.Injury=="Sprain");
   Console.WriteLine("PASS: production medical adapter recovers treated awake crew independently of watch tracking; pauses on uncertain/frozen/unloaded/warped state; actual duty query allows only after recovery. KSP boundaries doubled.");
  }
 }
}
