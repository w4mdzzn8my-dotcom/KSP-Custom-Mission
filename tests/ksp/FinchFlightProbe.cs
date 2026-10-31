// Disposable local test only: active only when an explicit marker exists.
using System;
using System.Collections;
using System.IO;
using UnityEngine;
[KSPAddon(KSPAddon.Startup.MainMenu, false)]
public sealed class FinchFlightStart : MonoBehaviour
{
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(5);
        var root=KSPUtil.ApplicationRootPath;
        var marker=Path.Combine(root,"FinchFlightQA.enable");
        if(!File.Exists(marker))yield break;
        // Consume marker before changing scenes so a restart never repeats the launch.
        File.Move(marker,Path.Combine(root,"FinchFlightQA.consumed"));
        try {
            const string folder="MegaCrew Finch QA";
            var game=GamePersistence.LoadGame("persistent",folder,true,false);
            if(game==null)throw new Exception("QA save could not be loaded");
            HighLogic.SaveFolder=folder;HighLogic.CurrentGame=game;
            var path=Path.Combine(root,"Ships","VAB","Hermes Finch Docking Cradle.craft");
            var manifest=VesselCrewManifest.FromConfigNode(ConfigNode.Load(path));
            Debug.Log("[FinchFlightQA] Starting disposable launch from "+folder);
            FlightDriver.StartWithNewLaunch(path,"Squad/Flags/default","LaunchPad",manifest);
        } catch(Exception e) { Debug.LogError("[FinchFlightQA] Launch failed: "+e); }
    }
}
[KSPAddon(KSPAddon.Startup.Flight, false)]
public sealed class FinchFlightCheck : MonoBehaviour
{
    IEnumerator Start()
    {
        if(HighLogic.SaveFolder!="MegaCrew Finch QA")yield break;
        yield return new WaitForSecondsRealtime(5);
        var v=FlightGlobals.ActiveVessel;
        if(v==null){Debug.LogError("[FinchFlightQA] No active vessel");yield break;}
        Debug.Log("[FinchFlightQA] Flight vessel="+v.vesselName+" parts="+v.parts.Count+" situation="+v.situation);
        for(int i=0;i<v.parts.Count;i++) {
            var p=v.parts[i];
            foreach(PartModule m in p.Modules) if(m.moduleName=="ModuleDockingNode") {
                var node=m as ModuleDockingNode;
                var decouple=m.Events["Decouple"];var undock=m.Events["Undock"];
                Debug.Log("[FinchFlightQA] DOCK index="+i+" parent="+(p.parent==null?-1:v.parts.IndexOf(p.parent))+" state="+node.state+" decouple="+(decouple!=null&&decouple.active)+" undock="+(undock!=null&&undock.active));
            }
        }
    }
}
