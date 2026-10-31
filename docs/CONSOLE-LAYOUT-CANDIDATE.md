# Console layout candidate — awaiting runtime retest

The human report in [issue #11](https://github.com/w4mdzzn8my-dotcom/KSP-Custom-Mission/issues/11)
showed a console that expanded across the screen and cropped briefly on clicks.
The old eight-tab GUILayout toolbar and unbounded content could enlarge the
requested window. This establishes an overflow path; it does not establish the
cause of every failed click or prove IMGUI event oscillation.

The candidate uses GUI.Window with a viewport-clamped 720 × 520 maximum size.
Tabs occupy four columns and two rows. Header, tabs, scrolling body and wrapped
status occupy separate fixed rectangles. Content never expands the outer window.
Dragging is restricted to the title strip. Every layout area and scroll group
ends in finally, and OnGUI restores the incoming skin/enabled state. The console
starts closed; F8 or the MegaCrew button opens it.

The screenshot also had an EVA Kerbal as the active vessel. The Crew tab now
states that context and disables its watch-lead, freeze, care, wake and alarm
buttons there. This is a Crew-tab gate; it does not make every mission/debug
panel read-only during EVA. The eventual outside-pod check uses Ada aboard
Hermes in another crew part, with DeepFreeze ready and idle.

Evidence: all 38 production sources compile against KSP 1.12.5. The existing
console input-lock boundary harness passes, including preservation of FreeIVA's
lock. These checks cannot prove pixel geometry, actual click response or stock
crew-transfer behavior. The DLL is a repository candidate only.

After Basil explicitly releases the active-play runtime hold, use a newly
prepared disposable copy. Open/close and drag the console; switch every tab;
scroll long pages; check wrapped messages at the actual display/UI scale; check
Crew controls aboard and in EVA; then perform the original refusal procedure.
Record the exact source and DLL hashes, display settings and observed behavior.
Do not replace a running game or infer this test from compilation.
