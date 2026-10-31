import pathlib,subprocess,sys,tempfile,unittest
ROOT=pathlib.Path(__file__).resolve().parents[1]
class CryoQaGuards(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  for name in ['prepare_cryo_qa.py','build_cryo_qa.py']:
   if not (ROOT/'tools'/name).is_file():raise AssertionError('Missing actual QA tool: '+name)
 def prepare(self,src,dst):
  return subprocess.run([sys.executable,str(ROOT/'tools/prepare_cryo_qa.py'),'--source-root',str(src),'--qa-root',str(dst),'--pristine-archive',str(src/'unused.zip')],capture_output=True,text=True)
 def test_existing_qa_destination_is_never_overwritten(self):
  with tempfile.TemporaryDirectory() as d:
   root=pathlib.Path(d);src=root/'source';src.mkdir();dst=root/'KSP-Hermes-QA';dst.mkdir();sentinel=dst/'keep.sfs';sentinel.write_text('existing test state')
   result=self.prepare(src,dst);self.assertNotEqual(result.returncode,0);self.assertIn("never overwrite",result.stderr);self.assertEqual(sentinel.read_text(),'existing test state');self.assertEqual(len(list(dst.iterdir())),1)
 def test_nested_destination_is_rejected_before_copy(self):
  with tempfile.TemporaryDirectory() as d:
   src=pathlib.Path(d)/'source';src.mkdir();dst=src/'KSP-Hermes-QA'
   result=self.prepare(src,dst);self.assertNotEqual(result.returncode,0);self.assertIn("never overwrite",result.stderr);self.assertFalse(dst.exists())
 def test_probe_builder_rejects_unmarked_game(self):
  with tempfile.TemporaryDirectory() as d:
   k=pathlib.Path(d)/'KSP-Hermes-QA';k.mkdir()
   result=subprocess.run([sys.executable,str(ROOT/'tools/build_cryo_qa.py'),'--ksp-root',str(k),'--csc',str(k/'must-not-run.dll')],capture_output=True,text=True)
   self.assertNotEqual(result.returncode,0);self.assertIn("HermesCryoQA.scope",result.stderr);self.assertFalse((k/'GameData').exists())
if __name__=='__main__':unittest.main()
