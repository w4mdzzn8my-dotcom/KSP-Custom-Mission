# Finch lander and Hermes external cradle

Development candidates for the single-player foundation, following the priority
in docs/ROADMAP.md. These craft files do not replace any save or the existing
Hermes trainer. Neither craft includes a launch booster.

## Delivered hardware

- `Ships/VAB/Hermes Finch Vacuum Lander.craft`: 17 stock parts, two-seat Mk2 lander
  cabin, full Rockomax X200-8 LF/O tank, Terrier, four LT-2 legs, four RCS blocks,
  40 units of cabin monopropellant, four fixed solar panels, supplemental battery
  and standard Clamp-O-Tron docking port. The cabin's Lander variant is selected.
- `Ships/VAB/Hermes Finch Docking Cradle.craft`: existing Hermes walkthrough
  assembly with one external side port and the Finch attached port-to-port.
  This is an open cradle, not a shielded cargo bay. The interior route is retained.

Use `tools/build_landers.py --ksp-root '/path/to/KSP'` to regenerate from local
stock part definitions. Add `--output-root '/path/to/temp/output'` to write the
generated craft files and design report elsewhere; the base Hermes Walkthrough
craft is still read from this repository. No stock assets are redistributed.
The generator uses stock node positions and serializes resources; prefab
defaults supply modules. The cradle uses existing SSPX/DeepFreeze dependencies
from the trainer.

## Mission envelope and provisional budget

The target is a sortie from a mothership **already in low Mun or Minmus orbit**,
landing and returning to rendezvous. The 150-km Kerbin trainer is a deployment
practice site, not a lunar transfer solution. No atmospheric entry, Duna landing,
Kerbin return or ascent from Kerbin is qualified by this design.

The ideal design calculation includes two 0.09375-t crew, full cabin monopropellant
and four tonnes of LF/O. It estimates 7.3425 t wet mass, about 2,663 m/s ideal vacuum
delta-v at 345 s Isp, and initial Mun TWR about 5.0. These are design calculations;
actual KSP mass/module behavior and flight losses need separate validation.

A provisional Mun sortie allocation is 650 m/s descent, 650 m/s ascent,
200 m/s rendezvous/correction and 300 m/s retained reserve: 1,800 m/s total.
This is a planning allocation, not a proven trajectory or guarantee. Hovering,
terrain, landing site and inclination can exceed it. Low Minmus orbit needs less
propulsion but still needs a tested ascent/rendezvous plan. Preserve monopropellant
for docking and keep adequate battery reserves through eclipse.

## Storage and deployment procedure to qualify

1. Load the cradle in the editor. Keep the centrifuge stopped. Check full geometric
   clearance around the cabin hatch, docking rings, legs, arrays and radiators.
2. In a disposable orbit fixture, verify the two docking nodes are connected and
   the lander engine is off. Transfer two crew by stock crew transfer if available;
   otherwise use an EVA transfer. No new FreeIVA passage is claimed through the port.
3. Select the lander port's control reference, separate using its docking-node
   release, and translate clear with RCS. Confirm the carrier remains intact.
4. Keep the engine off until at least 50 m clear and orient away from the carrier
   before firing. This distance is a proposed test procedure, not measured plume
   clearance. No automatic undocking burn is installed.
5. Deploy legs, demonstrate a surface touchdown without engine/terrain contact,
   verify both crew can EVA/reboard, then ascend with the planned reserve.
6. Return with engines off near Hermes and redock the same standard ports. Check
   state/crew/resources across save/reload, then retransfer crew.

## Evidence and outstanding gates

The lander-craft tests pass, including graph connectivity/acyclicity, unique
IDs, finite transforms, normalized rotations, opposed/coincident docking faces,
four legs/RCS blocks and explicit fuel resources. Regeneration tests also verify
that historical evidence is left untouched, output is deterministic, and the
current craft hashes in the design report match generated files. These checks are
structural and do not prove collider clearance, leg deployment, flight stability
or docking behavior. `docs/Finch-Design.json` contains current calculated design
figures and generated craft SHA-256 values; `runtime_verified` remains false.
Historical main-menu observations are kept separately in
`docs/Finch-Loader-Evidence.json` and are explicitly scoped to a prior candidate.

Native KSP loading is checked with a temporary disposable probe, separate from the
production mod. Historical main-menu observations are recorded in
`docs/Finch-Loader-Evidence.json`; no flight result is established. Flight load,
storage/release clearance, landing, EVA access, re-ascent, redocking and reload
persistence must all pass before these candidates can be called usable landers.

