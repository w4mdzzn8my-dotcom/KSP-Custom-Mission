// One-shot disposable fixture; never include in the production DLL.
using System;
using System.Collections;
using System.IO;
using UnityEngine;
[KSPAddon(KSPAddon.Startup.MainMenu,false)]
public sealed class FinchResumeStart:MonoBehaviour
{
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(5);
        var root=KSPUtil.ApplicationRootPath;var marker=Path.Combine(root,"FinchResumeQA.enable");
        if(!File.Exists(marker))yield break;
        File.Move(marker,Path.Combine(root,"FinchResumeQA.consumed"));
        try {
            const string save="MegaCrew Finch Resume QA";
            var game=GamePersistence.LoadGame("persistent",save,true,false);
            if(game==null||game.flightState.protoVessels.Count<2)throw new Exception("Expected two QA vessels");
            HighLogic.SaveFolder=save;HighLogic.CurrentGame=game;
            Debug.Log("[FinchResumeQA] Resuming saved vessel index 1");
            FlightDriver.StartAndFocusVessel(game,1);
        }catch(Exception e){Debug.LogError("[FinchResumeQA] Start failed: "+e);}
    }
}
[KSPAddon(KSPAddon.Startup.Flight,false)]
public sealed class FinchResumeCheck:MonoBehaviour
{
    IEnumerator Start()
    {
        if(HighLogic.SaveFolder!="MegaCrew Finch Resume QA")yield break;
        yield return new WaitForSecondsRealtime(10);
        var v=FlightGlobals.ActiveVessel;
        if(v==null){Debug.LogError("[FinchResumeQA] No active vessel");yield break;}
        Debug.Log("[FinchResumeQA] VESSEL "+v.vesselName+" parts="+v.parts.Count+" situation="+v.situation+" Pe="+v.orbit.PeA+" Ap="+v.orbit.ApA);
        foreach(var p in v.parts)foreach(PartModule m in p.Modules)if(m.moduleName=="ModuleDockingNode"){
            var d=m as ModuleDockingNode;var a=m.Events["Decouple"];var b=m.Events["Undock"];
            Debug.Log("[FinchResumeQA] DOCK id="+p.flightID+" state="+d.state+" decouple="+(a!=null&&a.active)+" undock="+(b!=null&&b.active));
        }
        yield return new WaitForSecondsRealtime(20);v=FlightGlobals.ActiveVessel;
        if(v!=null)Debug.Log("[FinchResumeQA] AFTER20 "+v.vesselName+" parts="+v.parts.Count+" situation="+v.situation);
    }
}
