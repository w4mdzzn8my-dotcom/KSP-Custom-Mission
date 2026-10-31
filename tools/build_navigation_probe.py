#!/usr/bin/env python3
"""Compile the disposable QA probe against local KSP and navigation candidate DLLs."""
import argparse
from pathlib import Path
import subprocess
import tempfile

parser = argparse.ArgumentParser()
parser.add_argument('--managed', required=True)
parser.add_argument('--navigation', required=True)
parser.add_argument('--csc', required=True)
parser.add_argument('--output', required=True)
args = parser.parse_args()
managed = Path(args.managed).resolve()
navigation = Path(args.navigation).resolve()
csc = Path(args.csc).resolve()
source = Path(__file__).resolve().parents[1] / 'tests/ksp/CircularNodeProbe.cs'
out = Path(args.output).resolve()
if not (managed / 'Assembly-CSharp.dll').is_file():
    parser.error('--managed must point to KSP Data/Managed')
if not navigation.is_file():
    parser.error('--navigation candidate DLL does not exist')
refs = sorted(managed.glob('*.dll'))
rsp_args = ['-nologo', '-target:library', '-nostdlib+', '-langversion:7.3', f'-out:"{out}"']
rsp_args += [f'-reference:"{ref}"' for ref in refs]
rsp_args += [f'-reference:"{navigation}"', f'"{source}"']
out.parent.mkdir(parents=True, exist_ok=True)
with tempfile.TemporaryDirectory() as temp:
    rsp = Path(temp) / 'probe.rsp'
    rsp.write_text('\n'.join(rsp_args) + '\n')
    subprocess.run(['dotnet', str(csc), f'@{rsp}'], check=True)
print(f'Compiled {out}')
