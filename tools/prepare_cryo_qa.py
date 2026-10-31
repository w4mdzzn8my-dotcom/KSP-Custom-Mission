"""Prepare a fresh disposable KSP copy; never copies the user's saves or server."""
import argparse,hashlib,json,pathlib,shutil,subprocess,sys,zipfile
p=argparse.ArgumentParser();p.add_argument('--source-root',type=pathlib.Path,required=True);p.add_argument('--qa-root',type=pathlib.Path,required=True);p.add_argument('--pristine-archive',type=pathlib.Path,required=True);p.add_argument('--provider-candidate',type=pathlib.Path);a=p.parse_args()
src=a.source_root.resolve();dst=a.qa_root.resolve()
if dst.name!='KSP-Hermes-QA' or dst.exists() or src==dst or src in dst.parents or dst in src.parents:p.error('Use a new directory named KSP-Hermes-QA; never overwrite an existing copy')
if not (src/'KSP.app').is_dir() or not (src/'GameData/MegaCrew').is_dir():p.error('Source must be the separate MegaCrew KSP copy')
if (src/'GameData/LunaMultiplayer').exists():p.error('QA source must not contain multiplayer/server configuration')
provider=pathlib.Path('GameData/REPOSoftTech/BackgroundResources/BackgroundResources.dll')
original_hash=hashlib.sha256((src/provider).read_bytes()).hexdigest()
if original_hash!='ed956b9ec2cc5f414586a52a9086423e4c9125fa72097bce2721ea41eb075bc9':p.error('Unsupported original provider')
if a.provider_candidate and hashlib.sha256(a.provider_candidate.read_bytes()).hexdigest()!='d2c1ab678a0891246dc7b16e66e180ced7e8ad55db8bb9971cc742224dfee45a':p.error('Unrecognized staged provider')
if hashlib.sha256(a.pristine_archive.read_bytes()).hexdigest()!='0071eacf3a1b38a63c611fe2a65877ea2f62f6076b7e97d22e9cdf58b412cf40':p.error('Use the exact published pristine 0.3.0 archive')
with zipfile.ZipFile(a.pristine_archive) as archive:pristine=archive.read('saves/MegaCrew Practice/persistent.sfs')
dst.mkdir(parents=True)
for name in ['KSP.app','GameData']:
 if sys.platform=='darwin':subprocess.run(['/bin/cp','-cR',str(src/name),str(dst/name)],check=True)
 else:shutil.copytree(src/name,dst/name)
for name in ['buildID64.txt','buildID.txt']:
 if (src/name).is_file():shutil.copy2(src/name,dst/name)
# Fresh QA save only; no original saves, logs or personal game state are copied.
save=dst/'saves/MegaCrew Background QA';save.mkdir(parents=True);(save/'persistent.sfs').write_bytes(pristine)
(dst/'HermesCryoQA.scope').write_text('MEGACREW_DISPOSABLE_CRYO_QA_V1\n')
variant='original'
if a.provider_candidate:
 backup=dst/'QA-Backups';backup.mkdir();shutil.copy2(dst/provider,backup/'BackgroundResources.original.dll');shutil.copy2(a.provider_candidate,dst/provider);variant='patched'
(dst/'HermesCryoQA.variant').write_text(variant+'\n')
(dst/'HermesCryoQA.enable').write_text('one-shot\n')
(dst/'HermesCryoQA-preparation.json').write_text(json.dumps({'variant':variant,'source_provider_sha256':original_hash,'qa_provider_sha256':hashlib.sha256((dst/provider).read_bytes()).hexdigest(),'personal_saves_copied':False,'server_data_copied':False,'runtime_started':False},indent=2)+'\n')
print('Prepared disposable QA copy with a pristine save and one-shot marker. Install the QA probe DLL before launching. No game was started.')
