using System;
using System.Globalization;
using UnityEngine;
using KSP.UI.Screens;

namespace MegaCrew.Navigation
{
    /// <summary>Read-only flight UI for the detached circular lower-periapsis preview.</summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class NavigationPreviewPanel : MonoBehaviour
    {
        const string InputLockId = "MegaCrewNavigationPreviewPanel";
        const double MaxEccentricity = 0.001;
        const double PeriapsisToleranceMetres = 100.0;
        const double PositionChangeToleranceMetres = 0.01;
        const double VelocityChangeToleranceMetresPerSecond = 0.000001;

        ApplicationLauncherButton launcherButton;
        Texture2D launcherIcon;
        bool visible;
        bool keyboardLocked;
        Rect window = new Rect(140, 120, 430, 400);
        string targetPeriapsisKm = "80";
        string leadSeconds = "120";
        string status = "Enter target periapsis and lead time, then refresh the preview.";
        CircularLowerPeriapsisProposal proposal;
        PreviewSnapshot snapshot;

        sealed class PreviewSnapshot
        {
            public Guid VesselId;
            public Orbit Orbit;
            public CelestialBody Body;
            public string TargetText;
            public string LeadText;
            public global::Vector3d PositionAtBurn;
            public global::Vector3d VelocityAtBurn;
            public Orbit.PatchTransitionType EndTransition;
        }

        void Start()
        {
            GameEvents.onGUIApplicationLauncherReady.Add(OnLauncherReady);
            GameEvents.onGUIApplicationLauncherDestroyed.Add(OnLauncherDestroyed);
            if (ApplicationLauncher.Ready) OnLauncherReady();
        }

        void OnLauncherReady()
        {
            if (launcherButton != null || ApplicationLauncher.Instance == null) return;
            CreateIcon();
            launcherButton = ApplicationLauncher.Instance.AddModApplication(
                OnToolbarOn, OnToolbarOff, null, null, null, null,
                ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW,
                launcherIcon);
        }

        void OnLauncherDestroyed()
        {
            launcherButton = null;
            visible = false;
            Invalidate("Flight UI closed; refresh after returning to flight.");
            ReleaseShipControlLock();
        }

        void OnToolbarOn() { visible = true; }
        void OnToolbarOff() { visible = false; ReleaseShipControlLock(); }

        void CreateIcon()
        {
            if (launcherIcon != null) return;
            launcherIcon = new Texture2D(38, 38, TextureFormat.RGBA32, false);
            launcherIcon.name = "MegaCrewNavigationPreviewIcon";
            launcherIcon.hideFlags = HideFlags.HideAndDontSave;
            var pixels = new Color32[38 * 38];
            for (int y = 0; y < 38; y++)
            {
                for (int x = 0; x < 38; x++)
                {
                    int dx = x - 19;
                    int dy = y - 19;
                    int r2 = dx * dx + dy * dy;
                    Color32 color = new Color32(0, 0, 0, 0);
                    if (r2 >= 100 && r2 <= 145) color = new Color32(70, 190, 220, 255);
                    if (Math.Abs(dx) <= 2 || Math.Abs(dy) <= 2)
                    {
                        if (r2 <= 95) color = new Color32(225, 235, 240, 255);
                    }
                    if (Math.Abs(dx) <= 1 && Math.Abs(dy) <= 1) color = new Color32(255, 185, 65, 255);
                    pixels[y * 38 + x] = color;
                }
            }
            launcherIcon.SetPixels32(pixels);
            launcherIcon.Apply(false, false);
        }

        void Update()
        {
            if (HighLogic.LoadedScene != GameScenes.FLIGHT || Time.timeScale <= 0f)
            {
                if (proposal != null) Invalidate("Preview expired after pause or scene change; refresh when flight is active.");
                if (visible)
                {
                    visible = false;
                    if (launcherButton != null) launcherButton.SetFalse(false);
                }
                ReleaseShipControlLock();
                return;
            }

            Vessel vessel = FlightGlobals.ActiveVessel;
            if (proposal != null)
            {
                string reason;
                try
                {
                    if (!SnapshotIsCurrent(vessel, out reason)) Invalidate(reason);
                }
                catch (Exception)
                {
                    Invalidate("Orbit revalidation failed; refresh the preview.");
                }
            }
            else if (vessel != null && (vessel.packed || vessel.situation != Vessel.Situations.ORBITING))
            {
                status = "Preview requires an unpacked vessel in orbit.";
            }
        }

        void OnGUI()
        {
            if (!visible || HighLogic.LoadedScene != GameScenes.FLIGHT || Time.timeScale <= 0f) return;
            GUI.skin = HighLogic.Skin;
            window.width = Mathf.Clamp(window.width, 390f, Mathf.Max(390f, Screen.width - 20f));
            window.height = Mathf.Clamp(window.height, 330f, Mathf.Max(330f, Screen.height - 35f));
            window.x = Mathf.Clamp(window.x, 0f, Mathf.Max(0f, Screen.width - window.width));
            window.y = Mathf.Clamp(window.y, 0f, Mathf.Max(0f, Screen.height - window.height));
            Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            string focusedControl = GUI.GetNameOfFocusedControl();
            bool focusedInput = focusedControl == "MegaCrewNavTargetPe" || focusedControl == "MegaCrewNavLeadSeconds";
            if (window.Contains(mouse) || focusedInput) AcquireShipControlLock();
            else ReleaseShipControlLock();
            window = GUILayout.Window(GetInstanceID(), window, DrawWindow,
                "MEGACREW  /  NAVIGATION PREVIEW", GUILayout.Width(window.width), GUILayout.Height(window.height));
        }

        void DrawWindow(int id)
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            DrawCurrentOrbit(vessel);
            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Target periapsis [km]", GUILayout.Width(175));
            GUI.SetNextControlName("MegaCrewNavTargetPe");
            targetPeriapsisKm = GUILayout.TextField(targetPeriapsisKm, GUILayout.Width(95));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Burn lead time [s]", GUILayout.Width(175));
            GUI.SetNextControlName("MegaCrewNavLeadSeconds");
            leadSeconds = GUILayout.TextField(leadSeconds, GUILayout.Width(95));
            GUILayout.EndHorizontal();

            if (proposal != null && snapshot != null &&
                (targetPeriapsisKm != snapshot.TargetText || leadSeconds != snapshot.LeadText))
                Invalidate("Inputs changed. Refresh the preview to calculate again.");

            GUILayout.Space(4);
            bool canPreview = vessel != null && !vessel.packed && vessel.situation == Vessel.Situations.ORBITING;
            GUI.enabled = canPreview;
            if (GUILayout.Button("Refresh preview", GUILayout.Height(30))) RefreshPreview(vessel);
            GUI.enabled = true;

            if (proposal != null)
            {
                GUILayout.Space(5);
                GUILayout.Label("Signed prograde Δv: " + proposal.SignedProgradeDeltaV.ToString("F3", CultureInfo.InvariantCulture) + " m/s (negative = retrograde)");
                GUILayout.Label("Burn UT: " + proposal.BurnUT.ToString("F2", CultureInfo.InvariantCulture) + " s");
                GUILayout.Label("Target Pe: " + (proposal.TargetPeriapsisAltitude / 1000.0).ToString("F3", CultureInfo.InvariantCulture) + " km");
                GUILayout.Label("Detached predicted Pe: " + (proposal.PredictedPeriapsisAltitude / 1000.0).ToString("F3", CultureInfo.InvariantCulture) + " km");
                GUILayout.Label("Snapshot only; it becomes invalid if vessel, orbit, inputs or UT change.");
            }
            else GUILayout.Label(status, GUI.skin.box);

            GUILayout.Space(6);
            GUILayout.Label("Candidate preview only: two-body detached prediction; no SOI, atmosphere, drag, terrain, finite burn, node, thrust, time warp, or approvals.");
            GUILayout.Label("Not runtime-verified. This station cannot create nodes or execute burns.");

            string focused = GUI.GetNameOfFocusedControl();
            bool needsKeyboard = focused == "MegaCrewNavTargetPe" || focused == "MegaCrewNavLeadSeconds";
            Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            bool mouseOverPanel = window.Contains(mouse);
            if (needsKeyboard || mouseOverPanel) AcquireShipControlLock(); else ReleaseShipControlLock();
            GUI.DragWindow(new Rect(0, 0, 10000, 22));
        }

