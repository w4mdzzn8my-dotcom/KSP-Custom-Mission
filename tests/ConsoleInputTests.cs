// Exercise the production lock lifecycle against a recording KSP boundary.
using System;
using System.Collections.Generic;
namespace UnityEngine {
 public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}
 public struct Rect {float x,y,w,h;public Rect(float x,float y,float w,float h){this.x=x;this.y=y;this.w=w;this.h=h;}public bool Contains(Vector2 p){return p.x>=x&&p.x<x+w&&p.y>=y&&p.y<y+h;}}
 public static class Input {public static Vector2 mousePosition;}
 public static class Screen {public static int height=1000;}
 public enum EventType {MouseDown,Repaint}
 public class Event {public static Event current;public EventType type;}
 public static class GUI {public static string focus="";public static string GetNameOfFocusedControl(){return focus;}public static void FocusControl(string name){focus=name;}}
}
public static class HighLogic {public static bool LoadedSceneIsFlight=true;}
public enum ControlTypes {ALL_SHIP_CONTROLS}
public static class InputLockManager {
 public static readonly HashSet<string> locks=new HashSet<string>();public static int sets,removes;
 public static void SetControlLock(ControlTypes controls,string id){locks.Add(id);sets++;}
 public static void RemoveControlLock(string id){locks.Remove(id);removes++;}
}
namespace MegaCrew {
 public sealed partial class MissionConsole {
  UnityEngine.Rect window=new UnityEngine.Rect(30,65,760,650),interiorWindow=new UnityEngine.Rect(790,70,410,560);
  bool visible=true,interiorOpen;bool Allowed=true;
  static void Assert(bool yes,string message){if(!yes)throw new Exception(message);}
  static void Pointer(float x,float guiY){UnityEngine.Input.mousePosition=new UnityEngine.Vector2(x,1000-guiY);}
  void Check(bool expected,string context){UpdateConsoleInputLock();Assert(InputLockManager.locks.Contains(ConsoleLockId)==expected,context);Assert(InputLockManager.locks.Contains("FreeIVA"),"Removed another mod's lock");}
  public static void Main(){
   InputLockManager.locks.Add("FreeIVA");var c=new MissionConsole();
   Pointer(500,800);c.Check(false,"outside");Pointer(40,100);c.Check(true,"main hover");int sets=InputLockManager.sets;c.Check(true,"held");Assert(sets==InputLockManager.sets,"Repeated lock acquisition");
   Pointer(500,800);c.Check(false,"leave panel");
   UnityEngine.GUI.focus=ConsoleFieldPrefix+"Radius";c.RefreshConsoleGuiFocus();c.Check(true,"typing outside panel");
   UnityEngine.Event.current=new UnityEngine.Event{type=UnityEngine.EventType.MouseDown};c.ClearConsoleFocusOutside();c.RefreshConsoleGuiFocus();c.Check(false,"outside click clears field");
   UnityEngine.GUI.focus="OtherMod.field";c.RefreshConsoleGuiFocus();c.Check(false,"other mod focus");
   Pointer(40,100);c.Check(true,"hover again");c.visible=false;c.Check(false,"close main");
   Pointer(800,100);c.interiorOpen=true;c.Check(true,"interior hover");c.Allowed=false;c.Check(false,"interior not permitted");c.Allowed=true;
   c.Check(true,"interior permitted");c.OnApplicationFocus(false);c.Check(false,"app loses focus");c.OnApplicationFocus(true);c.Check(true,"app returns");
   HighLogic.LoadedSceneIsFlight=false;c.Check(false,"scene exit");HighLogic.LoadedSceneIsFlight=true;c.Check(true,"scene return");
   c.OnDisable();Assert(!InputLockManager.locks.Contains(ConsoleLockId),"disable leaked lock");c.Check(true,"re-enabled");c.OnDestroy();Assert(!InputLockManager.locks.Contains(ConsoleLockId),"destroy leaked lock");
   c.interiorOpen=false;Pointer(20,920);c.Check(true,"launcher button hover");Pointer(500,800);c.Check(false,"leave launcher");
   Assert(InputLockManager.locks.Count==1,"foreign lock altered");Console.WriteLine("PASS: production console hover, focus, hide, scene and lifecycle lock checks; FreeIVA lock preserved.");
  }
 }
}
