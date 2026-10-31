"""Read-only procedural evidence check. Never certifies flight or enables warp."""
import argparse,json,math,pathlib
SCOPE='MEGACREW_DISPOSABLE_CRYO_QA_V1'
HASHES={'original':'ed956b9ec2cc5f414586a52a9086423e4c9125fa72097bce2721ea41eb075bc9','patched':'d2c1ab678a0891246dc7b16e66e180ced7e8ad55db8bb9971cc742224dfee45a'}
def parse(text):
 lines=[x.strip() for x in text.splitlines() if x.strip()]
 if not lines:raise ValueError('Empty report')
 if lines[0]=='HERMES_CRYO_QA':
  if len(lines)<3 or lines[1]!='{' or lines[-1]!='}':raise ValueError('Invalid report structure')
  lines=lines[2:-1]
 if any(x in ['{','}'] for x in lines):raise ValueError('Unexpected nested report')
 result={}
 for line in lines:
  if '=' not in line:raise ValueError('Invalid report field')
  key,value=map(str.strip,line.split('=',1))
  if not key or key in result:raise ValueError('Duplicate or empty report key')
  result[key]=value
 return result

def inspect(root):
 root=pathlib.Path(root)
 result={'status':'incomplete','procedure_complete':False,'flight_qualified':False,'problems':[]}
 try:
  if root.name!='KSP-Hermes-QA' or (root/'HermesCryoQA.scope').read_text().strip()!=SCOPE:raise ValueError('Not a marked disposable QA root')
  path=root/'HermesCryoQA-result.cfg'
  if not path.exists():result['problems'].append('No runtime report exists');return result
  data=parse(path.read_text());result['stage']=data.get('stage','unknown')
  if data.get('run_state')=='inconclusive':
   if data.get('probe_completed','').lower()!='false' or data.get('flight_qualification','').lower()!='false':raise ValueError('Inconclusive report contradicts its qualification fields')
   result['problems'].append(data.get('failure','Probe recorded an inconclusive prerequisite or observation'));return result
  if 'failure' in data or data.get('run_state')=='failed':
   result['status']='failed';result['problems'].append(data.get('failure','Probe recorded failure'));return result
  if data.get('probe_completed','').lower()!='true' or data.get('run_state')!='completed':
   result['problems'].append('Run has not recorded procedural completion');return result
  if not (root/'HermesCryoQA.done').is_file() or any((root/('HermesCryoQA.'+x)).exists() for x in ['enable','running']):raise ValueError('Completion markers disagree with report')
  if data.get('schema')!='1' or data.get('stage')!='procedure_finished':raise ValueError('Unsupported schema or unfinished stage')
  if data.get('flight_qualification','').lower()!='false':raise ValueError('Unexpected flight-qualification claim')
  variant=data.get('variant')
  if variant not in HASHES or data.get('provider_sha256')!=HASHES[variant]:raise ValueError('Provider identity mismatch')
  if (root/'HermesCryoQA.variant').read_text().strip()!=variant:raise ValueError('Variant marker mismatch')
  for key in ['actual_frozen_before_unload','actual_frozen_after_unload','frozen_after_reload','actual_thaw_complete']:
   if data.get(key,'').lower()!='true':raise ValueError('Missing actual-state evidence: '+key)
  for key,expected in [('initial_parts',17),('initial_awake_crew',4),('awake_after_reload',3),('final_awake_crew',4)]:
   if float(data.get(key,'nan'))!=expected:raise ValueError('Unexpected '+key)
  values={key:float(data.get(key,'nan')) for key in ['unloaded_before_ec','unloaded_after_ec','unloaded_elapsed_ut','pod_accounted_seconds','pod_expected_ec','observed_ec_delta']}
  if not all(math.isfinite(v) for v in values.values()):raise ValueError('Nonfinite or missing power evidence')
  for key in ['unloaded_elapsed_ut','pod_accounted_seconds','pod_expected_ec']:
   if values[key]<=0:raise ValueError('No positive interval/demand evidence: '+key)
  if min(values['unloaded_before_ec'],values['unloaded_after_ec'])<0:raise ValueError('Negative battery amount')
  delta=values['unloaded_after_ec']-values['unloaded_before_ec']
  if not math.isclose(delta,values['observed_ec_delta'],rel_tol=1e-9,abs_tol=1e-6):raise ValueError('Battery delta is inconsistent')
  if variant=='patched' and delta>=0:raise ValueError('Patched single-pod observation has no net EC draw')
  result.update(status='procedure_complete',procedure_complete=True,variant=variant)
  result['problems'].append('Single-pod 1x evidence only; no high-rate, thermal, failure-policy or multiple-pod qualification')
 except (OSError,ValueError) as e:
  result['status']='invalid';result['problems'].append(str(e))
 return result

def main():
 p=argparse.ArgumentParser();p.add_argument('qa_root',type=pathlib.Path);a=p.parse_args();result=inspect(a.qa_root);print(json.dumps(result,indent=2))
 return 0 if result['status']=='procedure_complete' else 2 if result['status']=='incomplete' else 1
if __name__=='__main__':raise SystemExit(main())
