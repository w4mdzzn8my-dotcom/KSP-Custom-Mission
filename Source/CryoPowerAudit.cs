using System;
using System.Reflection;
using UnityEngine;
namespace MegaCrew
{
    public static partial class CryoAudit
    {
        public static bool Read<T>(object obj,string name,out T result)
        {
            result=default(T);if(obj==null)return false;
            try
            {
                var p=obj.GetType().GetProperty(name,BindingFlags.Public|BindingFlags.Instance);
                if(p==null)return false;var value=p.GetValue(obj,null);
                if(!(value is T))return false;result=(T)value;return true;
            }catch{return false;}
        }
    }
    public sealed partial class MissionConsole
    {
        void DrawCryoPowerAudit()
        {
            GUILayout.Space(10);GUILayout.Label("CRYOPOD POWER PREFLIGHT / LIVE READ-ONLY");
            if(!HighLogic.LoadedSceneIsFlight||Ship==null){GUILayout.Label("Enter flight with Hermes to read its real pod state.");return;}
            bool requiresEC,fatal;
            if(!CryoBridge.TryPowerPolicy(out requiresEC,out fatal)){GUILayout.Label("DeepFreeze policy unavailable. Cryo coast cannot be qualified.");return;}
            GUILayout.Label("DeepFreeze EC requirement: "+requiresEC+" | Fatal consequence setting: "+fatal+". This panel changes neither setting.");
            double load=0,stored=0,capacity=0,constantRatedOutput=0;int pods=0,frozen=0;bool verified=true;
            foreach(var part in Ship.parts)
            {
                foreach(PartResource r in part.Resources)if(r.resourceName=="ElectricCharge"&&r.flowState){stored+=r.amount;capacity+=r.maxAmount;}
                foreach(PartModule m in part.Modules)
                {
                    if(m.moduleName=="DeepFreezer")
                    {
                        pods++;int count=0,perMinute=0;bool outOfEC=false,freezing=false,thawing=false;object thermal=null;
                        bool ok=CryoAudit.Read(m,"DFITotalFrozen",out count)&&CryoAudit.Read(m,"DFIFrznChargeRequired",out perMinute)&&CryoAudit.Read(m,"DFIFreezerOutofEC",out outOfEC)&&CryoAudit.Read(m,"DFIIsFreezeActive",out freezing)&&CryoAudit.Read(m,"DFIIsThawActive",out thawing)&&CryoAudit.Read(m,"DFIFrzrTmp",out thermal);
                        if(!ok){verified=false;GUILayout.Label(part.partInfo.title+": required public API unavailable.");continue;}
                        if(count<0||perMinute<0){verified=false;continue;}
                        frozen+=count;load+=CryoPowerModel.PodLoad(perMinute,count);
                        if(outOfEC||freezing||thawing)verified=false;
                        GUILayout.Label(part.partInfo.title+" #"+part.flightID+" | Frozen "+count+" | "+perMinute+" EC/min/crew | Out of EC "+outOfEC+" | Thermal status "+thermal+" | Part "+part.temperature.ToString("F1")+" K");
                    }
                    var generator=m as ModuleGenerator;
                    if(generator!=null&&generator.generatorIsActive&&generator.isAlwaysActive&&!generator.isThrottleControlled&&generator.resHandler.inputResources.Count==0)
                        foreach(var output in generator.resHandler.outputResources)if(output.name=="ElectricCharge"&&output.rate>0)constantRatedOutput+=output.rate;
                }
            }
            GUILayout.Label("Accessible battery: "+stored.ToString("F0")+" / "+capacity.ToString("F0")+" EC | Cryopods "+pods+" | Frozen "+frozen+" | Rated pod load "+load.ToString("F3")+" EC/s");
            GUILayout.Label("Enabled constant-generator rated output: "+constantRatedOutput.ToString("F3")+" EC/s. This is a nameplate readout, not verified net generation or a complete load budget. Variable solar output is excluded.");
            if(load>0)GUILayout.Label("Battery endurance for pods ONLY, zero generation: "+(CryoPowerModel.TimeToReserve(stored,0,load,0)/60).ToString("F1")+" min. Other loads shorten this; it does not authorize coast.");
            GUILayout.Label(verified&&pods>0?"Public pod telemetry readable. Runtime/background qualification still pending.":"Pod telemetry/state is incomplete or busy; cryo coast blocked.");
            GUILayout.Label("Pinned DeepFreeze 0.31.0.0 skips ongoing EC checks at warp index >=5 and thermal checks at index >=2. Moving huge custom rates into a lower index is not qualification. Background EC/thermal ownership and double-charge prevention must be tested before accelerated cryosleep.");
        }
    }
}
