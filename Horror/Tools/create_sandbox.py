"""One-time starter scene creation. Edits belong in Authoring/Sandbox.blend afterwards.
Run explicitly with Blender --background --factory-startup --python this_file.py.
"""
import math
from pathlib import Path
import sys
import bpy
from mathutils import Quaternion,Vector

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT.parent/'Engine/Vecxy/tools/Blender'))
import vecxy_usd
if not hasattr(bpy.types.Scene,'vecxy_usd'):vecxy_usd.register()
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
for col in list(bpy.data.collections):bpy.data.collections.remove(col)
scene=bpy.context.scene;scene.name='Sandbox'
scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1

def collection(name,parent=None):
 col=bpy.data.collections.new(name);(parent or scene.collection).children.link(col);return col
floor_col=collection('01_Environment')
instances_col=collection('02_Instances')
lights_col=collection('03_Lighting')
markers_col=collection('04_Markers')
library=collection('90_PrototypeLibrary')
source=collection('Washer_Prototype',library)
active_col=source

def put(obj,name,parent=None,material=None):
 obj.name=name
 for col in list(obj.users_collection):col.objects.unlink(obj)
 active_col.objects.link(obj)
 if parent:obj.parent=parent
 if material:obj.data.materials.append(material)
 return obj

def empty(name,parent=None):
 obj=bpy.data.objects.new(name,None);active_col.objects.link(obj);obj.parent=parent;return obj

def material(name,color,metallic=0,roughness=.4,alpha=1,emission=0):
 mat=bpy.data.materials.new(name);mat.use_nodes=True
 p=mat.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,alpha)
 p.inputs['Metallic'].default_value=metallic;p.inputs['Roughness'].default_value=roughness
 p.inputs['Alpha'].default_value=alpha;mat.diffuse_color=(*color,alpha)
 if emission:p.inputs['Emission Color'].default_value=(*color,1);p.inputs['Emission Strength'].default_value=emission
 if alpha<1:
  mat.surface_render_method='BLENDED'
  p.inputs['Alpha'].default_value=1
  p.inputs['Transmission Weight'].default_value=1-alpha
 return mat
white=material('Porcelain enamel',(.78,.81,.84),.12,.26)
trim=material('Satin aluminum',(.38,.43,.49),.8,.27)
rubber=material('Graphite rubber',(.015,.02,.027),0,.7)
steel=material('Drum brushed steel',(.3,.35,.4),.75,.32)
panel=material('Control panel',(.065,.085,.11),.2,.32)
display=material('Display background',(.005,.017,.022),0,.3)
led=material('Display digits',(.17,.7,.8),0,.35,emission=.8)
glass=material('Door glass',(.12,.19,.23),.15,.12,.22)
floor_mat=material('Floor neutral grey',(.31,.35,.39),.05,.57)

def finish(obj,bevel=0):
 bpy.context.view_layer.objects.active=obj
 if bevel:
  mod=obj.modifiers.new('Soft manufactured edges','BEVEL');mod.width=bevel;mod.segments=3
  bpy.ops.object.modifier_apply(modifier=mod.name)
 for face in obj.data.polygons:face.use_smooth=True
 mod=obj.modifiers.new('Weighted surface normals','WEIGHTED_NORMAL');mod.keep_sharp=True;mod.weight=50
 bpy.ops.object.modifier_apply(modifier=mod.name)
 return obj

def box(name,position,size,mat,parent,bevel=.01):
 bpy.ops.mesh.primitive_cube_add(size=1,location=position)
 obj=put(bpy.context.object,name,parent,mat);obj.dimensions=size
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 return finish(obj,bevel)

def cylinder(name,position,radius,depth,mat,parent,axis='Y',vertices=64):
 rotation=(math.pi/2,0,0) if axis=='Y' else (0,0,0)
 bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=depth,location=position,rotation=rotation)
 return finish(put(bpy.context.object,name,parent,mat),min(.002,depth*.15))

def ring(name,position,radius,thickness,mat,parent):
 bpy.ops.mesh.primitive_torus_add(major_segments=64,minor_segments=12,location=position,rotation=(math.pi/2,0,0),major_radius=radius,minor_radius=thickness)
 obj=put(bpy.context.object,name,parent,mat)
 for face in obj.data.polygons:face.use_smooth=True
 return obj

