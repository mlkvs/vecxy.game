"""Asset integrity checks, independent of a graphics/audio device."""
from pathlib import Path
import struct,json,wave,re,xml.etree.ElementTree as ET
import yaml
R=Path(__file__).resolve().parents[1];A=R/'Assets';c=yaml.safe_load((A/'Configs/Story.yaml').read_text());v=yaml.safe_load((A/'Configs/Voices.yaml').read_text());checks=0
def check(ok,msg):
 global checks
 checks+=1
 if not ok:raise AssertionError(msg)
for key,line in v['voices'].items():
 check(bool(re.search('[А-Яа-яЁё]',line['text'])),f'Russian voice {key}')
 with wave.open(str(A/line['audio'])) as wav:
  check(wav.getnchannels()==1 and wav.getsampwidth()==2,f'PCM voice {key}')
  check(0<=line['duration']-wav.getnframes()/wav.getframerate()<.3,f'Subtitle timing {key}')
for x in A.glob('Audio/*.wav'):
 with wave.open(str(x)) as wav:check(wav.getnframes()>100 and wav.getsampwidth()==2,f'Valid sound {x.name}')
for x in A.glob('UI/*.xml'):
 tree=ET.parse(x);root=tree.getroot();check((x.parent/root.attrib['styles']).exists(),f'Style {x.name}');ids=[e.attrib['id'] for e in root.iter() if 'id' in e.attrib];check(len(ids)==len(set(ids)),f'Unique UI IDs {x.name}')
 check(not any(re.search(r'\b(START|SETTINGS|EXIT|SEALED|RESIDENT|PLAY|RELEASE)\b',e.text or '') for e in root.iter()),f'No English UI {x.name}')
scene=json.loads((A/'Scenes/Cell.usda.usdscene').read_text());data=(A/scene['model']).read_bytes();length,kind=struct.unpack_from('<II',data,12);g=json.loads(data[20:20+length]);nodes={n.get('name') for n in g['nodes']}
for material in g.get('materials',[]):
 pbr=material.get('pbrMetallicRoughness',{})
 if 'baseColorTexture' in pbr:check(pbr.get('baseColorFactor')==[1,1,1,1],f'Untinted USD texture {material["name"]}')
 if 'baseColorTexture' in pbr:
  sampler=g['samplers'][g['textures'][pbr['baseColorTexture']['index']]['sampler']]
  check(sampler['magFilter']==9728 and sampler['minFilter']==9728,f'Closest sampling {material["name"]}')
roles={}
for prim in scene['prims']:
 if prim.get('role'):roles.setdefault(prim['role'],[]).append(prim)
for role in ['spawn','room-settings','menu-view','bed-view','bed-seat','chair-seat','hatch','hatch-open','meal','meal-on-table','document','water','main-light','screen-light','light-diffuser','archive-ui','indicator-ui']:check(len(roles.get(role,[]))==1,f'Unique USD role {role}')
components=[c for prim in scene['prims'] for c in prim.get('components',[]) if c.get('enabled',True)]
actions=[x for x in components if x['type']=='DoNotRelease.CellInteractable']
check({x['data']['action'] for x in actions}==set(['Door','Bed','Chair','Sink','Vent','Camera','Toilet','Table','Tray','Document','Screen']),'All interactions authored in Blender')
for item in actions:check(bool(re.search('[А-Яа-яЁё]',item['data']['prompt'])),f'Russian authored prompt {item["data"]["action"]}')
check(sum(x['type']=='Vecxy.Physics.BoxCollider' for x in components)==13,'13 Blender-authored static colliders')
check(sum(x['type']=='Vecxy.UI.UiCanvas' for x in components)==2,'Both world UI canvases authored')
check(sum(x['type']=='DoNotRelease.CellInitialState' for x in components)==3,'Initial prop states authored')
check(sum(x['type']=='DoNotRelease.CellRoomSettings' for x in components)==1,'Room parameters authored')
check(sum(x['type']=='DoNotRelease.CellLightRange' for x in components)==2,'Light ranges authored')
check(sum(bool(x.get('light')) for x in scene['prims'])==2,'Native UsdLux lights')
check('interactions' not in c and 'colliders' not in c,'Story config has no duplicated scene layout')
check(not (A/'Models/Cell.glb').exists(),'No legacy direct GLB source')
check((A/'Scenes/Cell.usda').is_file() and (R/'Authoring/Cell.blend').is_file(),'Editable Blender and USD sources')
check((R/'Authoring/vecxy.components.json').is_file(),'Addon component catalog')
check(all('uri' not in x for x in g.get('images',[])),'Textures embedded in GLB')
check((A/'Fonts/NotoSans.ttf').stat().st_size>100000,'Cyrillic font bundled')
check(len(v['recording'])==18,'Complete 18-line recording')
seconds=sum(d['max'] for d in c['stages'].values());check(300<seconds<600,'Full passive story under ten minutes')
check(c['releaseSeconds']==364*86400+23*3600+59*60,'Countdown')
triangles=sum(g['accessors'][p['indices']]['count']//3 for m in g['meshes'] for p in m['primitives'] if 'indices' in p)
print(f'PASS {checks} asset checks; {len(nodes)} GLB nodes; {triangles} triangles; {len(v["voices"])} Russian voice clips; passive schedule {seconds:.1f}s')
