using System;
using System.Collections;
using System.IO;
using UnityEngine;
[KSPAddon(KSPAddon.Startup.MainMenu, false)]
public sealed class LanderLoaderProbe : MonoBehaviour
{
    IEnumerator Start()
    {
        Debug.Log("[FinchQA] probe started"); yield return new WaitForSecondsRealtime(5);
        foreach (var m in typeof(FlightDriver).GetMethods(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static))
            if (m.Name.Contains("Start") || m.Name.Contains("Focus")) Debug.Log("[FinchQA] API " + m);
        foreach (var m in typeof(GamePersistence).GetMethods(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static))
            if (m.Name.Contains("Load")) Debug.Log("[FinchQA] API " + m);
        foreach (var name in new[] { "Hermes Finch Vacuum Lander", "Hermes Finch Docking Cradle" })
        {
            ShipConstruct craft = null;
            try
            {
                Debug.Log("[FinchQA] BEGIN "+name); craft = ShipConstruction.LoadShip(Path.Combine(KSPUtil.ApplicationRootPath,"Ships","VAB", name+".craft"));
                if(craft==null) throw new Exception("LoadShip returned null");
                double mass=0; int seats=0, docks=0, engines=0;
                foreach(var p in craft.parts) { mass+=p.mass+p.GetResourceMass(); seats+=p.CrewCapacity;foreach(PartModule m in p.Modules){if(m.moduleName=="ModuleDockingNode")docks++;if(m.moduleName.StartsWith("ModuleEngines"))engines++;} }
                Debug.Log("[FinchQA] LOADED "+name+" parts="+craft.parts.Count+" wetMass="+mass+" seats="+seats+" docks="+docks+" engines="+engines);
                for (int i=0; i<craft.parts.Count; i++) {
                    var p=craft.parts[i];
                    int parent=p.parent==null ? -1 : craft.parts.IndexOf(p.parent);
                    Debug.Log("[FinchQA] EDGE "+i+" "+p.partInfo.name+" parent="+parent);
                    foreach(PartModule m in p.Modules) if(m.moduleName=="ModuleDockingNode") {
                        var field=m.GetType().GetField("state");
                        Debug.Log("[FinchQA] DOCK "+i+" state="+(field==null?"unknown":Convert.ToString(field.GetValue(m))));
                    }
                }
            }
            catch(Exception ex) { Debug.LogError("[FinchQA] FAILED "+name+" "+ex); }
            finally { if(craft!=null)foreach(var p in craft.parts)if(p!=null)Destroy(p.gameObject); }
            yield return new WaitForSecondsRealtime(1);
        }
    }
}
