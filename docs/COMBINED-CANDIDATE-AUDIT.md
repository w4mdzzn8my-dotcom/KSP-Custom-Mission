# Combined 0.3.7 candidate packaging review

## Result

Reviewed the combined builder output read-only against a separately reconstructed pristine 0.3.7 baseline and the pinned Git commits recorded in its manifest. The builder is committed at `c170bfc`. No source or test blob mismatch, core overlay, unexpected dependency DLL, or missing source file was found.

- The baseline has 17 C# files under `Source`. All 17 candidate files match the pristine patched baseline SHA-256 values exactly.
- The candidate has exactly 36 C# source files: those 17 core files, the ten allowlisted operations files from `3c1fe22d001d52630a0a75430274f86fb75e45af`, and the nine navigation files from `294c667470e5304ac32d5f905950b357a8b9d45e`.
- All ten operations source blobs and all nine navigation source blobs match their pinned Git objects byte for byte. The five operations model test/runner pairs and five navigation test files also match their pinned blobs and expected output paths.
- The manifest identifies one cumulative patch only: `Hermes-Campaign-v0.3.7-source.patch` from `f76e4371d41ff6c246f3cea5b88d2a516f36bc38`, with the expected SHA-256. The published 0.3.0 ZIP hash also matches the pin. No 0.3.6 or supplementary patch was stacked.
- The manifest covers all 109 candidate files and its complete output-tree hash independently recomputes to `3557363223bff50bc4ae655f5ab172576c18d6cd26fdae61d8b2ecd715110be1`.
- The candidate compiled as C# 7.3. `AssemblyName.GetAssemblyName` reports `MegaCrew, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null`.
- The output contains one filesystem DLL, `GameData/MegaCrew/Plugins/MegaCrew.dll`. The embedded published ZIP contains only its original `MegaCrew.dll`; no KSP, Unity, or other dependency DLL was copied into the candidate. The candidate build does record KSP assemblies as compiler inputs, not as packaged files.
- The updated builder unit suite passes all 16 tests. The builder author reports the combined KSP compile and all 13 focused candidate suite groups pass. No game was launched.

## Builder review

The source/patch refs are resolved to commits and all blobs/hashes are collected before creating output. The fixed source/test allowlists prevent operations overlays from reaching `MegaCrew.cs`, `Practice.cs`, or other core files. Overlay path collisions are preflighted as a group before any file write. Missing operations blobs fail before destination creation. Navigation-only mode remains explicit when `--operations-ref` is omitted; the combined candidate records both refs and resolved commit IDs. The candidate output path is under `/tmp`, outside the checkout, and was created as a new directory.


This is packaging, compilation and model evidence. It does not establish live
UI, cryopod, flight, lander or multiplayer behavior. The candidate remains local.
