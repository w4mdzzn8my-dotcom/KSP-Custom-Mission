# Staged BackgroundResources cryopod correction — not flight qualified

The installed BackgroundResources DLL bundled with DeepFreeze has SHA256
`ed956b9ec2cc5f414586a52a9086423e4c9125fa72097bce2721ea41eb075bc9`.
Inspection of its actual DeepFreezer.ProcessHandler IL found:

- `pushing=true` at RequestResource, which the same DLL's resource handler treats
  as production rather than consumption.
- A request amount containing both elapsed UT and TimeWarp.fixedDeltaTime.
- Vessel-wide frozen crew count used separately for every pod.

The staged correction uses consumption, elapsed UT once, and per-pod frozen
crew. Same-length instruction edits preserve all branch offsets and metadata.
It does not replace the active-vessel DeepFreeze module or change its settings,
thermal rules, warning/death policy, or the existing failure path. In particular,
shortfall/recharge timing, settings guards and partial consumption still need
review and actual runtime tests; this is not a complete background support fix.

`tools/fix_background_cryo.py` requires the exact original input hash and method
hash, verifies old instruction bytes, refuses in-place writes and refuses an
existing output file. It stages a separate candidate only. Unknown, newer or
already patched inputs are rejected. No third-party DLL is redistributed here.
The manifest records all four edits and original method location/hash.

```
python3 tools/fix_background_cryo.py --input /path/to/original/BackgroundResources.dll --output /path/to/qa-staging/BackgroundResources.dll
MEGACREW_BGR_DLL=/path/to/original/BackgroundResources.dll python3 -m unittest discover -s tests -v
```

Expected staged SHA256:
`d2c1ab678a0891246dc7b16e66e180ced7e8ad55db8bb9971cc742224dfee45a`.
Retain the original and test only in a disposable KSP copy/save before installing
into the play copy. The host play copy is unchanged by this staging tool.

Five tests evaluate the corrected DLL's actual IL expression over 144 input
combinations, reproduce the original regression, verify the consumption opcode,
check that bytes outside the target method are unchanged, and reject unsupported
inputs. Independent ILSpy decompilation confirms the corrected expression and
consumption call. These are instruction-level tests, not a KSP resource test.

Runtime acceptance: real freeze, single/multiple pods, no solar generation,
measured EC and actual elapsed UT, partial/depleted stores, recharge, disabled
background settings, active/unloaded transitions, save/reload and no duplicated
charge. The separate active-vessel high-warp/thermal gaps remain to solve. Do not
enable accelerated campaign coast based solely on this candidate.