root=empty('Washer_Model')
cabinet=empty('01_Cabinet',root);door=empty('02_Door_and_drum',root)
controls=empty('03_Controls',root);feet=empty('04_Feet',root)
body=box('Enamel cabinet',(0,0,.45),(.6,.6,.81),white,cabinet,.022)
# Real opening into the housing, with a recessed drum behind the glass.
bpy.ops.mesh.primitive_cylinder_add(vertices=64,radius=.205,depth=.3,location=(0,-.285,.43),rotation=(math.pi/2,0,0))
cutter=bpy.context.object;bpy.context.view_layer.objects.active=body
mod=body.modifiers.new('Door recess','BOOLEAN');mod.operation='DIFFERENCE';mod.object=cutter
bpy.ops.object.modifier_apply(modifier=mod.name);bpy.data.objects.remove(cutter,do_unlink=True)
box('Top cover',(0,0,.852),(.615,.625,.035),white,cabinet,.013)
box('Lower service strip',(0,-.304,.105),(.548,.012,.055),white,cabinet,.006)
ring('Outer aluminum door rim',(0,-.332,.43),.218,.018,trim,door)
ring('Inner door gasket',(0,-.327,.43),.191,.012,rubber,door)
cylinder('Dark recessed tub',(0,-.173,.43),.202,.024,rubber,door)
cylinder('Drum back plate',(0,-.19,.43),.177,.012,steel,door)
ring('Drum rolled lip',(0,-.26,.43),.179,.007,steel,door)
for i in range(3):
 angle=math.tau*i/3
 fin=box('Drum lifter '+str(i+1),(math.sin(angle)*.122,-.235,.43+math.cos(angle)*.122),(.028,.075,.06),steel,door,.009)
 fin.rotation_euler.y=angle
# A single perforation mesh keeps the hierarchy small and the shared mesh efficient.
verts=[];faces=[]
for radius,count in ((.055,10),(.105,18),(.15,26)):
 for i in range(count):
  angle=math.tau*i/count;cx=math.cos(angle)*radius;cz=.43+math.sin(angle)*radius
  start=len(verts)
  for j in range(10):
   a=math.tau*j/10;verts.append((cx+math.cos(a)*.0035,-.198,cz+math.sin(a)*.0035))
  faces.append(tuple(range(start,start+10)))
mesh=bpy.data.meshes.new('Drum perforations');mesh.from_pydata(verts,[],faces);mesh.materials.append(rubber)
obj=bpy.data.objects.new('Drum perforations',mesh);source.objects.link(obj);obj.parent=door
cylinder('Door glass disc',(0,-.342,.43),.184,.006,glass,door)
box('Door grip',(.193,-.367,.43),(.035,.031,.11),panel,door,.013)
box('Upper controls fascia',(0,-.307,.752),(.55,.018,.137),panel,controls,.012)
box('Detergent drawer',(-.175,-.322,.752),(.16,.018,.083),white,controls,.007)
box('Drawer finger groove',(-.175,-.335,.732),(.095,.008,.006),panel,controls,.002)
cylinder('Program dial',(-.015,-.335,.759),.038,.028,trim,controls)
box('Dial marker',(-.015,-.353,.785),(.004,.003,.012),rubber,controls,.001)
box('Display',(.16,-.322,.766),(.147,.011,.05),display,controls,.004)
# Text is converted to a real mesh, so it exports with no font dependency.
bpy.ops.object.text_add(location=(.104,-.331,.751),rotation=(math.pi/2,0,0))
text=put(bpy.context.object,'Display 1:20',controls,led);text.data.body='1:20';text.data.size=.033;text.data.extrude=.0001
bpy.ops.object.convert(target='MESH')
cylinder('Start button',(.208,-.327,.718),.012,.012,trim,controls,vertices=32)
for i,(x,y) in enumerate(((-.235,-.24),(.235,-.24),(-.235,.24),(.235,.24)),1):
 cylinder('Leveling foot '+str(i),(x,y,.025),.027,.04,rubber,feet,axis='Z',vertices=24)

