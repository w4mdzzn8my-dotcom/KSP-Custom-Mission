"""Compile and run the pure Duna round-trip and fuel/watch coverage tests."""
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
with tempfile.TemporaryDirectory(prefix='duna-route-tests-') as temp:
    temp = pathlib.Path(temp)
    output = temp / 'DunaRoutePlannerTests.dll'
    references = ['System.Private.CoreLib.dll', 'System.Runtime.dll',
                  'System.Console.dll', 'System.Collections.dll']
    command = ['-nologo', '-nostdlib+', '-langversion:7.3', '-target:exe',
               f'-out:"{output}"']
    command += [f'-reference:"{args.runtime.resolve() / name}"' for name in references]
    source_paths=['Source/DunaMission/DunaRoutePlanner.cs','Source/Navigation/Lambert.cs','Source/CampaignTiming.cs','Source/CampaignEpochPlanner.cs','tests/DunaRoutePlannerTests.cs']
    command += [f'"{root / rel}"' for rel in source_paths]
    response = temp / 'args.rsp'
    response.write_text('\n'.join(command) + '\n')
    subprocess.run(['dotnet', str(args.csc), '@' + str(response)], check=True)
    runtime_major = args.runtime.name.split('.')[0]
    (temp / 'DunaRoutePlannerTests.runtimeconfig.json').write_text(json.dumps({
        'runtimeOptions': {
            'tfm': 'net' + runtime_major + '.0',
            'framework': {'name': 'Microsoft.NETCore.App', 'version': args.runtime.name}
        }
    }))
    subprocess.run(['dotnet', str(output)], check=True)
