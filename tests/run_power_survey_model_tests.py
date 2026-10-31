"""Compile and run the pure observed-power survey model tests."""
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
with tempfile.TemporaryDirectory(prefix='power-survey-tests-') as temp:
    temp = pathlib.Path(temp)
    output = temp / 'PowerSurveyModelTests.dll'
    references = ['System.Private.CoreLib.dll', 'System.Runtime.dll',
                  'System.Console.dll', 'System.Collections.dll']
    command = ['-nologo', '-nostdlib+', '-langversion:7.3', '-target:exe',
               f'-out:"{output}"']
    command += [f'-reference:"{args.runtime.resolve() / name}"' for name in references]
    command += [f'"{root / "Source" / "PowerSurveyModel.cs"}"',
                f'"{root / "tests" / "PowerSurveyModelTests.cs"}"']
    response = temp / 'args.rsp'
    response.write_text('\n'.join(command) + '\n')
    subprocess.run(['dotnet', str(args.csc), '@' + str(response)], check=True)
    runtime_major = args.runtime.name.split('.')[0]
    (temp / 'PowerSurveyModelTests.runtimeconfig.json').write_text(json.dumps({
        'runtimeOptions': {
            'tfm': 'net' + runtime_major + '.0',
            'framework': {'name': 'Microsoft.NETCore.App', 'version': args.runtime.name}
        }
    }))
    subprocess.run(['dotnet', str(output)], check=True)
