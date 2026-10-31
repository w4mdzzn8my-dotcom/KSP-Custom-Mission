"""Generate a stock vacuum lander and a Hermes external docking cradle candidate."""
import argparse
import copy
import hashlib
import json
import math
from pathlib import Path

class Node:
    def __init__(self, name): self.name, self.values, self.children = name, [], []
    def get(self, key, default=None): return next((v for k,v in self.values if k==key),default)
    def set(self,key,value): self.values=[(k,v) for k,v in self.values if k!=key];self.values.append((key,str(value)))
    def add(self,key,value): self.values.append((key,str(value)))
    def nodes(self,name): return [n for n in self.children if n.name==name]
    def text(self,depth=0):
        pad='\t'*depth
        inner=''.join(pad+'\t'+k+' = '+v+'\n' for k,v in self.values)+''.join(n.text(depth+1) for n in self.children)
        return pad+self.name+'\n'+pad+'{\n'+inner+pad+'}\n'

def parse(text):
    root=Node('ROOT'); stack=[root];pending=None
    for line in text.lstrip('\ufeff').splitlines():
        line=line.split('//',1)[0].strip()
        if not line:continue
        if line=='{':
            n=Node(pending);stack[-1].children.append(n);stack.append(n);pending=None
        elif line=='}':stack.pop()
        elif '=' in line:stack[-1].add(*[s.strip() for s in line.split('=',1)])
        else:pending=line
    if len(stack)!=1:raise ValueError('Unbalanced config')
    return root

def output(root):return ''.join(k+' = '+v+'\n' for k,v in root.values)+''.join(n.text() for n in root.children)
def vector(v):return tuple(map(float,v.split(',')))
def fmt(v):return ','.join(format(x,'.9g') for x in v)

