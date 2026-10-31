using System;
using System.Collections.Generic;
using MegaCrew;
class CampaignEpochPlannerTests
{
 static int assertions;static void Yes(bool v){assertions++;if(!v)throw new Exception("Epoch planner assertion failed at assertion "+assertions);}
 static bool Covered(double epoch,List<BurnDutyInterval> burns){foreach(var b in burns)if(!CampaignTiming.CoversBurn(epoch,b.StartUT,b.EndUT-b.StartUT,0,0))return false;return true;}
 static bool InWindows(double epoch,List<CampaignEpochWindow> windows){foreach(var w in windows)if(w.Contains(epoch))return true;return false;}
 static void Reject(Action f){try{f();}catch(ArgumentOutOfRangeException){return;}throw new Exception("Invalid schedule input accepted");}
 static void Main()
 {
  double w=CampaignTiming.WatchSeconds;
  var b=new List<BurnDutyInterval>{new BurnDutyInterval(w+100,60,120,90)};
  var windows=CampaignEpochPlanner.Find(0,w,b);Yes(windows.Count>0);Yes(!InWindows(0,windows));Yes(InWindows(w-100,windows));
  foreach(var range in windows){Yes(Covered(range.EarliestUT,b));Yes(Covered(range.LatestUT,b));}
  b=new List<BurnDutyInterval>{new BurnDutyInterval(100,10,0,0),new BurnDutyInterval(w+100,10,0,0)};
  Yes(CampaignEpochPlanner.Find(0,100,b).Count==0); // cannot cover two burns separated by exactly one watch
  b=new List<BurnDutyInterval>{new BurnDutyInterval(100,w+1,0,0)};Yes(CampaignEpochPlanner.Find(0,100,b).Count==0);
  var random=new Random(2910);int samples=0;
  for(int scenario=0;scenario<100;scenario++)
  {
   b=new List<BurnDutyInterval>();int count=random.Next(1,7);
   for(int i=0;i<count;i++)b.Add(new BurnDutyInterval(10000+random.NextDouble()*8*w,1+random.NextDouble()*10000,random.NextDouble()*1000,random.NextDouble()*1000));
   windows=CampaignEpochPlanner.Find(0,2*w,b);
   foreach(var range in windows){Yes(range.EarliestUT>=0&&range.LatestUT<=2*w);Yes(Covered(range.EarliestUT,b));Yes(Covered(range.LatestUT,b));}
   for(int i=0;i<=200;i++){double epoch=2*w*i/200;Yes(InWindows(epoch,windows)==Covered(epoch,b));samples++;}
  }
  var none=CampaignEpochPlanner.Find(5,10,new BurnDutyInterval[0]);Yes(none.Count==1&&none[0].EarliestUT==5&&none[0].LatestUT==10);
  Reject(()=>CampaignEpochPlanner.Find(10,5,new BurnDutyInterval[0]));Reject(()=>CampaignEpochPlanner.Find(0,double.NaN,new BurnDutyInterval[0]));Reject(()=>CampaignEpochPlanner.Find(0,10,null));
  Reject(()=>new BurnDutyInterval(10,0,0,0));Reject(()=>new BurnDutyInterval(10,1,20,0));Reject(()=>CampaignEpochPlanner.Find(0,100,new[]{default(BurnDutyInterval)}));
  b=new List<BurnDutyInterval>();for(int i=0;i<129;i++)b.Add(new BurnDutyInterval(10,1,0,0));Reject(()=>CampaignEpochPlanner.Find(0,10,b));
  Console.WriteLine("PASS: physical burn epochs unchanged; full-duty-window schedule intersections, conflicts, boundaries and "+samples+" independent CoversBurn samples. Pre-start model only; no trajectory or KSP clock edits.");
 }
}