        void DrawCurrentOrbit(Vessel vessel)
        {
            if (vessel == null || vessel.orbit == null || vessel.orbit.referenceBody == null)
            {
                GUILayout.Label("Active vessel / orbit: unavailable");
                return;
            }
            Orbit orbit = vessel.orbit;
            GUILayout.Label("Vessel: " + vessel.vesselName + "    Body: " + orbit.referenceBody.bodyName);
            GUILayout.Label("Pe: " + FormatKm(orbit.PeA) + " km    Ap: " + FormatKm(orbit.ApA) + " km    e: " + Format(orbit.eccentricity, "F6"));
        }

        void RefreshPreview(Vessel vessel)
        {
            Invalidate("Computing detached preview…");
            double peKm, lead;
            if (!TryParseFinite(targetPeriapsisKm, out peKm) || peKm <= 0.0)
            {
                status = "Target Pe must be a finite positive number in kilometres.";
                return;
            }
            if (!TryParseFinite(leadSeconds, out lead) || lead <= 0.0)
            {
                status = "Lead time must be a finite positive number of seconds.";
                return;
            }
            double targetPe = peKm * 1000.0;
            double now = Planetarium.GetUniversalTime();
            double burnUT = now + lead;
            if (!IsFinite(targetPe) || !IsFinite(now) || !IsFinite(burnUT) || burnUT <= now)
            {
                status = "Target altitude or future burn UT is outside finite numeric bounds.";
                return;
            }

            try
            {
                proposal = CircularLowerPeriapsisPreview.Create(vessel, burnUT, targetPe,
                    MaxEccentricity, PeriapsisToleranceMetres);
                snapshot = Capture(vessel, proposal, targetPeriapsisKm, leadSeconds);
                status = "Detached prediction ready.";
            }
            catch (Exception ex)
            {
                proposal = null;
                snapshot = null;
                status = "Preview rejected: " + ex.Message;
            }
        }

