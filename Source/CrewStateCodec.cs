using System;
using System.Collections.Generic;
using System.Globalization;
namespace MegaCrew
{
    // KSP scenario snapshot shares the same save/quicksave as vessel resources.
    public static class CrewStateCodec
    {
        static readonly string[] Names={"Hamish Kerman","Basil Kerman","Ada Kerman","Vikram Kerman"};
        static void Number(ConfigNode n,string key,double value){n.AddValue(key,value.ToString("R",CultureInfo.InvariantCulture));}
        static double Read(ConfigNode n,string key,double max)
        {
            double v;
            if(!double.TryParse(n.GetValue(key),NumberStyles.Float,CultureInfo.InvariantCulture,out v)||double.IsNaN(v)||double.IsInfinity(v)||v<0||v>max)throw new FormatException("Invalid crew metric");
            return v;
        }
        static double Stamp(ConfigNode n,string key)
        {
            double v;if(!double.TryParse(n.GetValue(key),NumberStyles.Float,CultureInfo.InvariantCulture,out v)||double.IsNaN(v)||double.IsInfinity(v)||v< -1)throw new FormatException("Invalid care timestamp");return v;
        }
        public static void Save(ConfigNode root,List<CrewRecord> crew)
        {
            foreach(var c in crew)
            {
                var n=root.AddNode("CAMPAIGN_CREW");n.AddValue("name",c.Name);n.AddValue("role",c.Role);n.AddValue("injury",c.Injury);
                n.AddValue("resting",c.Resting);n.AddValue("treated",c.Treated);
                Number(n,"hunger",c.Hunger);Number(n,"thirst",c.Thirst);Number(n,"fatigue",c.Fatigue);Number(n,"recovery",c.Recovery);
                Number(n,"hygiene",c.Hygiene);Number(n,"conditioning",c.Conditioning);Number(n,"morale",c.Morale);
                Number(n,"hygieneDoneUT",c.HygieneDoneUT);Number(n,"exerciseDoneUT",c.ExerciseDoneUT);Number(n,"socialDoneUT",c.SocialDoneUT);
            }
        }
        public static List<CrewRecord> Load(ConfigNode root)
        {
            var nodes=root.GetNodes("CAMPAIGN_CREW");if(nodes.Length!=4)throw new FormatException("Expected four crew");
            var result=new List<CrewRecord>();
            foreach(string name in Names)
            {
                ConfigNode found=null;
                foreach(var n in nodes)if(n.GetValue("name")==name){if(found!=null)throw new FormatException("Duplicate crew");found=n;}
                if(found==null)throw new FormatException("Missing crew");
                var c=new CrewRecord(name,found.GetValue("role")??"");
                c.Hunger=Read(found,"hunger",100);c.Thirst=Read(found,"thirst",100);c.Fatigue=Read(found,"fatigue",100);c.Recovery=Read(found,"recovery",double.MaxValue);
                c.Hygiene=Read(found,"hygiene",100);c.Conditioning=Read(found,"conditioning",100);c.Morale=Read(found,"morale",100);
                c.HygieneDoneUT=Stamp(found,"hygieneDoneUT");c.ExerciseDoneUT=Stamp(found,"exerciseDoneUT");c.SocialDoneUT=Stamp(found,"socialDoneUT");
                if(!bool.TryParse(found.GetValue("resting"),out c.Resting)||!bool.TryParse(found.GetValue("treated"),out c.Treated))throw new FormatException("Invalid crew flags");
                c.Injury=found.GetValue("injury")??"None";result.Add(c);
            }
            return result;
        }
    }
}
