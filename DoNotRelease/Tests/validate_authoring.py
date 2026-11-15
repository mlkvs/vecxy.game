"""Blender -> addon -> USD -> cooked cache roundtrip. The source .blend is never saved.
blender -b DoNotRelease/Authoring/Cell.blend --python DoNotRelease/Tests/validate_authoring.py
"""
from pathlib import Path
import bpy,sys,json,subprocess,tempfile,hashlib
from mathutils import Vector
R=Path(__file__).resolve().parents[1];repo=R.parent
sys.path.insert(0,str(repo/'Engine/Vecxy/tools/Blender'))
import vecxy_usd
if not hasattr(bpy.types.Scene,'vecxy_usd'):vecxy_usd.register()
vecxy_usd.schema.reload_catalog(bpy.context,required=True)
source=Path(bpy.data.filepath);before_hash=hashlib.sha256(source.read_bytes()).hexdigest()
bed=next(o for o in bpy.context.scene.objects if any(c.type_name=='DoNotRelease.CellInteractable' and vecxy_usd.schema.values(c)['action']=='Bed' for c in o.vecxy.components))
markers=[next(o for o in bpy.context.scene.objects if o.vecxy.role==role) for role in ['bed-view','bed-seat']]
assert all(o.parent==bed for o in markers),'Bed poses must be children of the furniture'
positions=[o.matrix_world.translation.copy() for o in markers];delta=Vector((.27,.11,0));bed.location+=delta;bpy.context.view_layer.update()
assert all((o.matrix_world.translation-p-delta).length<1e-5 for o,p in zip(markers,positions))
work=repo/'.agents/local/donotrelease/usd-migration';work.mkdir(parents=True,exist_ok=True)
with tempfile.TemporaryDirectory(prefix='roundtrip-',dir=work) as temp:
 output=Path(temp)/'Cell.usda';bpy.context.scene.vecxy_usd.export_path=str(output);bpy.context.scene.vecxy_usd.cook_after_export=False
 assert bpy.ops.vecxy.export_scene()=={'FINISHED'}
 python=vecxy_usd.exporter.python_executable(bpy.context)
 subprocess.run([python,str(repo/'Engine/Vecxy/Code/Vecxy.Usd/Tools/cook_usd.py'),str(output),'--assets',temp],check=True)
 # Use OpenUSD to verify the exported prim hierarchy and actual world-space edit.
 script=r'''
from pxr import Usd,UsdGeom
import sys,json
base=Usd.Stage.Open(sys.argv[1]);edit=Usd.Stage.Open(sys.argv[2])
def role(s,name):return next(p for p in s.Traverse() if p.GetAttribute('vecxy:role').Get()==name)
def position(s,name):return UsdGeom.XformCache().GetLocalToWorldTransform(role(s,name)).ExtractTranslation()
for name in ['bed-view','bed-seat']:
 d=position(edit,name)-position(base,name);assert all(abs(d[i]-[.27,.11,0][i])<1e-5 for i in range(3)),(name,d)
assert (position(edit,'chair-seat')-position(base,'chair-seat')).GetLength()<1e-6
def bed(s):
 return next(p for p in s.Traverse() if any(c['type']=='DoNotRelease.CellInteractable' and c['data']['action']=='Bed' for c in json.loads(p.GetAttribute('vecxy:components').Get() or '[]')))
a,b=bed(base),bed(edit)
for name in ['bed-seat','bed-view']:assert role(edit,name).GetParent()==b
ca=json.loads(a.GetAttribute('vecxy:components').Get());cb=json.loads(b.GetAttribute('vecxy:components').Get());assert ca==cb,'Components and local collider shape must survive a furniture edit'
cache=UsdGeom.BBoxCache(Usd.TimeCode.Default(),[UsdGeom.Tokens.default_])
ra=cache.ComputeWorldBound(a).ComputeAlignedRange();rb=cache.ComputeWorldBound(b).ComputeAlignedRange()
for getter in ['GetMin','GetMax']:
 d=getattr(rb,getter)()-getattr(ra,getter)();assert all(abs(d[i]-[.27,.11,0][i])<1e-5 for i in range(3)),d
print('PASS: moving the Blender bed moves its USD geometry, collider and both pose markers; chair unaffected')
'''
 subprocess.run([python,'-c',script,str(R/'Assets/Scenes/Cell.usda'),str(output)],check=True)
assert hashlib.sha256(source.read_bytes()).hexdigest()==before_hash,'Source blend changed during test'
print('PASS: editable Blender source preserved; addon export and cooking succeeded')
