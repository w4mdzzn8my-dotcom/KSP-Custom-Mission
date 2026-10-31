using System;
using MegaCrew;
class NeedsModelTests
{
    static void Equal(double a,double b){if(Math.Abs(a-b)>1e-7)throw new Exception("Expected "+b+", got "+a);}
    static void Yes(bool value){if(!value)throw new Exception("Condition failed");}
    static void TestDailyRoutine()
    {
        double epoch=10000,day=86400;
        double[] rest=new double[2];
        for(int h=0;h<24;h++)
        {
            bool a=DailyRoutine.RestingAt(epoch,epoch+h*3600,0),b=DailyRoutine.RestingAt(epoch,epoch+h*3600,1);
            Yes(!(a&&b));if(a)rest[0]++;if(b)rest[1]++;
        }
        Equal(rest[0],8);Equal(rest[1],8);
        var whole=new CrewRecord("test","test");var split=new CrewRecord("test","test");
        whole.Fatigue=split.Fatigue=40;whole.Hunger=split.Hunger=50;whole.Thirst=split.Thirst=50;
        whole.Hygiene=split.Hygiene=60;whole.Conditioning=split.Conditioning=60;whole.Morale=split.Morale=60;
        DailyRoutine.Advance(whole,epoch,epoch,epoch+2*day,0,false,true,1,1);
        for(int i=0;i<2880;i++)DailyRoutine.Advance(split,epoch,epoch+i*60,epoch+(i+1)*60,0,false,true,1,1);
        Equal(whole.Hunger,split.Hunger);Equal(whole.Thirst,split.Thirst);Equal(whole.Fatigue,split.Fatigue);Equal(whole.Hygiene,split.Hygiene);Equal(whole.Conditioning,split.Conditioning);Equal(whole.Morale,split.Morale);
        Yes(whole.Fatigue<90);Yes(whole.Hunger<50);Yes(whole.Morale>60);
        var lone=new CrewRecord("test","test");DailyRoutine.Advance(lone,epoch,epoch,epoch+4*3600,0,false,false,1,1);Yes(!lone.Resting);Yes(lone.Fatigue>0);
        var hungry=new CrewRecord("test","test");DailyRoutine.Advance(hungry,epoch,epoch,epoch+day,1,false,false,0,0);Equal(hungry.Hunger,100);Equal(hungry.Thirst,100);Yes(hungry.Morale<100);
        var frozen=new CrewRecord("test","test");frozen.Fatigue=100;DailyRoutine.Advance(frozen,epoch,epoch,epoch+4*30*day,0,true,false,0,0);Equal(frozen.Hunger,0);Equal(frozen.Hygiene,100);Equal(frozen.Fatigue,0);
        bool reject=false;try{DailyRoutine.Advance(whole,epoch,epoch,epoch-1,0,false,true,1,1);}catch(ArgumentOutOfRangeException){reject=true;}Yes(reject);
        Console.WriteLine("PASS: staggered eight-hour rest, continuous awake coverage, real-UT daily routines, frame partitioning, finite supply benefit and frozen exclusion.");
    }
    static void TestSnapshot()
    {
        var crew=new System.Collections.Generic.List<CrewRecord>();
        foreach(string name in new[]{"Hamish Kerman","Basil Kerman","Ada Kerman","Vikram Kerman"})crew.Add(new CrewRecord(name,"test"));
        var c=crew[0];c.Hunger=44.5;c.Thirst=62.25;c.Fatigue=12;c.Resting=true;c.Injury="Training sprain";c.Treated=true;c.Recovery=17;c.Hygiene=33;c.Conditioning=47;c.Morale=51;c.HygieneDoneUT=1234.56789;
        var root=new ConfigNode();CrewStateCodec.Save(root,crew);
        var old=System.Globalization.CultureInfo.CurrentCulture;System.Globalization.CultureInfo.CurrentCulture=new System.Globalization.CultureInfo("fr-FR");
        var restored=CrewStateCodec.Load(root);System.Globalization.CultureInfo.CurrentCulture=old;
        Equal(restored[0].Hunger,44.5);Equal(restored[0].Thirst,62.25);Equal(restored[0].Recovery,17);Equal(restored[0].Hygiene,33);Equal(restored[0].Conditioning,47);Equal(restored[0].Morale,51);Equal(restored[0].HygieneDoneUT,1234.56789);Yes(restored[0].Resting&&restored[0].Treated);Yes(restored[0].Injury=="Training sprain");
        c.Hunger=0;Equal(restored[0].Hunger,44.5); // no shared mutable alias on load
        root.GetNodes("CAMPAIGN_CREW")[0].Values["hunger"]="NaN";
        bool rejected=false;try{CrewStateCodec.Load(root);}catch(FormatException){rejected=true;}Yes(rejected);
        root.GetNodes("CAMPAIGN_CREW")[0].Values["hunger"]="44.5";root.GetNodes("CAMPAIGN_CREW")[1].Values["name"]="Hamish Kerman";
        rejected=false;try{CrewStateCodec.Load(root);}catch(FormatException){rejected=true;}Yes(rejected);
        Console.WriteLine("PASS: campaign crew snapshot round-trip, locale independence, restoration isolation and malformed/duplicate crew rejection (ConfigNode test double).");
    }
    static void Main()
    {
        var c=new CrewRecord("test","test");NeedsModel.Tick(c,3600,false);Equal(c.Hunger,100.0/6);Equal(c.Thirst,25);Equal(c.Fatigue,25);
        var split=new CrewRecord("test","test");for(int i=0;i<3600;i++)NeedsModel.Tick(split,1,false);Equal(c.Hunger,split.Hunger);Equal(c.Thirst,split.Thirst);Equal(c.Fatigue,split.Fatigue);
        NeedsModel.Tick(c,21600,false);Equal(c.Hunger,100);Equal(c.Thirst,100);Equal(c.Fatigue,100);
        c=new CrewRecord("test","test");c.Resting=true;c.Fatigue=100;NeedsModel.Tick(c,7200,false);Equal(c.Fatigue,0);Equal(c.Hunger,100.0/3);Equal(c.Thirst,50);
        c.Resting=true;c.Hunger=10;c.Thirst=20;c.Fatigue=100;NeedsModel.Tick(c,7200,true);Equal(c.Hunger,10);Equal(c.Thirst,20);Equal(c.Fatigue,0);Yes(!c.Resting);
        c.Hunger=89.9;c.Thirst=89.9;c.Fatigue=89.9;Yes(NeedsModel.Fit(c));c.Hunger=90;Yes(!NeedsModel.Fit(c));c.Hunger=0;c.Thirst=90;Yes(!NeedsModel.Fit(c));c.Thirst=0;c.Fatigue=90;Yes(!NeedsModel.Fit(c));c.Fatigue=0;c.Resting=true;Yes(!NeedsModel.Fit(c));Yes(!NeedsModel.Fit(null));
        c=new CrewRecord("test","test");NeedsModel.Tick(c,86400,false);Equal(c.Hygiene,75);Equal(c.Conditioning,85);Equal(c.Morale,90);
        c=new CrewRecord("test","test");NeedsModel.Tick(c,86400,true);Equal(c.Hygiene,100);Equal(c.Conditioning,100);Equal(c.Morale,100);
        c.Hygiene=10;Yes(!NeedsModel.Fit(c));Yes(NeedsModel.CareReady(c));c.Hygiene=100;c.Conditioning=10;Yes(!NeedsModel.Fit(c));c.Conditioning=100;c.Morale=10;Yes(!NeedsModel.Fit(c));
        c=new CrewRecord("test","test");c.Injury="Minor sprain";Yes(!NeedsModel.Fit(c));Yes(NeedsModel.CareReady(c));c.Treated=true;Yes(!NeedsModel.Fit(c));c.Injury="None";Yes(NeedsModel.Fit(c));
        bool rejected=false;try{NeedsModel.Tick(c,double.NaN,false);}catch(ArgumentOutOfRangeException){rejected=true;}Yes(rejected);
        TestSnapshot();
        TestDailyRoutine();
        Console.WriteLine("PASS: rates, frame partitioning, upper limits, ordinary sleep, frozen needs and duty thresholds.");
    }
}

// Only the ConfigNode boundary is doubled; CrewStateCodec is production source.
public class ConfigNode
{
    public System.Collections.Generic.Dictionary<string,string> Values=new System.Collections.Generic.Dictionary<string,string>();
    System.Collections.Generic.List<ConfigNode> Children=new System.Collections.Generic.List<ConfigNode>();
    string Name;
    public ConfigNode AddNode(string name){var n=new ConfigNode{Name=name};Children.Add(n);return n;}
    public void AddValue(string key,object value){Values[key]=value.ToString();}
    public string GetValue(string key){return Values.ContainsKey(key)?Values[key]:null;}
    public ConfigNode[] GetNodes(string name){return Children.FindAll(n=>n.Name==name).ToArray();}
}
