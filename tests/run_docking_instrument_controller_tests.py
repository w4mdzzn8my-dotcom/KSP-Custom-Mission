"""Compile and run the docking-instrument controller against small KSP/Unity fakes."""
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
root = pathlib.Path(__file__).resolve().parents[1]
with tempfile.TemporaryDirectory(prefix='docking-instrument-controller-tests-') as temporary:
    temporary = pathlib.Path(temporary)
    output = temporary / 'DockingInstrumentControllerTests.dll'
    references = ['System.Private.CoreLib.dll', 'System.Runtime.dll',
                  'System.Console.dll', 'System.Collections.dll']
    command = ['-nologo', '-nostdlib+', '-langversion:7.3', '-target:exe',
               f'-out:"{output}"']
    command += [f'-reference:"{args.runtime.resolve() / name}"' for name in references]
    sources = [root / 'Source' / 'DockingGeometryModel.cs',
               root / 'Source' / 'OnboardDockingInstruments.cs',
               root / 'tests' / 'DockingInstrumentControllerFakes.cs',
               root / 'tests' / 'DockingInstrumentControllerTests.cs']
    command += [f'"{source}"' for source in sources]
    response = temporary / 'args.rsp'
    response.write_text('\n'.join(command) + '\n')
    subprocess.run(['dotnet', str(args.csc), '@' + str(response)], check=True)
    runtime_version = args.runtime.name
    runtime_major = runtime_version.split('.')[0]
    (temporary / 'DockingInstrumentControllerTests.runtimeconfig.json').write_text(json.dumps({
        'runtimeOptions': {
            'tfm': 'net' + runtime_major + '.0',
            'framework': {'name': 'Microsoft.NETCore.App', 'version': runtime_version}
        }
    }))
    subprocess.run(['dotnet', str(output)], check=True)