        PreviewSnapshot Capture(Vessel vessel, CircularLowerPeriapsisProposal candidate,
            string targetText, string leadText)
        {
            Orbit orbit = candidate.StartingOrbit;
            return new PreviewSnapshot
            {
                VesselId = vessel.id,
                Orbit = orbit,
                Body = candidate.Body,
                TargetText = targetText,
                LeadText = leadText,
                PositionAtBurn = orbit.getRelativePositionAtUT(candidate.BurnUT),
                VelocityAtBurn = orbit.getOrbitalVelocityAtUT(candidate.BurnUT),
                EndTransition = orbit.patchEndTransition
            };
        }

        bool SnapshotIsCurrent(Vessel vessel, out string reason)
        {
            reason = "Preview invalidated; refresh before relying on it.";
            if (proposal == null || snapshot == null) return false;
            if (HighLogic.LoadedScene != GameScenes.FLIGHT || Time.timeScale <= 0f)
            {
                reason = "Preview invalidated by pause or scene change.";
                return false;
            }
            if (vessel == null || vessel.id != snapshot.VesselId || FlightGlobals.ActiveVessel != vessel)
            {
                reason = "Active vessel changed; refresh the preview.";
                return false;
            }
            if (vessel.packed || vessel.situation != Vessel.Situations.ORBITING)
            {
                reason = "Vessel is packed or no longer orbiting; refresh after returning to an unpacked orbit.";
                return false;
            }
            double now = Planetarium.GetUniversalTime();
            if (!IsFinite(now) || proposal.BurnUT <= now)
            {
                reason = "Preview burn UT expired or universal time is invalid; refresh with a new lead time.";
                return false;
            }
            if (vessel.orbit != snapshot.Orbit || vessel.orbit == null || !vessel.orbit.activePatch ||
                vessel.orbit.referenceBody != snapshot.Body || snapshot.Body == null)
            {
                reason = "Orbit patch or reference body changed; refresh the preview.";
                return false;
            }
            Orbit orbit = vessel.orbit;
            if (orbit.patchEndTransition != snapshot.EndTransition)
            {
                reason = "Orbit patch transition changed; refresh the preview.";
                return false;
            }
            if (!IsFinite(orbit.StartUT) || proposal.BurnUT < orbit.StartUT ||
                (orbit.patchEndTransition != Orbit.PatchTransitionType.FINAL &&
                 (!IsFinite(orbit.EndUT) || proposal.BurnUT >= orbit.EndUT)))
            {
                reason = "Proposal UT is no longer inside the active orbit patch.";
                return false;
            }
            global::Vector3d position = orbit.getRelativePositionAtUT(proposal.BurnUT);
            global::Vector3d velocity = orbit.getOrbitalVelocityAtUT(proposal.BurnUT);
            if (!VectorIsFinite(position) || !VectorIsFinite(velocity) ||
                (position - snapshot.PositionAtBurn).magnitude > PositionChangeToleranceMetres ||
                (velocity - snapshot.VelocityAtBurn).magnitude > VelocityChangeToleranceMetresPerSecond)
            {
                reason = "Future orbit state changed in place; refresh the preview.";
                return false;
            }
            if (vessel.patchedConicSolver == null || vessel.patchedConicSolver.maneuverNodes == null ||
                vessel.patchedConicSolver.maneuverNodes.Count != 0)
            {
                reason = "Maneuver plan changed; refresh after the vessel has no existing nodes.";
                return false;
            }
            if (targetPeriapsisKm != snapshot.TargetText || leadSeconds != snapshot.LeadText)
            {
                reason = "Preview inputs changed; refresh to calculate again.";
                return false;
            }
            return true;
        }

