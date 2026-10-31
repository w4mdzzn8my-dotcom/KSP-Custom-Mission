"""Test production reflection bridge against recording DeepFreeze/KSP boundaries."""
import argparse,json,pathlib,subprocess,tempfile
p=argparse.ArgumentParser();p.add_argument('--runtime',type=pathlib.Path,required=True);p.add_argument('--csc',type=pathlib.Path,required=True);a=p.parse_args();root=pathlib.Path(__file__).resolve().parents[1]
def extract(file,name):
 text=file.read_text();start=text.index('public '+name);brace=text.index('{',start);depth=1;end=brace+1
 while depth:
  depth+=(text[end]=='{')-(text[end]=='}');end+=1
 return text[start:end]
with tempfile.TemporaryDirectory() as folder:
 d=pathlib.Path(folder);(d/'Bridge.cs').write_text('using System;using System.Reflection;using UnityEngine;namespace MegaCrew{'+extract(root/'Source/MegaCrew.cs','static class CryoBridge')+extract(root/'Source/CryoPowerAudit.cs','static partial class CryoAudit')+'}')
 refs=['System.Private.CoreLib.dll','System.Runtime.dll','System.Console.dll','System.Collections.dll']
 args=['-nologo','-nostdlib+','-target:exe',f'-out:"{d}/Tests.dll"']+[f'-reference:"{a.runtime.resolve()/r}"' for r in refs]+[f'"{d}/Bridge.cs"',f'"{root}/tests/CryoReadinessTests.cs"']
 (d/'compile.rsp').write_text('\n'.join(args));subprocess.run(['dotnet',str(a.csc),'@'+str(d/'compile.rsp')],check=True)
 (d/'Tests.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net'+a.runtime.name.split('.')[0]+'.0','framework':{'name':'Microsoft.NETCore.App','version':a.runtime.name}}}))
 subprocess.run(['dotnet',str(d/'Tests.dll')],check=True)
