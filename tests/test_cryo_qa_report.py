import importlib.util,pathlib,tempfile,unittest
ROOT=pathlib.Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('cryo_report',ROOT/'tools/inspect_cryo_qa.py');report=importlib.util.module_from_spec(spec);spec.loader.exec_module(report)
class CryoReportTests(unittest.TestCase):
 def fixture(self,root):
  # Synthetic test data, not real game evidence.
  (root/'HermesCryoQA.scope').write_text(report.SCOPE);(root/'HermesCryoQA.variant').write_text('patched');(root/'HermesCryoQA.done').touch()
  return dict(schema='1',stage='procedure_finished',run_state='completed',probe_completed='True',flight_qualification='False',variant='patched',provider_sha256=report.HASHES['patched'],actual_frozen_before_unload='True',actual_frozen_after_unload='True',frozen_after_reload='True',actual_thaw_complete='True',initial_parts='17',initial_awake_crew='4',awake_after_reload='3',final_awake_crew='4',unloaded_before_ec='1000',unloaded_after_ec='990',unloaded_elapsed_ut='45',pod_accounted_seconds='45',pod_expected_ec='10',observed_ec_delta='-10')
 def write(self,root,data):
  (root/'HermesCryoQA-result.cfg').write_text(''.join(k+' = '+v+'\n' for k,v in data.items()))
 def test_absent_report_is_incomplete(self):
  with tempfile.TemporaryDirectory() as d:
   root=pathlib.Path(d)/'KSP-Hermes-QA';root.mkdir();self.fixture(root);self.assertEqual(report.inspect(root)['status'],'incomplete')
 def test_synthetic_complete_never_certifies_flight(self):
  with tempfile.TemporaryDirectory() as d:
   root=pathlib.Path(d)/'KSP-Hermes-QA';root.mkdir();self.write(root,self.fixture(root));result=report.inspect(root);self.assertTrue(result['procedure_complete']);self.assertFalse(result['flight_qualified'])
 def test_corrupt_or_contradictory_evidence_cannot_complete(self):
  cases=[('observed_ec_delta','-9'),('unloaded_elapsed_ut','nan'),('pod_accounted_seconds','0'),('provider_sha256','wrong'),('flight_qualification','True'),('actual_thaw_complete','False'),('final_awake_crew','3'),('stage','thawing_crew')]
  for key,value in cases:
   with self.subTest(key=key),tempfile.TemporaryDirectory() as d:
    root=pathlib.Path(d)/'KSP-Hermes-QA';root.mkdir();data=self.fixture(root);data[key]=value;self.write(root,data);result=report.inspect(root);self.assertEqual(result['status'],'invalid');self.assertFalse(result['procedure_complete'])
 def test_failure_and_stale_markers_are_not_success(self):
  with tempfile.TemporaryDirectory() as d:
   root=pathlib.Path(d)/'KSP-Hermes-QA';root.mkdir();data=self.fixture(root);data['failure']='Freeze timeout';self.write(root,data);self.assertEqual(report.inspect(root)['status'],'failed');del data['failure'];self.write(root,data);(root/'HermesCryoQA.running').touch();self.assertEqual(report.inspect(root)['status'],'invalid')
 def test_saved_bare_and_explicit_wrapper_forms_match(self):
  bare="schema = 1\nprobe_completed = False\n"
  self.assertEqual(report.parse(bare),report.parse("HERMES_CRYO_QA\n{\n"+bare+"}\n"))
 def test_inconclusive_precondition_is_incomplete_not_a_provider_failure(self):
  with tempfile.TemporaryDirectory() as d:
   root=pathlib.Path(d)/'KSP-Hermes-QA';root.mkdir();data=self.fixture(root)
   data.update(run_state='inconclusive',probe_completed='False',failure='Electricity requirement disabled')
   self.write(root,data);result=report.inspect(root)
   self.assertEqual(result['status'],'incomplete');self.assertFalse(result['procedure_complete']);self.assertFalse(result['flight_qualified'])
   data['probe_completed']='True';self.write(root,data);self.assertEqual(report.inspect(root)['status'],'invalid')
 def test_duplicate_keys_rejected(self):
  with self.assertRaises(ValueError):report.parse('HERMES_CRYO_QA\n{\nprobe_completed = False\nprobe_completed = True\n}\n')
if __name__=='__main__':unittest.main()
