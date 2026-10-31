"""Tests the staged DLL's actual corrected IL expression, not a copied formula."""
import importlib.util,json,pathlib,struct,unittest,os
ROOT=pathlib.Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('cryo_patch',ROOT/'tools/fix_background_cryo.py')
patch=importlib.util.module_from_spec(spec);spec.loader.exec_module(patch)

class CryoPatchTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  path=os.environ.get('MEGACREW_BGR_DLL')
  if not path:raise unittest.SkipTest('Set MEGACREW_BGR_DLL to the original pinned local BackgroundResources.dll')
  cls.original=pathlib.Path(path).read_bytes();cls.manifest=json.loads((ROOT/'tools/background-cryo-fix.json').read_text())
  cls.fixed=patch.corrected(cls.original,cls.manifest)
  cls.start=cls.manifest['method_file_offset'];cls.size=cls.manifest['method_size']
 def expression(self,data,minutes,pod_count,vessel_count,elapsed,step):
  il=data[self.start:self.start+self.size];stack=[];ip=0xa5
  pod_token=int.from_bytes(il[0x9b:0x9f],'little')
  vessel_token=int.from_bytes(self.original[self.start+0xb9:self.start+0xbd],'little')
  rate_token=int.from_bytes(il[0xa7:0xab],'little')
  while ip<0xc8:
   op=il[ip];ip+=1
   if op==0:continue
   if op in (0x06,0x07,0x09):stack.append({0x06:'vessel',0x07:'pod',0x09:elapsed}[op])
   elif op==0x6f:
    token=int.from_bytes(il[ip:ip+4],'little');ip+=4;receiver=stack.pop()
    if token==pod_token:self.assertEqual(receiver,'pod');stack.append(pod_count)
    elif token==vessel_token:self.assertEqual(receiver,'vessel');stack.append(vessel_count)
    elif token==rate_token:self.assertEqual(receiver,'pod');stack.append(minutes)
    else:self.fail('Unexpected virtual call token')
   elif op==0x23:stack.append(struct.unpack('<d',il[ip:ip+8])[0]);ip+=8
   elif op==0x28:ip+=4;stack.append(step)
   elif op==0x6c:stack[-1]=float(stack[-1])
   elif op in (0x5a,0x5b):
    b=stack.pop();a=stack.pop();stack.append(a*b if op==0x5a else a/b)
   elif op==0x13:
    self.assertEqual(il[ip],4);return stack.pop()
   else:self.fail('Unexpected instruction '+hex(op))
  self.fail('No request amount assignment')
 def test_actual_il_uses_per_pod_elapsed_time_once(self):
  for per_minute in (0,10,60):
   for count in (1,2,4):
    for elapsed in (1,30,540,1000000):
     for step in (0.02,1,54,3456):
      self.assertAlmostEqual(self.expression(self.fixed,per_minute,count,12,elapsed,step),per_minute*count*elapsed/60,places=6)
 def test_original_regression_is_reproduced(self):
  self.assertNotAlmostEqual(self.expression(self.original,10,2,4,30,0.02),10)
 def test_consumption_direction(self):
  self.assertEqual(self.original[self.start+0xe9],0x17) # true: generation
  self.assertEqual(self.fixed[self.start+0xe9],0x16) # false: consumption
 def test_no_changes_outside_target_body(self):
  self.assertEqual(len(self.original),len(self.fixed));self.assertEqual(self.original[:self.start],self.fixed[:self.start]);self.assertEqual(self.original[self.start+self.size:],self.fixed[self.start+self.size:])
 def test_unknown_or_already_patched_input_is_rejected(self):
  with self.assertRaises(ValueError):patch.corrected(self.fixed,self.manifest)
  with self.assertRaises(ValueError):patch.corrected(b'unknown',self.manifest)

if __name__=='__main__':unittest.main()
