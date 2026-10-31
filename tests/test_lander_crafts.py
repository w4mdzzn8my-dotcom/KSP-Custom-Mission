import importlib.util
import hashlib
import json
import contextlib
import io
import math
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('landers',ROOT/'tools/build_landers.py')
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)

def craft(name):return m.parse((ROOT/'Ships/VAB'/name).read_text())
def parts(root):return {p.get('part'):p for p in root.nodes('PART')}
def rotate(q,v):
    x,y,z,w=q;vx,vy,vz=v
    tx,ty,tz=2*(y*vz-z*vy),2*(z*vx-x*vz),2*(x*vy-y*vx)
    return (vx+w*tx+y*tz-z*ty,vy+w*ty+z*tx-x*tz,vz+w*tz+x*ty-y*tx)

class LanderCraftTests(unittest.TestCase):
    def test_connected_trees_with_unique_persistent_ids(self):
        for name in ('Hermes Finch Vacuum Lander.craft','Hermes Finch Docking Cradle.craft'):
            root=craft(name);p=parts(root)
            self.assertEqual(len(p),len(root.nodes('PART')))
            for key in p:
                name_part, ident = key.split('_')
                self.assertTrue(name_part and ident.isdigit(), key)
            self.assertEqual(len(set(n.get('persistentId') for n in p.values())),len(p))
            children={k:[v for key,v in n.values if key=='link'] for k,n in p.items()}
            parents={k:[] for k in p}
            for key,links in children.items():
                for link in links:self.assertIn(link,p);parents[link].append(key)
            roots=[k for k,v in parents.items() if not v];self.assertEqual(len(roots),1)
            visited=set()
            def walk(key):
                self.assertNotIn(key,visited);visited.add(key)
                for child in children[key]:walk(child)
            walk(roots[0]);self.assertEqual(visited,set(p))
            for k,n in p.items():
                self.assertLessEqual(len(parents[k]),1)
                if parents[k]:self.assertLess(list(p).index(parents[k][0]),list(p).index(k))
                for key,v in n.values:
                    if key in ('attN','srfN'):self.assertIn(v.split(',')[1].split('_0|')[0],p)
                self.assertTrue(all(math.isfinite(x) for x in m.vector(n.get('pos'))))
                self.assertAlmostEqual(sum(x*x for x in m.vector(n.get('rot'))),1,places=7)

    def test_docking_faces_coincide_and_face_each_other(self):
        p=parts(craft('Hermes Finch Docking Cradle.craft'))
        dock=p['dockingPort2_979000000']
        other=p[next(v for k,v in dock.values if k=='link')]
        faces=[];axes=[]
        for n in (dock,other):
            pos=m.vector(n.get('pos'));q=m.vector(n.get('rot'))
            offset=rotate(q,(0,.2828832,0));axis=rotate(q,(0,1,0))
            faces.append(tuple(a+b for a,b in zip(pos,offset)));axes.append(axis)
        self.assertLess(math.dist(*faces),1e-6)
        self.assertLess(sum(a*b for a,b in zip(*axes)),-.999999)

    def test_lander_has_return_fuel_and_no_separation_explosives(self):
        p=parts(craft('Hermes Finch Vacuum Lander.craft'))
        self.assertEqual(len([k for k in p if k.startswith('landingLeg1-2_')]),4)
        self.assertEqual(len([k for k in p if k.startswith('RCSBlock.v2_')]),4)
        self.assertFalse(any('decoupler' in k.lower() for k in p))
        fuel={}
        for n in p.values():
            for r in n.nodes('RESOURCE'):fuel[r.get('name')]=fuel.get(r.get('name'),0)+float(r.get('amount'))
        self.assertEqual(fuel['LiquidFuel'],360)
        self.assertEqual(fuel['Oxidizer'],440)
        self.assertEqual(fuel['MonoPropellant'],40)
        self.assertGreater(fuel['ElectricCharge'],100)

    def test_regeneration_preserves_historical_evidence_and_is_deterministic(self):
        names=('mk2LanderCabin_v2','Rockomax8BW','liquidEngine3_v2','dockingPort2','landingLeg1-2','RCSBlock_v2','solarPanels5','batteryBankMini')
        with tempfile.TemporaryDirectory() as temp:
            temp=Path(temp);game=temp/'game';parts_dir=game/'GameData/Squad/Parts';parts_dir.mkdir(parents=True)
            for name in names:
                resources=''
                if name=='mk2LanderCabin_v2': resources='RESOURCE\n{\n name = MonoPropellant\n amount = 40\n maxAmount = 40\n}\nRESOURCE\n{\n name = ElectricCharge\n amount = 200\n maxAmount = 200\n}\n'
                if name=='Rockomax8BW': resources='RESOURCE\n{\n name = LiquidFuel\n amount = 360\n maxAmount = 360\n}\nRESOURCE\n{\n name = Oxidizer\n amount = 440\n maxAmount = 440\n}\n'
                (parts_dir/(name+'.cfg')).write_text('PART\n{\n name = '+name+'\n mass = 1\n'+resources+'}\n')
            repo=temp/'repo';base=repo/'Ships/VAB';base.mkdir(parents=True)
            shutil.copy(ROOT/'Ships/VAB/Hermes Walkthrough v2.craft',base/'Hermes Walkthrough v2.craft')
            docs=repo/'docs';docs.mkdir();evidence=ROOT/'docs/Finch-Loader-Evidence.json'
            shutil.copy(evidence,docs/evidence.name);evidence_before=evidence.read_bytes()

            # Default output-root semantics: write current outputs, never the evidence file.
            with contextlib.redirect_stdout(io.StringIO()): m.generate(game,repo)
            generated=[base/'Hermes Finch Vacuum Lander.craft',base/'Hermes Finch Docking Cradle.craft',docs/'Finch-Design.json']
            first=[p.read_bytes() for p in generated]
            self.assertEqual((docs/evidence.name).read_bytes(),evidence_before)
            with contextlib.redirect_stdout(io.StringIO()): m.generate(game,repo)
            self.assertEqual([p.read_bytes() for p in generated],first)
            self.assertEqual((docs/evidence.name).read_bytes(),evidence_before)

            design=json.loads((docs/'Finch-Design.json').read_text())
            self.assertFalse(design['runtime_verified'])
            self.assertNotIn('loader_test',design)
            for path in generated[:2]:
                self.assertEqual(design['candidate_craft_sha256'][path.name],hashlib.sha256(path.read_bytes()).hexdigest())
            self.assertEqual(design['historical_loader_evidence']['scope'],'prior candidate only; exact runtime-loaded craft hash was not recorded')

    def test_output_root_uses_repo_walkthrough_without_writing_repo(self):
        names=('mk2LanderCabin_v2','Rockomax8BW','liquidEngine3_v2','dockingPort2','landingLeg1-2','RCSBlock_v2','solarPanels5','batteryBankMini')
        with tempfile.TemporaryDirectory() as temp:
            temp=Path(temp);game=temp/'game';parts_dir=game/'GameData/Squad/Parts';parts_dir.mkdir(parents=True)
            for name in names:
                resources=''
                if name=='mk2LanderCabin_v2': resources='RESOURCE\n{\n name = MonoPropellant\n amount = 40\n maxAmount = 40\n}\nRESOURCE\n{\n name = ElectricCharge\n amount = 200\n maxAmount = 200\n}\n'
                if name=='Rockomax8BW': resources='RESOURCE\n{\n name = LiquidFuel\n amount = 360\n maxAmount = 360\n}\nRESOURCE\n{\n name = Oxidizer\n amount = 440\n maxAmount = 440\n}\n'
                (parts_dir/(name+'.cfg')).write_text('PART\n{\n name = '+name+'\n mass = 1\n'+resources+'}\n')
            repo=temp/'repo';base=repo/'Ships/VAB';base.mkdir(parents=True)
            shutil.copy(ROOT/'Ships/VAB/Hermes Walkthrough v2.craft',base/'Hermes Walkthrough v2.craft')
            original=(base/'Hermes Walkthrough v2.craft').read_bytes();out=temp/'generated'
            with contextlib.redirect_stdout(io.StringIO()): m.generate(game,repo,out)
            self.assertEqual((base/'Hermes Walkthrough v2.craft').read_bytes(),original)
            self.assertTrue((out/'Ships/VAB/Hermes Finch Docking Cradle.craft').is_file())
            self.assertTrue((out/'docs/Finch-Design.json').is_file())
            self.assertFalse((repo/'docs').exists())

            cli_out=temp/'cli-generated'
            subprocess.run([sys.executable,str(ROOT/'tools/build_landers.py'),'--ksp-root',str(game),'--output-root',str(cli_out)],check=True,capture_output=True,text=True)
            self.assertTrue((cli_out/'Ships/VAB/Hermes Finch Docking Cradle.craft').is_file())
            self.assertTrue((cli_out/'docs/Finch-Design.json').is_file())

if __name__=='__main__':unittest.main()