schema=vecxy_usd.schema
settings=scene.vecxy_usd;settings.catalog_path='//vecxy.components.json'
# Save once so blend-relative catalog paths resolve while authoring components.
ROOT.joinpath('Authoring').mkdir(exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Authoring/Sandbox.blend'))
schema.reload_catalog(bpy.context,required=True)
source.vecxy.is_prototype=True;source.vecxy.asset_name='Washer'
source.vecxy.export_path='//../Assets/Scenes/Prototypes/Washer.usda';source.vecxy.role='washer'
collider=schema.add_component(source.vecxy,'Vecxy.Physics.BoxCollider')
for field in schema.CATALOG[collider.type_name]['fields']:
 if field['key']=='center':schema.set_param(collider,field,(0,-.015,.44))
 if field['key']=='size':schema.set_param(collider,field,(.62,.7,.88))
schema.add_component(source.vecxy,'Horror.LaundryMachine')
for number,x in ((1,-.8),(2,.8)):
 obj=bpy.data.objects.new('Washer_0'+str(number),None);instances_col.objects.link(obj)
 obj.instance_type='COLLECTION';obj.instance_collection=source;obj.location=(x,1,0)
 component=schema.add_component(obj.vecxy,'Horror.LaundryMachine',override=True)
 field=next(f for f in schema.CATALOG[component.type_name]['fields'] if f['key']=='number')
 schema.set_param(component,field,number)
active_col=floor_col
bpy.ops.mesh.primitive_plane_add(size=16)
floor=put(bpy.context.object,'Floor',material=floor_mat)
component=schema.add_component(floor.vecxy,'Vecxy.Physics.BoxCollider')
for field in schema.CATALOG[component.type_name]['fields']:
 if field['key']=='center':schema.set_param(component,field,(0,0,-.12))
 if field['key']=='size':schema.set_param(component,field,(16,16,.24))
active_col=markers_col
spawn=empty('PlayerSpawn');spawn.location=(0,-1.8,.025);spawn.empty_display_type='ARROWS';spawn.empty_display_size=.4;spawn.vecxy.role='spawn'
active_col=lights_col
sun_data=bpy.data.lights.new('Overcast daylight','SUN');sun_data.energy=1.3;sun_data.color=(.83,.9,1)
sun=bpy.data.objects.new('KeyLight',sun_data);lights_col.objects.link(sun);sun.rotation_euler=(math.radians(27),math.radians(-20),math.radians(-25))
fill_data=bpy.data.lights.new('Soft front fill','POINT');fill_data.energy=18;fill_data.color=(1,.94,.86)
fill=bpy.data.objects.new('FillLight',fill_data);lights_col.objects.link(fill);fill.location=(-2,-2,3.2);fill['vecxy:range']=12
# Library stays in the Outliner but is excluded from placed geometry.
scene.view_layers[0].layer_collection.children[library.name].exclude=True
settings.export_path='//../Assets/Scenes/Sandbox.usda';settings.cook_after_export=True
settings.selected_component='Horror.LaundryMachine'
text=bpy.data.texts.new('START HERE — Scene guide')
text.write('''SANDBOX — пол, две машинки, дождь\n\n01_Environment / Floor — видимый плейн и Box Collider на нём.\n02_Instances / Washer_01, Washer_02 — две связанные копии одной модели.\n03_Lighting — свет сцены.\n04_Markers / PlayerSpawn — ноги игрока; локальная +Y смотрит вперёд.\n90_PrototypeLibrary / Washer_Prototype — одна общая модель, не расстановка.\n\nN → Vecxy:\nCommon settings (all copies) — общие компоненты и экспортируемый USD.\nEdit original prototype → Edit Mode → Return to scene.\nИсходный прототип изолируется как через /. Новых объектов не создаётся.\nГеометрия обновляется у обеих копий. Масштаб экземпляров равен 1.\nНомер машинки переопределён по одному полю: 1 и 2; остальные поля наследуются.\nКоллайдер — обычный компонент Physics / Box Collider, без helper-объекта.\n\nКомпоненты из C#: один атрибут Authoring на типе и свойствах Prototype.Options.\nvecxy generate / dotnet build → список обновится автоматически.\nCtrl+S → Export scene USD → перезапустить игру.\nExport this prototype USD меняет только Washer.usda.\nДождь — отдельный игровой эффект RainWeather.cs, не часть прототипа машинки.\n\nПодробности: Authoring/README.md.\n''')
# A useful initial view and a tidy Outliner are part of the starter file.
for obj in bpy.context.selected_objects:obj.select_set(False)
selected=bpy.data.objects['Washer_01'];selected.select_set(True);bpy.context.view_layer.objects.active=selected
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':
   area.spaces.active.show_region_ui=True
   area.spaces.active.region_3d.view_distance=5
   area.spaces.active.region_3d.view_location=(0,.7,.45)
   area.spaces.active.region_3d.view_rotation=Quaternion((.87,.47,.07,.13)).normalized()
   area.spaces.active.shading.type='MATERIAL'
scene.world.color=(.25,.25,.25)
import runpy
runpy.run_path(str(ROOT/'Tools/add_world_ui.py'),run_name='vecxy_authoring_helper')['add_ui_area'](bpy.context)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Authoring/Sandbox.blend'))
assert bpy.ops.vecxy.export_scene()=={'FINISHED'}
print('CLEAN_SANDBOX_CREATED')
