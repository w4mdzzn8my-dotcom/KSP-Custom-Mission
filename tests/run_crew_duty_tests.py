"""Exercise production duty eligibility and fit policy with recording game boundaries."""
import argparse,json,pathlib,subprocess,tempfile
p=argparse.ArgumentParser();p.add_argument('--runtime',type=pathlib.Path,required=True);p.add_argument('--bridge',type=pathlib.Path,help='Optional reviewed production DutyReadinessBridge.cs for end-to-end query tests');p.add_argument('--csc',type=pathlib.Path,required=True);a=p.parse_args();root=pathlib.Path(__file__).resolve().parents[1]
def extract(file,name):
 text=file.read_text();start=text.index('public '+name);brace=text.index('{',start);depth=1;end=brace+1
 while depth:
  depth+=(text[end]=='{')-(text[end]=='}');end+=1
 return text[start:end]
if a.bridge and not a.bridge.is_file():p.error('Bridge source does not exist')
with tempfile.TemporaryDirectory() as folder:
 d=pathlib.Path(folder);(d/'Needs.cs').write_text('using System;namespace MegaCrew{'+extract(root/'Source/MegaCrew.cs','sealed class CrewRecord')+extract(root/'Source/CrewNeeds.cs','static class NeedsModel')+'}')
 fixture=(root/'tests/CrewDutyTests.cs').read_text()
 if a.bridge:
  marker='bool ready=CanPerformDuty('
  if fixture.count(marker)!=1:raise SystemExit('Fixture query seam changed; refusing an unverified replacement')
  fixture=fixture.replace(marker,'bool ready=DutyReadinessBridge.CanPerformDuty(')
 (d/'Fixture.cs').write_text(fixture)
 refs=['System.Private.CoreLib.dll','System.Runtime.dll','System.Console.dll','System.Collections.dll']
 args=['-nologo','-nostdlib+','-target:exe',f'-out:"{d}/Tests.dll"']+[f'-reference:"{a.runtime.resolve()/r}"' for r in refs]+[f'"{d}/Needs.cs"',f'"{root}/Source/CrewDuty.cs"',f'"{d}/Fixture.cs"']
 if a.bridge:args.append(f'"{a.bridge.resolve()}"')
 (d/'compile.rsp').write_text('\n'.join(args));subprocess.run(['dotnet',str(a.csc),'@'+str(d/'compile.rsp')],check=True)
 (d/'Tests.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net'+a.runtime.name.split('.')[0]+'.0','framework':{'name':'Microsoft.NETCore.App','version':a.runtime.name}}}));subprocess.run(['dotnet',str(d/'Tests.dll')],check=True)

if a.bridge:print('PASS: actual production DutyReadinessBridge and CanPerformDuty exercised together; KSP/Unity boundaries doubled, no live command execution.')
