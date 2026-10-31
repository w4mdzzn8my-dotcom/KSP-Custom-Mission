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
 public static class CryoBridge {public static bool Verified=true,Frozen;public static bool TryFrozen(string n,out bool frozen){frozen=Frozen;return Verified;}}
 public sealed partial class MissionConsole {
  Game loadedGame=HighLogic.CurrentGame;bool Allowed=true;public bool isActiveAndEnabled=true;string loadedSave="MegaCrew Test";Vessel Ship=new Vessel();HermesCampaign Campaign;
  List<CrewRecord> crew=new List<CrewRecord>{new CrewRecord("Hamish Kerman","Pilot")};bool aboard=true;
  bool Aboard(string name){return aboard;}
  static void Yes(bool b){if(!b)throw new Exception("Duty readiness assertion failed");}
  bool Check(){string reason;bool ready=CanPerformDuty("Hamish Kerman",Ship.id,out reason);Yes(ready?reason=="":reason.Length>0);return ready;}
  public static void Main(){
   var c=new MissionConsole();dutyConsole=c;Yes(c.Check());c.crew[0].Hunger=49;Yes(c.Check());Yes(c.crew[0].Hunger==49); // read-only
   c.isActiveAndEnabled=false;Yes(!c.Check());c.isActiveAndEnabled=true;c.Allowed=false;Yes(!c.Check());c.Allowed=true;
   FlightDriver.Pause=true;Yes(!c.Check());FlightDriver.Pause=false;TimeWarp.CurrentRateIndex=1;Yes(!c.Check());TimeWarp.CurrentRateIndex=0;TimeWarp.CurrentRate=540;Yes(!c.Check());TimeWarp.CurrentRate=1;
   FlightGlobals.ready=false;Yes(!c.Check());FlightGlobals.ready=true;HighLogic.LoadedSceneIsFlight=false;Yes(!c.Check());HighLogic.LoadedSceneIsFlight=true;
   c.loadedSave="MegaCrew Other";Yes(!c.Check());c.loadedSave=HighLogic.SaveFolder;c.Ship.loaded=false;Yes(!c.Check());c.Ship.loaded=true;c.Ship.packed=true;Yes(!c.Check());c.Ship.packed=false;c.Ship.isEVA=true;Yes(!c.Check());c.Ship.isEVA=false;
   var originalGame=HighLogic.CurrentGame;HighLogic.CurrentGame=new Game();Yes(!c.Check());c.loadedGame=HighLogic.CurrentGame;Yes(c.Check());HighLogic.CurrentGame=originalGame;c.loadedGame=originalGame;
   var sceneData=HighLogic.CurrentGame.scenarios;HighLogic.CurrentGame.scenarios=null;Yes(!c.Check());HighLogic.CurrentGame.scenarios=sceneData;
   var pending=new ProtoScenarioModule{moduleName="HermesCampaign"};HighLogic.CurrentGame.scenarios.Add(pending);Yes(!c.Check());pending.moduleRef=new object();Yes(!c.Check());HighLogic.CurrentGame.scenarios.Clear();Yes(c.Check());
   HighLogic.CurrentGame.scenarios.Add(new ProtoScenarioModule{moduleName="HermesCampaign",moduleRef=new HermesCampaign()});HighLogic.CurrentGame.scenarios.Add(new ProtoScenarioModule{moduleName="HermesCampaign",moduleRef=new HermesCampaign()});Yes(!c.Check());HighLogic.CurrentGame.scenarios.Clear();
   string reason;Yes(!CanPerformDuty("Hamish Kerman",Guid.NewGuid(),out reason));Yes(!CanPerformDuty("Unknown",c.Ship.id,out reason));
   CryoBridge.Verified=false;Yes(!c.Check());CryoBridge.Verified=true;CryoBridge.Frozen=true;Yes(!c.Check());CryoBridge.Frozen=false;c.aboard=false;Yes(!c.Check());c.aboard=true;
   c.crew[0].Resting=true;Yes(!c.Check());c.crew[0].Resting=false;c.crew[0].Injury="Minor sprain";Yes(!c.Check());Yes(NeedsModel.CareReady(c.crew[0]));c.crew[0].Treated=true;Yes(!c.Check());c.crew[0].Injury="None";
   c.crew[0].Hunger=90;Yes(!c.Check());c.crew[0].Hunger=0;c.crew[0].Thirst=90;Yes(!c.Check());c.crew[0].Thirst=0;c.crew[0].Fatigue=90;Yes(!c.Check());c.crew[0].Fatigue=0;
   c.crew[0].Hygiene=10;Yes(!c.Check());c.crew[0].Hygiene=100;c.crew[0].Conditioning=10;Yes(!c.Check());c.crew[0].Conditioning=100;c.crew[0].Morale=10;Yes(!c.Check());c.crew[0].Morale=100;Yes(c.Check());
   c.Campaign=new HermesCampaign{Started=true,VesselId=c.Ship.id};Yes(!c.Check());c.Campaign.CrewSnapshot=new List<CrewRecord>();Yes(!c.Check());c.Campaign.CrewSnapshot=c.crew;Yes(c.Check());
   c.Campaign.VesselId=Guid.NewGuid();Yes(!c.Check());c.Campaign.VesselId=c.Ship.id;c.Campaign.LoadError="Invalid save";Yes(!c.Check());c.Campaign.LoadError="";c.Campaign.SupplyError="Unknown interval";Yes(!c.Check());c.Campaign.SupplyError="";Yes(c.Check());
   dutyConsole=null;Yes(!CanPerformDuty("Hamish Kerman",c.Ship.id,out reason));
   Console.WriteLine("PASS: production local duty query rejects stale/unbound crew, unknown cryo, rest/injury/needs, wrong vessel and uncertain campaign state without changing crew data. KSP boundaries doubled; no multiplayer authority.");
  }
 }
}
