using System;
using System.Collections.Generic;
using MegaCrew;
namespace UnityEngine {public static class Debug{public static void LogError(string s){}}}
public class Person {public string name;}
public class PartModule {public string moduleName="DeepFreezer";}
public class Pod : PartModule {
 public bool freeze,thaw,to,from,throwRead;public int freezeCalls,thawCalls;
 public bool DFIIsFreezeActive{get{if(throwRead)throw new Exception("Unavailable pod");return freeze;}}
 public bool DFIIsThawActive{get{return thaw;}}public bool DFIcrewXferTOActive{get{return to;}}public bool DFIcrewXferFROMActive{get{return from;}}
 public void beginFreezeKerbal(Person p){freezeCalls++;}public void beginThawKerbal(string n){thawCalls++;}
}
public class Part {public List<PartModule> Modules=new List<PartModule>();public List<Person> protoModuleCrew=new List<Person>();public uint flightID=1;public int CrewCapacity=4;}
public class Vessel {public Guid id=Guid.NewGuid();public bool loaded=true,packed;public List<Part> parts=new List<Part>();}
public static class FlightGlobals {public static Vessel ActiveVessel;}
namespace DF {
 public class Info {public Guid vesselID;public uint partID=1;}
 public class DeepFreeze {
  public static bool APIReady;public static DeepFreeze Instance;public bool failDictionary;
  public System.Collections.IDictionary crew=new Dictionary<string,Info>();
  public System.Collections.IDictionary FrozenKerbals{get{if(failDictionary)throw new Exception("Unavailable dictionary");return crew;}}
  public bool DFIECReqd{get{return true;}}public bool DFIDeathFatal{get{return false;}}
 }
}
class CryoReadinessTests {
 static void Yes(bool b){if(!b)throw new Exception("Cryo boundary assertion failed");}
 static void Main(){
  var v=new Vessel();FlightGlobals.ActiveVessel=v;var p=new Part();v.parts.Add(p);var pod=new Pod();p.Modules.Add(pod);p.protoModuleCrew.Add(new Person{name="Ada"});
  DF.DeepFreeze.Instance=new DF.DeepFreeze();bool frozen,required,fatal;
  Yes(!CryoBridge.TryFrozen("Ada",out frozen));Yes(CryoBridge.Busy);Yes(!CryoBridge.Freeze("Ada"));Yes(!CryoBridge.TryPowerPolicy(out required,out fatal));Yes(pod.freezeCalls==0);
  DF.DeepFreeze.APIReady=true;Yes(CryoBridge.TryFrozen("Ada",out frozen)&&!frozen);Yes(!CryoBridge.Busy);Yes(CryoBridge.TryPowerPolicy(out required,out fatal)&&required&&!fatal);
  pod.to=true;Yes(CryoBridge.Busy);Yes(!CryoBridge.Freeze("Ada"));pod.to=false;pod.from=true;Yes(CryoBridge.Busy);pod.from=false;
  pod.freeze=true;Yes(CryoBridge.Busy);pod.freeze=false;pod.thaw=true;Yes(CryoBridge.Busy);pod.thaw=false;
  pod.throwRead=true;Yes(CryoBridge.Busy);pod.throwRead=false;p.Modules.Add(new PartModule());Yes(CryoBridge.Busy);p.Modules.RemoveAt(1);
  v.packed=true;Yes(CryoBridge.Busy);v.packed=false;v.loaded=false;Yes(CryoBridge.Busy);v.loaded=true;
  Yes(CryoBridge.Freeze("Ada")&&pod.freezeCalls==1);
  DF.DeepFreeze.Instance.crew["Ada"]=new DF.Info{vesselID=v.id};Yes(CryoBridge.TryFrozen("Ada",out frozen)&&frozen);
  pod.to=true;Yes(!CryoBridge.Thaw("Ada"));pod.to=false;Yes(CryoBridge.Thaw("Ada")&&pod.thawCalls==1);
  DF.DeepFreeze.Instance.crew["Ada"]=new DF.Info{vesselID=Guid.NewGuid()};Yes(!CryoBridge.TryFrozen("Ada",out frozen));Yes(!CryoBridge.Thaw("Ada"));
  DF.DeepFreeze.Instance.failDictionary=true;Yes(!CryoBridge.TryFrozen("Ada",out frozen));DF.DeepFreeze.Instance.failDictionary=false;
  DF.DeepFreeze.Instance=null;Yes(CryoBridge.Busy);Yes(!CryoBridge.TryFrozen("Ada",out frozen));Yes(!CryoBridge.Freeze("Ada"));
  Yes(pod.freezeCalls==1&&pod.thawCalls==1);
  Console.WriteLine("PASS: production reflection bridge rejects initializing API, transfer/busy/unknown pods, packed vessels and foreign crew; only ready requests reach recorded API boundary. Not a real freeze/thaw test.");
 }
}
