"""Publishable source-only delta against the original archive; never includes saves/DLLs."""
import argparse,difflib,hashlib,pathlib,subprocess,tempfile,zipfile
p=argparse.ArgumentParser()
p.add_argument('--base-zip',required=True,type=pathlib.Path)
p.add_argument('--output',required=True,type=pathlib.Path)
a=p.parse_args();root=pathlib.Path(__file__).resolve().parents[1]
files=sorted(set([x.relative_to(root).as_posix() for x in (root/'Source').glob('*.cs')]+[x.relative_to(root).as_posix() for x in (root/'tests').glob('*.cs')]+[x.relative_to(root).as_posix() for x in (root/'tests').glob('*.py')]+['docs/CAMPAIGN-SHIFTS.md','docs/RELEASE-GATES.md','tools/package_candidate.py','tools/fix_background_cryo.py','tools/background-cryo-fix.json','docs/BACKGROUND-CRYO-FIX.md','tools/prepare_cryo_qa.py','tools/build_cryo_qa.py','tests/ksp/HermesCryoProbe.cs','docs/CRYO-QA.md','tools/inspect_cryo_qa.py','docs/CONSOLE-INPUT.md','docs/SUPPLY-DEPLETION.md','docs/CRYO-READINESS.md','docs/SOURCE-CANDIDATE.md','docs/CREW-DUTY.md','docs/CAMPAIGN-STATE-GUARDS.md','docs/CAMPAIGN-EPOCH-PLANNING.md']))
parts=[];expected={}
with zipfile.ZipFile(a.base_zip) as archive,tempfile.TemporaryDirectory(prefix='hermes-source-check-') as folder:
 folder=pathlib.Path(folder)
 for rel in files:
  target=(root/rel).read_text();expected[rel]=target
  try:before=archive.read(rel).decode('utf-8');exists=True
  except KeyError:before='';exists=False
  if exists:
   dest=folder/rel;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_text(before)
  if before==target:continue
  parts.append('diff --git a/'+rel+' b/'+rel+'\n')
  if not exists:parts.append('new file mode 100644\n')
  parts.extend(difflib.unified_diff(before.splitlines(True),target.splitlines(True),fromfile='a/'+rel if exists else '/dev/null',tofile='b/'+rel))
 a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(''.join(parts))
 patch=str(a.output.resolve())
 subprocess.run(['git','apply','--check',patch],cwd=folder,check=True)
 subprocess.run(['git','apply',patch],cwd=folder,check=True)
 for rel,target in expected.items():
  if (folder/rel).read_text()!=target:raise SystemExit('Round-trip mismatch: '+rel)
print('PASS: applied source packet matches every selected production/test/doc file; no binaries or saves.')
print('SHA256',hashlib.sha256(a.output.read_bytes()).hexdigest())
