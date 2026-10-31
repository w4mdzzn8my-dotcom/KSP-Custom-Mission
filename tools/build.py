"""Compile the custom plugin against the user's own KSP assemblies; no NuGet."""
import argparse, pathlib, subprocess, tempfile
p=argparse.ArgumentParser();p.add_argument('--managed',required=True,type=pathlib.Path);p.add_argument('--csc',required=True,type=pathlib.Path);p.add_argument('--output',type=pathlib.Path,help='Write a candidate DLL outside GameData');a=p.parse_args()
root=pathlib.Path(__file__).resolve().parents[1]
refs=sorted(a.managed.glob('*.dll'))
if not refs or not (a.managed/'Assembly-CSharp.dll').is_file():p.error('Select KSP 1.12.5 Data/Managed, not the game root')
out=(a.output.resolve() if a.output else root/'GameData/MegaCrew/Plugins/MegaCrew.dll');out.parent.mkdir(parents=True,exist_ok=True)
sources=sorted((root/'Source').rglob('*.cs'))
if not sources:p.error('No C# sources found')
args=['-nologo','-target:library','-nostdlib+','-langversion:7.3',f'-out:"{out}"']+[f'-reference:"{r}"' for r in refs]+[f'"{r}"' for r in sources]
with tempfile.TemporaryDirectory() as d:
 rsp=pathlib.Path(d)/'compile.rsp';rsp.write_text('\n'.join(args)+'\n');subprocess.run(['dotnet',str(a.csc),'@'+str(rsp)],check=True)
print('Built',out,'from',len(sources),'C# sources')
