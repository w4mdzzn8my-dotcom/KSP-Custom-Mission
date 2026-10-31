import argparse
import json
from pathlib import Path
import subprocess
import tempfile
root=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser();parser.add_argument('--runtime',required=True,type=Path);parser.add_argument('--csc',required=True,type=Path);args=parser.parse_args()
runtime=args.runtime.resolve();csc=args.csc.resolve()
with tempfile.TemporaryDirectory() as t:
    out=Path(t);refs=['System.Private.CoreLib.dll','System.Runtime.dll','System.Console.dll','System.Security.Cryptography.dll']
    args=['-nologo','-nostdlib+','-target:exe',f'-out:"{out}/Tests.dll"']+[f'-reference:"{runtime/r}"' for r in refs]+[f'"{root}/Source/Navigation/MissionControlMath.cs"',f'"{root}/Source/Navigation/Lambert.cs"',f'"{root}/Source/Navigation/NodeFrame.cs"',f'"{root}/Source/Navigation/BurnApprovals.cs"',f'"{root}/Source/Navigation/PlanFingerprint.cs"',f'"{root}/tests/navigation/MissionControlMathTests.cs"']
    (out/'args.rsp').write_text('\n'.join(args));subprocess.run(['dotnet',str(csc),'@'+str(out/'args.rsp')],check=True)
    (out/'Tests.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net'+runtime.name.split('.')[0]+'.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
    subprocess.run(['dotnet',str(out/'Tests.dll')],check=True)
    args[-1]=f'"{root}/tests/navigation/LambertRandomizedTests.cs"'
    (out/'args.rsp').write_text('\n'.join(args));subprocess.run(['dotnet',str(csc),'@'+str(out/'args.rsp')],check=True)
    subprocess.run(['dotnet',str(out/'Tests.dll')],check=True)

    for source, tests in [('BurnDurationEstimator.cs','BurnDurationTests.cs'),('WatchCoverage.cs','WatchCoverageTests.cs')]:
        case=['-nologo','-nostdlib+','-langversion:7.3','-target:exe',f'-out:"{out}/Tests.dll"']+[f'-reference:"{runtime/r}"' for r in refs]+[f'"{root}/Source/Navigation/{source}"',f'"{root}/tests/navigation/{tests}"']
        (out/'args.rsp').write_text('\n'.join(case));subprocess.run(['dotnet',str(csc),'@'+str(out/'args.rsp')],check=True)
        subprocess.run(['dotnet',str(out/'Tests.dll')],check=True)
