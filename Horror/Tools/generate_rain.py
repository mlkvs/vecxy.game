"""Build only the reusable rain/splash mesh. Coordinates are engine Y-up metres.
No building, road, roof masks or dependencies on the editable Blender scene.
Run with Python 3; no third-party packages required.
"""
import json
import math
from pathlib import Path
import random
import struct

output=Path(__file__).resolve().parents[1]/'Assets/Models/Weather.glb'
buffer=bytearray();views=[];accessors=[];meshes=[];nodes=[]

def accessor(data,width,kind,integer=False):
 while len(buffer)%4:buffer.append(0)
 start=len(buffer);flat=[value for item in data for value in item]
 buffer.extend(struct.pack('<'+('I' if integer else 'f')*len(flat),*flat))
 views.append({'buffer':0,'byteOffset':start,'byteLength':len(buffer)-start})
 record={'bufferView':len(views)-1,'componentType':5125 if integer else 5126,'count':len(data),'type':kind}
 if kind=='VEC3' and not integer:
  record['min']=[min(p[i] for p in data) for i in range(width)]
  record['max']=[max(p[i] for p in data) for i in range(width)]
 accessors.append(record);return len(accessors)-1

rng=random.Random(206)
for splash,count in ((False,3500),(True,750)):
 positions=[];normals=[];uvs=[];indices=[]
 def quad(points,phase):
  start=len(positions);positions.extend(points)
  normals.extend([(phase,math.sqrt(1-phase*phase),0)]*4)
  uvs.extend([(0,0),(1,0),(1,1),(0,1)])
  indices.extend([(start,),(start+1,),(start+2,),(start,),(start+2,),(start+3,)])
 for _ in range(count):
  x=rng.uniform(-7.9,7.9);z=rng.uniform(-7.9,7.9);phase=rng.random()
  if splash:
   # Small landing rings on the flat floor.
   w=.16;y=.008
   quad([(x-w,y,z-w),(x+w,y,z-w),(x+w,y,z+w),(x-w,y,z+w)],phase)
  else:
   y=phase*8;w=.008;h=.11
   quad([(x-w,y-h,z),(x+w,y-h,z),(x+w,y+h,z),(x-w,y+h,z)],phase)
   quad([(x,y-h,z-w),(x,y-h,z+w),(x,y+h,z+w),(x,y+h,z-w)],phase)
 primitive={'attributes':{'POSITION':accessor(positions,3,'VEC3'),'NORMAL':accessor(normals,3,'VEC3'),'TEXCOORD_0':accessor(uvs,2,'VEC2')},'indices':accessor(indices,1,'SCALAR',True),'material':0}
 meshes.append({'name':'Splashes' if splash else 'Rain','primitives':[primitive]})
 nodes.append({'name':'Splashes' if splash else 'Rain','mesh':len(meshes)-1})
doc={'asset':{'version':'2.0','generator':'Vecxy clean sandbox rain'},'scene':0,'scenes':[{'nodes':[0,1]}],'nodes':nodes,'meshes':meshes,'materials':[{'name':'Rain placeholder','doubleSided':True,'pbrMetallicRoughness':{'baseColorFactor':[.6,.7,.8,1]}}],'buffers':[{'byteLength':len(buffer)}],'bufferViews':views,'accessors':accessors}
encoded=json.dumps(doc,separators=(',',':')).encode()
while len(encoded)%4:encoded+=b' '
while len(buffer)%4:buffer.append(0)
payload=struct.pack('<I4s',len(encoded),b'JSON')+encoded+struct.pack('<I4s',len(buffer),b'BIN\x00')+buffer
output.write_bytes(struct.pack('<4sII',b'glTF',2,12+len(payload))+payload)
print('Rain and ground splashes:',output)
