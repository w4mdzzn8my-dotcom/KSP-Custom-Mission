# Console input isolation candidate

Apply Hermes-Console-Input.patch after the cumulative 0.3.4 source patch.
This focused source delta does not include a DLL, save or provider. Rebuild
an isolated candidate with the current campaign/operations sources.

The mission and interior panels now hold their own ALL_SHIP_CONTROLS lock
while hovered in flight. Named console text fields retain that lock while
being edited; an outside click clears the console field focus. The toolbar
button is covered too. Closing/hiding, leaving the pointer without a field
focus, changing scene, losing application focus, disabling or destroying
the console releases only MegaCrew.ConsoleInput. It never removes FreeIVA
or another mod's locks. GUI focus is read only in OnGUI, not Update.

Production lock lifecycle checks with recording KSP/GUI boundaries pass. The
full plugin compiles against KSP 1.12.5. These do not prove actual IMGUI event
ordering, click-through prevention or FreeIVA behavior. No new DLL has been
installed into a play copy for this change.

Runtime gates in the disposable copy:
- Hover both panels and the toolbar: clicks/keys must not change stock ship
  attitude, throttle or staging. Leave the panels: controls must return.
- Type and edit engineering fields, move the pointer away, then click outside;
  check focus and stock controls. Verify F8 and the Close button release.
- Exercise I-panel actions and FreeIVA hatches/walking. The stock control lock
  does not itself guarantee that FreeIVA raw keyboard input is suppressed.
- Change scenes, switch application focus, and close the console. Inspect KSP
  input locks and verify no lingering MegaCrew.ConsoleInput entry.
- Repeat alongside Basil's standalone navigation panel so each independent
  lock releases without changing the other.
