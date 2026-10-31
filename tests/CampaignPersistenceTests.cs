using System;
using MegaCrew;
public class ScenarioModule{public virtual void OnLoad(ConfigNode n){}public virtual void OnSave(ConfigNode n){}}
public class ConfigNode {
 public System.Collections.Generic.Dictionary<string,string> Values=new System.Collections.Generic.Dictionary<string,string>();
 System.Collections.Generic.List<ConfigNode> nodes=new System.Collections.Generic.List<ConfigNode>();string name;
 public void AddValue(string key,object value){Values[key]=value.ToString();}
 public string GetValue(string key){return Values.ContainsKey(key)?Values[key]:null;}
 public bool HasValue(string key){return Values.ContainsKey(key);}
 public bool HasNode(string key){return GetNodes(key).Length>0;}
 public ConfigNode AddNode(string key){var n=new ConfigNode{name=key};nodes.Add(n);return n;}
 public ConfigNode[] GetNodes(string key){return nodes.FindAll(n=>n.name==key).ToArray();}
 public void ClearData(){Values.Clear();nodes.Clear();}
 public ConfigNode CreateCopy(){var n=new ConfigNode();CopyTo(n);return n;}
 public void CopyTo(ConfigNode target){foreach(var pair in Values)target.AddValue(pair.Key,pair.Value);foreach(var child in nodes)child.CopyTo(target.AddNode(child.name));}
}
class CampaignPersistenceTests {
 static void Yes(bool x){if(!x)throw new Exception("Persistence assertion failed");}
 static ConfigNode Save(){var c=new HermesCampaign{Started=true,EpochUT=100,VesselId=Guid.NewGuid(),RationsEnabled=true,DailyRoutineEnabled=true,SupplyUT=130,FoodUsed=2.25,WaterUsed=1.5,FoodMissing=0.5,WaterMissing=0.25,SupplyDepleted=true,PreviousUnfrozen=2,UnfrozenMask=3};var n=new ConfigNode();c.OnSave(n);return n;}
 static HermesCampaign Load(ConfigNode n){var c=new HermesCampaign();c.OnLoad(n);return c;}
 public static void Main(){
  var n=Save();var c=Load(n);Yes(c.Started&&c.SupplyDepleted&&c.DailyRoutineEnabled&&c.SupplyError=="");Yes(c.FoodMissing==0.5&&c.FoodUsed==2.25&&c.SupplyUT==130);
  // A KSP reload restores recorded shortages without treating them as a new meal.
  n.Values.Remove("supplyDepleted");n.Values["supplyError"]="Routine rations depleted. Unmet supply demand recorded; coast blocked.";c=Load(n);Yes(c.SupplyDepleted&&c.SupplyError==""&&c.FoodMissing==0.5);
  n.Values["supplyError"]="Unobserved supply interval";c=Load(n);Yes(c.SupplyError=="Unobserved supply interval");
  n=Save();n.Values["supplyDepleted"]="garbled";Yes(!Load(n).Started&&Load(n).LoadError.Length>0);
  n=Save();n.Values["foodMissing"]="NaN";Yes(!Load(n).Started&&Load(n).LoadError.Length>0);
  n=Save();n.Values["supplyUT"]="99";Yes(!Load(n).Started&&Load(n).LoadError.Length>0);
  n=Save();n.Values["schema"]="99";c=Load(n);Yes(!c.Started&&c.LoadError.Length>0);
  foreach(var key in new[]{"started","rationsEnabled","dailyRoutineEnabled","supplyDepleted"}) {
   n=Save();n.Values[key]="garbled";n.AddValue("unknownFutureValue","keep");n.AddNode("UNKNOWN").AddValue("data","one");n.AddNode("UNKNOWN").AddValue("data","two");
   c=Load(n);Yes(!c.Started&&c.LoadError.Length>0);n.Values[key]="changed-after-load";
   var preserved=new ConfigNode();c.OnSave(preserved);Yes(preserved.GetValue(key)=="garbled");Yes(preserved.GetValue("unknownFutureValue")=="keep");Yes(preserved.GetNodes("UNKNOWN").Length==2);Yes(preserved.GetNodes("UNKNOWN")[1].GetValue("data")=="two");
   Yes(Load(preserved).LoadError.Length>0); // save/reload cannot normalize rejected data into a fresh campaign
  }
  n=Save();n.Values["rationsEnabled"]="False";Yes(Load(n).LoadError.Length>0); // routine without finite food plan
  n=Save();n.Values.Remove("rationsEnabled");Yes(Load(n).LoadError.Length>0); // ledger cannot silently become opted out
  n=Save();n.Values["started"]="False";n.Values["schema"]="99";c=Load(n);Yes(c.LoadError.Length>0);var future=new ConfigNode();c.OnSave(future);Yes(future.GetValue("schema")=="99");
  n=Save();n.Values.Remove("started");Yes(Load(n).LoadError.Length>0);
  var empty=new ConfigNode();empty.AddValue("name","HermesCampaign");empty.AddValue("scene","7,5,6");Yes(Load(empty).LoadError=="");var partial=new ConfigNode();partial.AddValue("rationsEnabled",false);Yes(Load(partial).LoadError.Length>0);
  c=Load(Save());var stopped=new ConfigNode();stopped.AddValue("schema",1);stopped.AddValue("started",false);c.OnLoad(stopped);Yes(!c.Started&&c.FoodUsed==0&&c.FoodMissing==0&&c.EpochUT==0&&c.VesselId==Guid.Empty);
  n=Save();foreach(var key in new[]{"rationsEnabled","dailyRoutineEnabled","supplyUT","foodUsed","waterUsed","foodMissing","waterMissing","supplyDepleted"})n.Values.Remove(key);c=Load(n);Yes(c.Started&&!c.RationsEnabled&&c.LoadError==""); // actual older pre-ration schema remains compatible
  Console.WriteLine("PASS: production scenario save/load depletion round-trip, legacy migration, retained unmet totals and malformed flags/history rejection and lossless rejected-node preservation (ConfigNode/ScenarioModule doubles).");
 }
}
