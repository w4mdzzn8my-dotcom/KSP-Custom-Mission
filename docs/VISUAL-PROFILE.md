# Optional Mac visuals — issue #3

Basil clarified in chat that the effects request is **Firefly plus VaporCones**.
The profile uses the already installed ShVAK VaporCones, not the separate
Waterfall generator-part mod with a similar name.

- Clouds: locally saved `VolumetricClouds-v3.zip`, including its matching EVE,
  Scatterer and stock cloud configuration. Obtain it from
  [Blackrack's release 3 post](https://www.patreon.com/blackrack/posts/true-volumetric-87982960).
  The measured archive hash is in `visual-profile.json`; this is a local pin,
  not a publisher signature. Do not mix in the main game's SPVE/OPM configs.
- Terrain/scatters: Basil's installed NativeParallax 0.2 experimental native Mac
  renderer and its packaged assets. This is a custom backend using Parallax
  assets, with five configured bodies, rather than upstream Parallax Continued's
  GPU renderer. Its Harmony dependency is included in this optional profile.
- Re-entry: Firefly 1.0.6.0 and FireflyAPI 1.0.0.0 from the installed collection.
  [Upstream documentation](https://github.com/M1rageDev/Firefly) lists ModuleManager
  and FireflyAPI as dependencies. Its existing effect settings are retained.
- Vapour cones: ShVAK's [VaporCones](https://spacedock.info/mod/3805/VaporCones),
  pinned by content hash because this local folder has no version file.

No third-party assets are committed or redistributed by this profile. The local
Mac port is not a downloadable dependency for Hami's host; use a separately tested
platform-specific terrain installation there. Visual assets need not become
network-authoritative mission state.

## Install

Close the test game first. Install the base mission into a separate copy created
by `tools/mac_preflight.py`. Then run:

```sh
python3 tools/install_visuals.py \
  --ksp-root '/path/to/MegaCrew-Test' \
  --source-ksp '/path/to/existing/KSP' \
  --cloud-archive '/path/to/VolumetricClouds-v3.zip'
```

The source is read only. The installer checks archive and folder hashes, stages
files, omits the Mac port's personal installation metadata and refuses to overwrite
existing visual folders. It requires the isolated-copy marker. The install report
in the test game records completed folders and explicitly leaves runtime unverified.
If a copy fails, inspect the report and partial output before retrying; this tool
never removes folders automatically. Keep the existing game closed during copying
so its configuration cannot change during validation.

## Validation and remaining integration

Installation alone does not verify clouds, terrain, re-entry or vapour rendering.
Check main-menu loader errors, cloud layers in orbit, terrain/scatters on the
surface, and effects during an atmospheric flight. Hermes at 150 km cannot test
re-entry or transonic cones. Use a separate disposable atmospheric test craft.

Both NativeParallax and MegaCrew currently use F8 in flight. This toggles both
panels; use their Close buttons until the source owners agree a configurable
binding. Do not claim the profile is fully integrated until this is resolved and
surface/atmospheric scenes have been observed.

## Local installation evidence — 2026-10-30

The profile installed into the isolated MegaCrew copy. All five installed mod
folder hashes match the source pins; personal installation metadata is absent.
A repeat install is rejected before writing. Existing six preflight tests pass.
The new startup log records loading Harmony, NativeParallax, Firefly, FireflyAPI
and VaporCones assemblies. Rendering checks remain pending. Startup includes
ADDON BINDER resolution warnings for EVEManager/Utils and the pre-existing EVS
utility, plus a missing Firefly Apollo white texture; do not equate assembly
loading with successful visual operation.

The test copy subsequently reached the main menu with cloud cover visible over
Kerbin and the MegaCrew console visible. NativeParallax logged Metal compute
online on Apple M5, terrain shader supported=True, OpenGLCore backend and five
configured bodies. This verifies startup and menu cloud rendering only; it does
not establish surface scatter quality, flight cloud behavior or atmospheric FX.
