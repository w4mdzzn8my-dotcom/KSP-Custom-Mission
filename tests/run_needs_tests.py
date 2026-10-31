"""Run pure production need-model logic without launching Unity/KSP."""
import argparse,json,pathlib,subprocess,tempfile
p=argparse.ArgumentParser();p.add_argument('--runtime',type=pathlib.Path,required=True);p.add_argument('--csc',type=pathlib.Path,required=True);a=p.parse_args()
root=pathlib.Path(__file__).resolve().parents[1]
def actual_class(file,name):
 text=file.read_text();start=text.index('public '+name);brace=text.index('{',start);depth=1;end=brace+1
 while depth:
  depth+= (text[end]=='{')-(text[end]=='}');end+=1
 return text[start:end]
model='using System; namespace MegaCrew {\n'+actual_class(root/'Source/MegaCrew.cs','sealed class CrewRecord')+'\n'+actual_class(root/'Source/CrewNeeds.cs','static class NeedsModel')+'\n}'
with tempfile.TemporaryDirectory() as d:
 d=pathlib.Path(d);(d/'Model.cs').write_text(model)
 refs=['System.Private.CoreLib.dll','System.Runtime.dll','System.Console.dll','System.Collections.dll','System.Globalization.dll']
 args=['-nologo','-nostdlib+','-target:exe',f'-out:"{d}/Tests.dll"']+[f'-reference:"{a.runtime.resolve()/r}"' for r in refs]+[f'"{d}/Model.cs"',f'"{root}/Source/CrewStateCodec.cs"',f'"{root}/Source/CampaignTiming.cs"',f'"{root}/Source/DailyRoutine.cs"',f'"{root}/tests/NeedsModelTests.cs"']
 (d/'args.rsp').write_text('\n'.join(args))
 subprocess.run(['dotnet',str(a.csc),'@'+str(d/'args.rsp')],check=True)
 (d/'Tests.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net'+a.runtime.name.split('.')[0]+'.0','framework':{'name':'Microsoft.NETCore.App','version':a.runtime.name}}}))
 subprocess.run(['dotnet',str(d/'Tests.dll')],check=True)
