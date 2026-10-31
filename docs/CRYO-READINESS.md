# DeepFreeze bridge readiness candidate

The bridge now requires the actual static DeepFreeze APIReady flag and instance
before treating a frozen-crew dictionary or power policy as verified. An empty
dictionary during API initialization no longer counts as proof of unfrozen crew.

Freeze/thaw dispatch is blocked while the active vessel is packed/unloaded,
while any pod is freezing/thawing or transferring stock crew, or when a required
pod status property cannot be read. Uses public v0.31.0.0 DFIIsFreezeActive,
DFIIsThawActive, DFIcrewXferTOActive and DFIcrewXferFROMActive properties.
The pinned source was inspected locally; no DeepFreeze settings are changed.

Production reflection-bridge tests use recording KSP/DeepFreeze boundary types.
They verify initialization, transfer, busy and unknown-property rejection, foreign
vessel membership, and that only ready requests reach the API boundary. They do
not simulate actual freezing, resource draw or a crew animation. Full plugin
compile against installed KSP 1.12.5 APIs passes; live qualification is pending.

The existing disposable cryopod probe remains the first live test. In addition,
check manual commands during stock crew transfer and pod initialization, and
verify sequential thaw waits for actual completion. A returned request still
means requested, not a completed freeze/thaw or a certified power budget.