        void Invalidate(string message)
        {
            proposal = null;
            snapshot = null;
            status = message;
        }

        void AcquireShipControlLock()
        {
            if (keyboardLocked) return;
            InputLockManager.SetControlLock(ControlTypes.ALL_SHIP_CONTROLS, InputLockId);
            keyboardLocked = true;
        }

        void ReleaseShipControlLock()
        {
            if (!keyboardLocked) return;
            InputLockManager.RemoveControlLock(InputLockId);
            keyboardLocked = false;
        }

        void OnDestroy()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(OnLauncherReady);
            GameEvents.onGUIApplicationLauncherDestroyed.Remove(OnLauncherDestroyed);
            if (launcherButton != null && ApplicationLauncher.Instance != null)
                ApplicationLauncher.Instance.RemoveModApplication(launcherButton);
            launcherButton = null;
            visible = false;
            ReleaseShipControlLock();
            if (launcherIcon != null) Destroy(launcherIcon);
            launcherIcon = null;
        }

        static bool TryParseFinite(string text, out double value)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && IsFinite(value);
        }

        static string FormatKm(double metres)
        {
            return IsFinite(metres) ? (metres / 1000.0).ToString("F3", CultureInfo.InvariantCulture) : "unavailable";
        }

        static string Format(double value, string format)
        {
            return IsFinite(value) ? value.ToString(format, CultureInfo.InvariantCulture) : "unavailable";
        }

        static bool VectorIsFinite(global::Vector3d value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