### Loader-probe history

The first native loader attempt rejected both crafts because generated stock part
names containing underscores were not encoded as KSP's period-separated names.
The generator and a regression test now enforce this format. A second attempt
produced no result while using Unity's game-time wait at the main menu. The test
probe now uses a real-time wait and explicit begin markers, and is being rerun.
The temporary `tests/ksp/LanderLoaderProbe.cs` is test-only and must not be placed
in a normal installation: it instantiates and destroys both craft in the main menu.
It is not included in the production MegaCrew DLL.

### Native loader result

These are retained historical main-menu observations for a prior, pre-reorder
candidate. The exact craft bytes loaded by the probe were not recorded, so the
source-reference hashes in `docs/Finch-Loader-Evidence.json` are provenance
only. They do not qualify the current parent-first candidate.

The then-current files were loaded with `ShipConstruction.LoadShip` in the separate
KSP 1.12.5 test copy. The Finch loader returned 17 parts, two seats, one docking
module and one engine; the loader reported 6.967 t with resources and no crew.
The cradle returned 35 parts, 32 total seats, two docking modules and two engines.
The Finch's loaded root was the Mk2 lander cabin. The probe did not report a root
for the cradle, so its actual in-game attachment hierarchy needs investigation;
the Python graph test is not sufficient proof of this. KSP logged a module-index
mismatch before finding the cabin's variant module, plus the existing SSPX
adapter module mismatch. The test ran at the main menu, outside a real editor
or flight scene, and generated unrelated main-menu module-start exceptions.

The measured loaded mass is not the fully crewed design estimate; resolve the
remaining difference against the stock editor and module/crew mass before relying
on the ideal delta-v figure. Loading does not establish docking-node state, collider
clearance, working legs, staging, EVA access, propulsion or landing/recovery.

A repeat probe that waits a real second between destroying the first construct
and loading the second did report `sspx-core-375-1` as the cradle root and the
expected carrier spine parent chain. The first no-root observation therefore is
not reproducible under separated probe lifetimes. Both docking parts have
`flightID = 0` in this main-menu context, so the parent log cannot distinguish
which is the lander-side port. Confirm the two ports' actual attachment and
release behavior in an editor/flight scene before claiming the cradle works.

The later index-based loader probe confirms cradle part 18 (lander cabin) is
parented to part 21 (lander port), which is parented to part 17 (carrier port).
Both docking modules report `Ready` in the main-menu load context; that is not
proof of their initialized flight docking state or an operational release event.

### Disposable flight probe in progress

`tests/ksp/FinchFlightProbe.cs` is a separate test-only addon, never part of the
production MegaCrew build. It checks for a one-shot marker in an isolated test
copy, loads a separately named `MegaCrew Finch QA` save, consumes the marker,
launches the cradle on the launchpad and reports in-flight docking event state.
The named QA save is a copy of the pristine practice fixture. This experiment
may destroy its own simulated craft and must never target an existing personal
save or server Universe. The marker and probe should be removed after testing.
At present the probe is running; no launch, release or safe-clearance result has
been established. Launchpad observation does not substitute for orbital testing.

### Orbital fixture probe

The launchpad fixture failed: KSP could not find `Facility/LaunchPad_spawn` and
returned to the space center. It did not yield a vessel or docking result. A
separate `FinchOrbitProbe.cs` is now being tested against a **new** disposable
`MegaCrew Finch Orbit QA` copy. It loads the cradle as a ShipConstruct, asks KSP
to serialize its actual parts into ProtoPartSnapshots, creates a 150-km circular
Kerbin orbit and focuses that vessel. It intentionally clears vessels only in the
new QA game object and has no production role. This test can diagnose flight state
and the docking modules; it does not establish a crewed mission, redocking or safe
clearance. A marker makes it one-shot. Results must be observed in the log.

The first orbital snapshot fixture reached flight but did not create an active
vessel and produced module-initialization exceptions. It is not a craft flight
result. The next isolated probe loads the existing practice orbit first, then
creates the test cradle at a separate 300-km Kerbin orbit through KSP's
`Game.AddVessel`; the original practice vessel remains in the disposable copy.
This alternative is awaiting runtime results.

The second orbital attempt loaded the practice station, spawned the test cradle
in a separate orbit and requested a switch. KSP saved only the disposable QA game,
then emitted part-initialization errors and had no active vessel. The cause is
unresolved; the generated craft is not flight-qualified. A third one-shot probe
copies that generated QA save into a new `MegaCrew Finch Resume QA` directory
and attempts a clean reload of vessel index 1. This tests reload mechanics only;
it does not modify any earlier fixture or user save.

