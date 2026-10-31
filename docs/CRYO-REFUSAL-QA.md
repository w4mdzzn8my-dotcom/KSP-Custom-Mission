# Direct cryopod colocation refusal QA

This separate addon checks one backend behavior: the real `CryoBridge.Freeze`
request must refuse Ada while she is aboard but outside the sole cryopod.
It does not qualify the GUI, transfer-busy protection, freeze/thaw, power
accounting, save persistence or the whole flight. Those gates remain separate.

## Exact disposable inputs

Use a fresh copy named `KSP-Hermes-QA`, with the pristine
`MegaCrew Background QA` save and the stable 36-source candidate. The builder
and addon pin these SHA-256 values:

| Input | SHA-256 |
| --- | --- |
| `saves/MegaCrew Background QA/persistent.sfs` before load | `6fd1a826062dafcdf02fa485c89ca818819ea5352bcfc5eca5f1bfde2c1bb4b9` |
| `GameData/MegaCrew/Plugins/MegaCrew.dll` | `2d6fd46d353b1cac3b971deff623a2f7f24d2d52cb44cc605e01e8548c5ca986` |
| Original BackgroundResources provider | `ed956b9ec2cc5f414586a52a9086423e4c9125fa72097bce2721ea41eb075bc9` |

These identify this local candidate, not an arbitrary rebuild on another host.
No other QA DLL may be loaded from GameData. Backups outside GameData are
ignored. The tool rejects mismatched inputs, symlinked/escaping output and
existing run markers or output; it never overwrites a prior attempt.

Create these exact UTF-8 root files, each ending with a newline:

`CryoRefusalQA.scope`:

```text
MEGACREW_DISPOSABLE_CRYO_REFUSAL_QA_V1
```

`CryoRefusalQA.enable`:

```text
AUTHORIZE ONE ORIGINAL-PROVIDER CRYO REFUSAL QA FOR SAVE: MegaCrew Background QA
```

Build from this source checkout:

```sh
python3 tools/build_cryo_refusal_qa.py --qa-root /path/to/KSP-Hermes-QA --csc /path/to/Roslyn/bincore/csc.dll
```

The build writes only the dedicated `HermesCryoRefusalQA.dll`. Runtime consumes
enable into running, writes `CryoRefusalQA-result.cfg`, moves running to done,
then quits only its scoped app. Preserve consumed profiles and use a new copy
for every attempt.

## Native initialization and actual observation

After verifying the pristine save and loaded bridge identity, the addon follows
KSP's normal load pipeline: assign the loaded Game, update missing scenario
modules, explicitly save that Game to the disposable persistent save, fire
the post-load event with its loaded config, set the save folder, and call
`Game.Start()`. It does not call FlightDriver directly or manufacture a
DeepFreeze singleton/readiness flag. The native setup save is intentional;
the pristine template and original installations are untouched. A changed
disposable save hash is reported and never called persistence qualification.

Menu pause/warp values are diagnostics. Strict unpaused actual 1x checks begin
after the intended vessel is loaded and ready. The probe requires 17 parts,
four known awake crew, exactly one real pod, connected and idle DeepFreeze,
and Ada exactly once aboard outside every pod. It observes actual UT before
the sole request and for five UT seconds afterward. Immediate crew/pod/resource
changes fail; later resource deltas are recorded without assigning a cause,
because ordinary consumption continues. Lost context/readiness is inconclusive.

## Evidence so far

The component harness uses the actual pinned bridge DLL and real file hashes
with fake KSP/Unity/DeepFreeze boundaries. Its 111 assertions pass, including
the native initialization order and exact four-argument SaveGame overload.
This does not establish a live scenario load.

The first live attempt correctly stopped before load on an overly strict
menu-clock check; no request occurred. After that QA setup fix, a second live
attempt loaded actual Hermes flight at 150 km, 17 parts and four awake crew,
but DeepFreeze never became ready. Logs showed its settings instance missing;
inspection found the launcher had skipped native scenario initialization.
That run was inconclusive and made no freeze request. The native lifecycle
correction is now implemented; its live result remains pending.

The subsequent native-lifecycle run completed the direct refusal observation.
See [the runtime evidence record](CRYO-REFUSAL-RUNTIME-EVIDENCE.md) for exact
scope, hashes and remaining gates. The historical report is preserved; newer
probe source also emits an explicit false flight-qualification field and a
finished stage label. Those label additions were tested at recording boundaries
and have not been substituted into the historical live report.
