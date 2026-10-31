"""Run the production pre-start campaign epoch planner against schedule invariants."""
import argparse,json,pathlib,subprocess,tempfile
p=argparse.ArgumentParser();p.add_argument('--runtime',type=pathlib.Path,required=True);p.add_argument('--csc',type=pathlib.Path,required=True);a=p.parse_args();root=pathlib.Path(__file__).resolve().parents[1]
with tempfile.TemporaryDirectory() as folder:
 d=pathlib.Path(folder);refs=['System.Private.CoreLib.dll','System.Runtime.dll','System.Console.dll','System.Collections.dll']
 args=['-nologo','-nostdlib+','-target:exe',f'-out:"{d}/Tests.dll"']+[f'-reference:"{a.runtime.resolve()/r}"' for r in refs]+[f'"{root}/{f}"' for f in ['Source/CampaignTiming.cs','Source/CampaignEpochPlanner.cs','tests/CampaignEpochPlannerTests.cs']]
 (d/'compile.rsp').write_text('\n'.join(args));subprocess.run(['dotnet',str(a.csc),'@'+str(d/'compile.rsp')],check=True)
 (d/'Tests.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net'+a.runtime.name.split('.')[0]+'.0','framework':{'name':'Microsoft.NETCore.App','version':a.runtime.name}}}));subprocess.run(['dotnet',str(d/'Tests.dll')],check=True)
