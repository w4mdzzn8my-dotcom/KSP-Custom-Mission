# MegaCrew integration audit

Source-only audit on 2026-10-31 Australia/Sydney; all game-runtime gates remain pending.

## Inputs

- Published 0.3.0 baseline ZIP from origin/main: SHA-256 0071eacf3a1b38a63c611fe2a65877ea2f62f6076b7e97d22e9cdf58b412cf40.
- Cumulative 0.3.4 source patch from origin/main: SHA-256 3d97e71e2aea4f1359ba5528bfd9ef2c0d9be08cfd74ac04d3c26f518b89711a.
- Both artifacts came from origin/main commit 95749349533e87da7597f65506dd6ce3ed8e1915. Their hashes were recomputed after staging.
- baseline/ is a direct ZIP extraction. combined/ is a copy with the source patch applied cleanly using patch --dry-run -p1 and patch -p1, followed by overlaying the current navigation working tree's Source/Navigation/*.cs. The reconciliation itself made no edits to campaign source in the checkout.

## Overlay and build

The combined overlay has nine files. SHA-256:

- BurnApprovals.cs 5dd7cdef215b3671d9822a9caadbe5b53d0465450c0081fd6d58b7624140b7ce
- BurnDurationEstimator.cs de30d1b7e898538dcb08b8446a8451ccf5a1a7a9a9b0d113a65df5384932ef53
- CircularLowerPeriapsisPreview.cs 2ac5da3d0c4cd4406cf823090423653e313bacbf1ff4155ea9bbc07574a4c8c3
- Lambert.cs 8efcbd6402d1a315735805e1bc6737f384d75ea522fdd13e63908c8e724bdcfc
- MissionControlMath.cs bb6825e45a94025084955d17dd2b2698c6aac34acb2480f450167fc0ac2ef90b
- NavigationPreviewPanel.cs e12873c0976eb3aa47dd8636be1a1b5260b39f9fe6d0073d6f16abb4c1075823
- NodeFrame.cs b6db03fda381f96d6039302b4cf4411e32a7af8546b76b63e94d08db700b5796
- PlanFingerprint.cs 7ae0c0e3546f10962a7e023524d0ef3fc50a57fbe204935b176b6ae5ba36f4f9
- WatchCoverage.cs 2139efd02d243e565e2cd580057ff5ce5f3f444813a79745721fb773f43c17fe

After the final panel edits (patch bounds, vector staleness, ship locks and exception handling), all C# files in combined/Source (22 files) compile successfully against the 89 DLLs in a KSP 1.12.5 Managed directory using Roslyn C# 7.3, with no diagnostics. Output: MegaCrew.dll SHA-256 a61c1759d50ffe47dd480d5b20e7feeed89811d1268c487c9aee0f44d1b81565. Output was written only under this temporary audit directory. The compile demonstrates source-level compatibility/no compile-time type conflicts; it does not establish runtime behavior.

Reproduction: extract the named ZIP to baseline, copy to combined, apply the named patch with patch -p1, overlay current Source/Navigation/*.cs, then invoke Roslyn with -nologo -target:library -nostdlib+ -langversion:7.3, -out set to a temp path, references to every DLL in the KSP 1.12.5 Managed directory, and every combined/Source/**/*.cs file. csc is the SDK's Roslyn/bincore/csc.dll; invoke via dotnet.

## Tests

From combined/:

- python3 -m unittest discover -s tests -p 'test_*.py' -v — 6/6 passed.
- python3 tests/run_needs_tests.py --runtime <.NETCore.App runtime directory> --csc <SDK Roslyn/bincore/csc.dll> — passed.
- python3 tests/run_campaign_tests.py --runtime <.NETCore.App runtime directory> --csc <SDK Roslyn/bincore/csc.dll> — passed. The adapter test emitted one benign CS0414 warning for an assigned but unread test-double field.

These are source/model tests and KSP stubs, not game runtime verification. No game launch or install occurred; the generated branch DLL was not touched. Hami owns campaign sources; this report is integration evidence only.