def parent_first(root):
    """KSP ProtoPartSnapshot loads parents before children; preserve a stable order."""
    parts=root.nodes('PART'); parents={}
    for part in parts:
        for key,value in part.values:
            if key=='link':
                if value in parents:raise ValueError('Multiple parents: '+value)
                parents[value]=part.get('part')
    pending=list(parts);ordered=[];placed=set()
    while pending:
        ready=[n for n in pending if n.get('part') not in parents or parents[n.get('part')] in placed]
        if not ready:raise ValueError('Cyclic or missing-parent craft')
        for node in ready:ordered.append(node);placed.add(node.get('part'));pending.remove(node)
    root.children=ordered+[n for n in root.children if n.name!='PART']


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def generate(game,repo,output_root=None):
    output_root=Path(output_root) if output_root is not None else repo
    catalog={}
    for f in (game/'GameData/Squad/Parts').rglob('*.cfg'):
        text=f.read_text(errors='replace')
        needed=('mk2LanderCabin_v2','Rockomax8BW','liquidEngine3_v2','dockingPort2','landingLeg1-2','RCSBlock_v2','solarPanels5','batteryBankMini')
        if not any(('name = '+name+'\n') in text or ('name = '+name+'\r') in text for name in needed): continue
        for n in parse(text).nodes('PART'):
            if n.get('name'):catalog[n.get('name')]=n
    base=970000000
    parts=[]
    def part(name,pos,rot=(0,0,0,1),parent=None,parentnode=None,childnode=None,surface=False):
        prefab=catalog[name];i=len(parts);key=name.replace('_','.')+'_'+str(base+i);n=Node('PART')
        for k,v in [('part',key),('partName','Part'),('persistentId',base+i+1000),('pos',fmt(pos)),('attPos','0,0,0'),('attPos0',fmt(pos)),('rot',fmt(rot)),('attRot','0,0,0,1'),('attRot0',fmt(rot)),('mir','1,1,1'),('symMethod','Radial'),('autostrutMode','Off'),('rigidAttachment','False'),('istg',0 if name=='liquidEngine3_v2' else -1),('dstg',0),('sidx',0),('sqor',-1),('sepI',-1),('attm',1 if surface else 0)]:n.add(k,v)
        # Prefab modules supply default fields; serialize resources explicitly.
        n.children=copy.deepcopy(prefab.nodes('RESOURCE'))
        if name=='mk2LanderCabin_v2':
            variant=Node('MODULE');variant.add('name','ModulePartVariants');variant.add('selectedVariant','Lander');n.children.append(variant)
        if parent is not None:
            p=parts[parent];p.add('link',key)
            if surface:n.add('srfN','srfAttach,'+p.get('part'))
            else:
                p.add('attN',parentnode+','+key+'_0|0|0')
                n.add('attN',childnode+','+p.get('part')+'_0|0|0')
        parts.append(n);return i
    # Local +Y is up; dimensions come from stock attachment nodes.
    cabin=part('mk2LanderCabin_v2',(0,4,0))
    tank_y=4-.403574-.455
    tank=part('Rockomax8BW',(0,tank_y,0),parent=cabin,parentnode='bottom',childnode='top')
    engine=part('liquidEngine3_v2',(0,tank_y-.455,0),parent=tank,parentnode='bottom',childnode='top')
    dock=part('dockingPort2',(0,4+.751929,0),parent=cabin,parentnode='top',childnode='bottom')
    for angle in (0,90,180,270):
        r=math.radians(angle);x,z=math.sin(r),math.cos(r)
        part('landingLeg1-2',(1.25*x,tank_y-.15,1.25*z),(0,math.sin(r/2),0,math.cos(r/2)),tank,surface=True)
        part('RCSBlock_v2',(1.25*x,4.1,1.25*z),(0,math.sin(r/2),0,math.cos(r/2)),cabin,surface=True)
        part('solarPanels5',(1.27*x,4.35,1.27*z),(0,math.sin(r/2),0,math.cos(r/2)),cabin,surface=True)
    part('batteryBankMini',(0,4,-1.38),parent=cabin,surface=True)
    root=Node('ROOT')
    for k,v in [('ship','Hermes Finch Vacuum Lander'),('version','1.12.5'),('description','Two-crew vacuum lander candidate for Mun/Minmus low-orbit sorties. No atmosphere, launch booster or Kerbin recovery. Flight qualification pending.'),('type','VAB'),('size','5,6,5'),('persistentId',base),('rot','0,0,0,1'),('missionFlag','Squad/Flags/default'),('vesselType','Lander')]:root.add(k,v)
    root.children=parts
    out=output_root/'Ships/VAB';out.mkdir(parents=True,exist_ok=True)
    lander_path=out/'Hermes Finch Vacuum Lander.craft'
    lander_path.write_text(output(root))
    # External cradle leaves the existing walking route intact.
    carrier=parse((repo/'Ships/VAB'/'Hermes Walkthrough v2.craft').read_text())
    carrier.set('ship','Hermes Finch Docking Cradle')
    carrier.set('description','External single-lander docking cradle prototype; no enclosing bay. Keep centrifuge stopped. Clearance and docking qualification pending.')
    mother=next(n for n in carrier.nodes('PART') if n.get('part').startswith('sspx-tube-375-1_'))
    port=Node('PART');port_key='dockingPort2_979000000'
    for k,v in [('part',port_key),('partName','Part'),('persistentId',979001000),('pos','1.9,17.2,0'),('attPos','0,0,0'),('attPos0','1.9,17.2,0'),('rot','0,0,-0.707106781,0.707106781'),('attRot','0,0,0,1'),('attRot0','0,0,-0.707106781,0.707106781'),('mir','1,1,1'),('symMethod','Radial'),('istg',-1),('dstg',-1),('sidx',-1),('sqor',-1),('sepI',-1),('attm',1),('srfN','srfAttach,'+mother.get('part'))]:port.add(k,v)
    mother.add('link',port_key);carrier.children.append(port)
    # Rotate lander +90 degrees around Z: its docking face points toward carrier.
    stored=copy.deepcopy(parts);offset=1.9+.2828832+.751929+.2828832
    for n in stored:
        x,y,z=vector(n.get('pos')); n.set('pos',fmt((offset-(y-4),17.2+x,z)));n.set('attPos0',n.get('pos'))
        qx,qy,qz,qw=vector(n.get('rot'));s=math.sqrt(.5)
        q=(s*qx-s*qy,s*qx+s*qy,s*qz+s*qw,s*qw-s*qz)
        n.set('rot',fmt(q));n.set('attRot0',fmt(q))
    # Tree root becomes carrier -> carrier port -> lander port -> cabin.
    stored_cabin,stored_dock=stored[0],stored[dock]
    dock_key=stored_dock.get('part');cabin_key=stored_cabin.get('part')
    stored_cabin.values=[(k,v) for k,v in stored_cabin.values if not(k=='link' and v==dock_key)]
    stored_dock.add('link',cabin_key);stored_dock.add('attN','top,'+port_key+'_0|0|0')
    port.add('link',dock_key);port.add('attN','top,'+dock_key+'_0|0|0')
    carrier.children.extend(stored)
    parent_first(carrier)
    (out/'Hermes Finch Docking Cradle.craft').write_text(output(carrier))
    # Conservative ideal budget includes two crew at 0.09375 t each and full monoprop.
    dry=sum(float(catalog[n.get('part').rsplit('_',1)[0].replace('.','_')].get('mass','0')) for n in parts)+2*.09375
    prop=4.0;mono=.16;wet=dry+prop+mono
    dv=345*9.80665*math.log(wet/(dry+mono))
    cradle_path=out/'Hermes Finch Docking Cradle.craft'
    report={'parts':len(parts),'crew_capacity':2,'dry_with_crew_t':dry,'wet_with_crew_t':wet,'lf_ox_propellant_t':prop,'vacuum_isp_s':345,'ideal_delta_v_m_s':dv,'mun_surface_twr':60/(wet*1.63),'minmus_surface_twr':60/(wet*.491),'scope':'Mun/Minmus low-orbit vacuum sortie candidate; no atmospheric capability or flight qualification','runtime_verified':False,'candidate_craft_sha256':{'Hermes Finch Vacuum Lander.craft':sha256(lander_path),'Hermes Finch Docking Cradle.craft':sha256(cradle_path)},'historical_loader_evidence':{'path':'docs/Finch-Loader-Evidence.json','reference_base':'repository root (the evidence file is not copied to --output-root)','scope':'prior candidate only; exact runtime-loaded craft hash was not recorded'}}
    design_path=output_root/'docs/Finch-Design.json';design_path.parent.mkdir(parents=True,exist_ok=True)
    design_path.write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps(report,indent=2))

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--ksp-root',required=True,type=Path);p.add_argument('--output-root',type=Path,help='write generated craft and design report here (base Hermes Walkthrough craft remains read from the repository)');a=p.parse_args();generate(a.ksp_root,Path(__file__).resolve().parents[1],a.output_root)
