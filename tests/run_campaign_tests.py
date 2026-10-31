"""Run pure production campaign timing and coast decisions without launching Unity/KSP."""
import argparse,json,pathlib,subprocess,tempfile
p=argparse.ArgumentParser();p.add_argument('--runtime',type=pathlib.Path,required=True);p.add_argument('--csc',type=pathlib.Path,required=True);a=p.parse_args()
root=pathlib.Path(__file__).resolve().parents[1]
with tempfile.TemporaryDirectory() as d:
 d=pathlib.Path(d);(d/'Model.cs').write_text((root/'Source/CampaignTiming.cs').read_text());(d/'Coast.cs').write_text((root/'Source/CampaignCoast.cs').read_text());ration=(root/'Source/CampaignSupplies.cs').read_text();(d/'Rations.cs').write_text(ration[:ration.index('    public sealed partial class MissionConsole')].replace('using UnityEngine;','')+'}')
 refs=['System.Private.CoreLib.dll','System.Runtime.dll','System.Console.dll']
 args=['-nologo','-nostdlib+','-target:exe',f'-out:"{d}/Tests.dll"']+[f'-reference:"{a.runtime.resolve()/r}"' for r in refs]+[f'"{d}/Model.cs"',f'"{d}/Coast.cs"',f'"{d}/Rations.cs"',f'"{root}/Source/CryoPowerModel.cs"',f'"{root}/tests/CampaignTimingTests.cs"']
 (d/'args.rsp').write_text('\n'.join(args))
 subprocess.run(['dotnet',str(a.csc),'@'+str(d/'args.rsp')],check=True)
 (d/'Tests.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net'+a.runtime.name.split('.')[0]+'.0','framework':{'name':'Microsoft.NETCore.App','version':a.runtime.name}}}))
 subprocess.run(['dotnet',str(d/'Tests.dll')],check=True)

def actual_class(file,name):
 text=file.read_text();start=text.index('public '+name);brace=text.index('{',start);depth=1;end=brace+1
 while depth:
  depth+=(text[end]=='{')-(text[end]=='}');end+=1
 return text[start:end]
# Exercise the production vessel adapter against minimal KSP/Unity test doubles.
with tempfile.TemporaryDirectory() as folder:
 d=pathlib.Path(folder)
 (d/'Needs.cs').write_text('using System; namespace MegaCrew {'+actual_class(root/'Source/MegaCrew.cs','sealed class CrewRecord')+actual_class(root/'Source/CrewNeeds.cs','static class NeedsModel')+'}')
 refs=['System.Private.CoreLib.dll','System.Runtime.dll','System.Console.dll','System.Collections.dll']
 args=['-nologo','-nostdlib+','-target:exe',f'-out:"{d}/AdapterTests.dll"']+[f'-reference:"{a.runtime.resolve()/r}"' for r in refs]+[f'"{root}/Source/CampaignTiming.cs"',f'"{root}/Source/CampaignSupplies.cs"',f'"{root}/Source/DailyRoutine.cs"',f'"{d}/Needs.cs"',f'"{root}/tests/CampaignSupplyAdapterTests.cs"']
 (d/'args.rsp').write_text('\n'.join(args))
 subprocess.run(['dotnet',str(a.csc),'@'+str(d/'args.rsp')],check=True)
 (d/'AdapterTests.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net'+a.runtime.name.split('.')[0]+'.0','framework':{'name':'Microsoft.NETCore.App','version':a.runtime.name}}}))
 subprocess.run(['dotnet',str(d/'AdapterTests.dll')],check=True)

# Save/load the production ScenarioModule logic, including legacy shortage state.
with tempfile.TemporaryDirectory() as folder:
 d=pathlib.Path(folder)
 production='using System;using System.Globalization;using System.Collections.Generic;namespace MegaCrew {'+actual_class(root/'Source/MegaCrew.cs','sealed class CrewRecord')+actual_class(root/'Source/Campaign.cs','sealed class HermesCampaign')+'}'
 (d/'Scenario.cs').write_text(production)
 refs=['System.Private.CoreLib.dll','System.Runtime.dll','System.Console.dll','System.Collections.dll','System.Globalization.dll']
 args=['-nologo','-nostdlib+','-target:exe',f'-out:"{d}/ScenarioTests.dll"']+[f'-reference:"{a.runtime.resolve()/r}"' for r in refs]+[f'"{d}/Scenario.cs"',f'"{root}/Source/CrewStateCodec.cs"',f'"{root}/Source/CampaignTiming.cs"',f'"{root}/tests/CampaignPersistenceTests.cs"']
 (d/'compile.rsp').write_text('\n'.join(args));subprocess.run(['dotnet',str(a.csc),'@'+str(d/'compile.rsp')],check=True)
 (d/'ScenarioTests.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net'+a.runtime.name.split('.')[0]+'.0','framework':{'name':'Microsoft.NETCore.App','version':a.runtime.name}}}))
 subprocess.run(['dotnet',str(d/'ScenarioTests.dll')],check=True)
