"""Stage a reversible, hash-pinned BackgroundResources correction. Never edits input."""
import argparse,hashlib,json,pathlib

def corrected(data,manifest):
 if hashlib.sha256(data).hexdigest()!=manifest['input_sha256']:
  raise ValueError('Unsupported DLL hash; refusing to patch')
 start=manifest['method_file_offset'];end=start+manifest['method_size']
 if hashlib.sha256(data[start:end]).hexdigest()!=manifest['method_sha256']:
  raise ValueError('Method body mismatch')
 output=bytearray(data)
 for change in manifest['changes']:
  offset=change['file_offset'];before=bytes.fromhex(change['before']);after=bytes.fromhex(change['after'])
  if offset!=start+change['il_offset'] or offset<start or offset+len(before)>end or len(before)!=len(after):
   raise ValueError('Invalid same-length method change')
  if output[offset:offset+len(before)]!=before:raise ValueError('Instruction precondition failed')
  output[offset:offset+len(before)]=after
 if output[:start]!=data[:start] or output[end:]!=data[end:]:raise ValueError('Non-method bytes changed')
 return bytes(output)

def main():
 p=argparse.ArgumentParser(description=__doc__)
 p.add_argument('--input',required=True,type=pathlib.Path);p.add_argument('--output',required=True,type=pathlib.Path)
 a=p.parse_args()
 if a.input.resolve()==a.output.resolve():p.error('In-place patching is forbidden; stage a new file and retain the original')
 manifest=json.loads(pathlib.Path(__file__).with_name('background-cryo-fix.json').read_text())
 result=corrected(a.input.read_bytes(),manifest)
 a.output.parent.mkdir(parents=True,exist_ok=True)
 with a.output.open('xb') as f:f.write(result)
 print('Staged candidate only; original untouched. Live KSP acceptance still required.')
 print('SHA256',hashlib.sha256(result).hexdigest())

if __name__=='__main__':main()
