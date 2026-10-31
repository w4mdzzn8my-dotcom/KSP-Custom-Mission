using System;
using UnityEngine;

namespace MegaCrew
{
    public sealed partial class MissionConsole
    {
        const string ConsoleLockId="MegaCrew.ConsoleInput";
        const string ConsoleFieldPrefix="MegaCrew.ConsoleField.";
        bool consoleControlsLocked;
        bool consoleHasFocus=true;
        bool consoleFieldFocused;
        bool PointerOverConsole()
        {
            var p=new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y);
            return (visible&&window.Contains(p)) ||
                (Allowed&&interiorOpen&&interiorWindow.Contains(p)) ||
                new Rect(12,Screen.height-95,165,34).Contains(p);
        }
        void ClearConsoleFocusOutside()
        {
            if(Event.current!=null&&Event.current.type==EventType.MouseDown&&!PointerOverConsole()&&
                (GUI.GetNameOfFocusedControl()??"").StartsWith(ConsoleFieldPrefix,StringComparison.Ordinal))
                GUI.FocusControl(null);
        }
        void RefreshConsoleGuiFocus()
        {
            consoleFieldFocused=(GUI.GetNameOfFocusedControl()??"").StartsWith(ConsoleFieldPrefix,StringComparison.Ordinal);
            UpdateConsoleInputLock();
        }
        void UpdateConsoleInputLock()
        {
            if(!HighLogic.LoadedSceneIsFlight)consoleFieldFocused=false;
            bool field=visible&&consoleFieldFocused;
            bool required=consoleHasFocus&&HighLogic.LoadedSceneIsFlight&&(PointerOverConsole()||field);
            if(required&&!consoleControlsLocked)
            {
                InputLockManager.SetControlLock(ControlTypes.ALL_SHIP_CONTROLS,ConsoleLockId);
                consoleControlsLocked=true;
            }
            else if(!required)ReleaseConsoleInputLock();
        }
        void ReleaseConsoleInputLock()
        {
            if(!consoleControlsLocked)return;
            InputLockManager.RemoveControlLock(ConsoleLockId);
            consoleControlsLocked=false;
        }
        void OnApplicationFocus(bool focused){consoleHasFocus=focused;if(!focused)ReleaseConsoleInputLock();}
        void OnDisable(){ReleaseConsoleInputLock();}
        void OnDestroy(){ReleaseConsoleInputLock();}
    }
}
