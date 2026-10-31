"""Install the pinned optional visuals into a marked, isolated MegaCrew Mac copy."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import tempfile
import zipfile

EXCLUDED = {'installation.json', '.DS_Store'}
MODS = ('NativeParallax', '000_Harmony', 'Firefly', 'FireflyAPI', 'VaporCones')
CLOUDS = ('EnvironmentalVisualEnhancements', 'Scatterer', 'StockVolumetricClouds')


def tree_digest(root):
    h = hashlib.sha256()
    count = 0
    for f in sorted(root.rglob('*')):
        if f.is_symlink():
            raise ValueError('Symlinks are not supported: ' + str(f))
        if f.is_file() and f.name not in EXCLUDED:
            h.update(f.relative_to(root).as_posix().encode() + b'\0' + hashlib.sha256(f.read_bytes()).digest())
            count += 1
    return {'tree_sha256': h.hexdigest(), 'files': count}


def install(game, source, archive, profile):
    game, source = game.resolve(), source.resolve()
    if game == source or game in source.parents or source in game.parents:
        raise ValueError('Source and test game must be separate, non-nested paths')
    if not (game / 'MEGACREW-STOCK-COPY.txt').is_file():
        raise ValueError('Use the isolated copy created by mac_preflight.py')
    gd = game / 'GameData'
    for name in MODS + CLOUDS:
        if (gd / name).exists():
            raise ValueError('Refusing to overwrite ' + name)
    if not (gd / 'MegaCrew/Plugins/MegaCrew.dll').is_file():
        raise ValueError('Install the base mission first')
    if hashlib.sha256(archive.read_bytes()).hexdigest() != profile['clouds']['sha256']:
        raise ValueError('Cloud archive hash mismatch')
    for name in MODS:
        if tree_digest(source / 'GameData' / name) != profile['local_mods'][name]:
            raise ValueError('Local mod hash mismatch: ' + name)
    # Validate and stage everything before touching GameData.
    with tempfile.TemporaryDirectory(prefix='megacrew-visuals-') as tmp:
        stage = Path(tmp).resolve()
        with zipfile.ZipFile(archive) as z:
            for info in z.infolist():
                if not (stage / info.filename).resolve().is_relative_to(stage):
                    raise ValueError('Unsafe ZIP path')
            z.extractall(stage)
        for name in MODS:
            shutil.copytree(source / 'GameData' / name, stage / 'GameData' / name,
                            ignore=shutil.ignore_patterns(*EXCLUDED))
        for name in MODS + CLOUDS:
            if not (stage / 'GameData' / name).is_dir():
                raise ValueError('Missing staged folder: ' + name)
        installed = []
        try:
            for name in MODS + CLOUDS:
                shutil.copytree(stage / 'GameData' / name, gd / name)
                installed.append(name)
        finally:
            (game / 'MegaCrew-visual-install.json').write_text(json.dumps({
                'installed_folders': installed, 'profile': profile,
                'runtime_verified': False,
            }, indent=2) + '\n')
    print('Installed pinned visual profile. Runtime rendering remains unverified.')


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--ksp-root', type=Path, required=True)
    p.add_argument('--source-ksp', type=Path, required=True)
    p.add_argument('--cloud-archive', type=Path, required=True)
    args = p.parse_args()
    profile = json.loads((Path(__file__).resolve().parents[1] / 'visual-profile.json').read_text())
    try:
        install(args.ksp_root, args.source_ksp, args.cloud_archive, profile)
    except (OSError, ValueError, zipfile.BadZipFile) as exc:
        p.exit(1, str(exc) + '\n')


if __name__ == '__main__':
    main()
