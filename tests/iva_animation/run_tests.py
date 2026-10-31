"""Compile the actual KSP IVA catalog probe against read-only fakes and run its lifecycle tests."""
import argparse
import json
import pathlib
import subprocess
import tempfile

parser = argparse.ArgumentParser()
parser.add_argument('--runtime', type=pathlib.Path, required=True,
                    help='Path to a shared Microsoft.NETCore.App/VERSION directory')
parser.add_argument('--csc', type=pathlib.Path, required=True,
                    help='Path to Roslyn bincore/csc.dll')
args = parser.parse_args()
repo = pathlib.Path(__file__).resolve().parents[2]
test_dir = pathlib.Path(__file__).resolve().parent
with tempfile.TemporaryDirectory(prefix='iva-animation-catalog-tests-') as tmp:
    tmp = pathlib.Path(tmp)
    output = tmp / 'IvaAnimationCatalogProbeTests.dll'
    refs = ['System.Private.CoreLib.dll', 'System.Runtime.dll', 'System.Console.dll',
            'System.Collections.dll', 'System.IO.FileSystem.dll', 'System.Reflection.dll']
    command = ['-nologo', '-nostdlib+', '-langversion:7.3', '-target:exe', f'-out:"{output}"']
    command += [f'-reference:"{args.runtime.resolve() / name}"' for name in refs]
    sources = [repo / 'tests' / 'ksp' / 'IvaAnimationCatalogProbe.cs',
               test_dir / 'IvaAnimationProbeFakes.cs',
               test_dir / 'IvaAnimationCatalogProbeTests.cs']
    command += [f'"{source}"' for source in sources]
    response = tmp / 'args.rsp'
    response.write_text('\n'.join(command) + '\n')
    subprocess.run(['dotnet', str(args.csc), '@' + str(response)], check=True)
    version = args.runtime.name
    major = version.split('.')[0]
    (tmp / 'IvaAnimationCatalogProbeTests.runtimeconfig.json').write_text(json.dumps({
        'runtimeOptions': {'tfm': 'net' + major + '.0',
                           'framework': {'name': 'Microsoft.NETCore.App', 'version': version}}
    }))
    subprocess.run(['dotnet', str(output)], check=True)