The clean reload of the generated fixture exposed a zero semimajor axis: the
probe passed orbital-constructor arguments in the wrong order. KSP removed the
invalid orbit and threw while loading the vessel. The probe now supplies the
semimajor axis in the correct third argument and uses another new disposable
save; this is a fixture correction, not evidence of a defective craft. The
separate part-snapshot loading exception also needs to be retested.

The corrected-orbit probe switched to Finch, but its flight-scene addon then
incorrectly spawned another copy on the new scene. The disposable run was stopped.
The probe now guards that spawn once per process and logs whether KSP accepted
the switch; the next run uses a fresh QA save. Until that run completes, there
is still no valid stability or docking-event result.

The corrected-orbit probe switched focus successfully, but observed only 18 of
35 candidate parts on the active carrier after 12 and 32 seconds; its remaining
port reported `Ready` with neither undock nor decouple available. The temporary
fixture serialized both mated docking modules as `Ready` from the editor before
flight initialization. That fixture does not reproduce stock launch's
`PreAttached` state, so it cannot distinguish a craft defect from an invalid
fixture. A new isolated test sets the two linked ports to the stock
`PreAttached` state before loading and records all vessels; this is test-fixture
initialization, not a production change or proof that stock launch will do so.

### Evidence correction: packed versus loaded vessels

The last log's world list also records the Finch vessel as `loaded=False` with
35 serialized part snapshots while the partial live part list had 18. The count
difference therefore **does not prove loss of the lander**. Likewise, the single
live docking module is insufficient to qualify release behavior on a packed or
partially loaded vessel. The probe now waits for both loaded and unpacked state
with a bounded timeout, reports live and snapshot counts, and marks the test
inconclusive if the load gate does not pass. This corrected probe compiles against
KSP 1.12.5; no new runtime pass is claimed. The prior results remain as diagnostic
history rather than evidence of an operational or defective cradle.

### Source correction: parent-before-child serialization

The cradle's editor attachment graph was connected, but its serialized order
listed the cabin before the docking port that parents it. The failed runtime
materialized 18 parts and then hit `ProtoPartSnapshot.Load` at that forward
reference. The generator now emits a stable parent-before-child ordering for the
cradle, and a regression asserts every parent precedes its child. The snapshot
probe locates docking modules by module name rather than obsolete part indices.
The structural/setup tests pass and the probe compiles. This addresses a
concrete serialization defect, but the corrected order has not been tested in
KSP; earlier loader/mass measurements are preserved in
`docs/Finch-Loader-Evidence.json` and describe prior-candidate observations.
The exact craft bytes used by those probes were not recorded. Hashes in that
file identify a pre-reorder source snapshot for provenance only and must not be
read as proof that those exact bytes were loaded. Nighttime runtime restrictions
remain respected.


### Read-only saved-vessel audit

`python3 tools/validate_vessel_snapshots.py /path/to/persistent.sfs --vessel "Finch Orbit QA"`
checks direct serialized VESSEL/PART snapshots without writing to the save. It
checks numeric unique IDs, root and parent bounds, parent-first order, connected
parent graphs, stack/surface attachment targets and known symmetry index fields.
It accepts the root's -1 parent sentinel. This is a narrow structural diagnostic;
it does not validate docking modules, flight behavior or every ConfigNode field.

The earlier saved 35-part fixture reports exactly one error: cabin index 18
references parent index 21 before that parent appears. Its attachment and surface
links are valid; an earlier audit suggesting missing surface links was incorrect.
The reordered generator is still awaiting runtime qualification. Eleven portable
validator tests include the old forward-parent shape and a reordered passing
shape, malformed inputs and ID/index failures. No personal save or server data
is included in those tests.


### Identity-bound orbital observations

The test-only orbital probe now records the original practice vessel GUID from
the explicitly loaded disposable save before flight. It checks that exact
identity, save and home body, then waits boundedly for loaded/unpacked state
before spawning. It starts observation explicitly after a successful switch
rather than relying on another flight-addon startup.

The spawned vessel GUID and body are retained for every observation wait. A
changed active vessel, save or body produces an inconclusive result. Docking
diagnostics require the exact target to be loaded/unpacked with both 35 live
parts and 35 saved snapshots. Names are labels, not identity. Scene teardown
releases observation ownership so a restarted addon can resume the same target
without spawning again. Source compiles against KSP 1.12.5; switching timing,
complete loading and docking/release remain unverified in-game.
