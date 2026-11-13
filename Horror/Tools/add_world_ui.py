"""Add the WorldSpace UI example to an existing scene, preserving its models.
Blender: --background Authoring/Sandbox.blend --python Tools/add_world_ui.py.
The XML/CSS/font live in Assets/UI and Assets/Fonts. No generated UI meshes are authored.
"""
from pathlib import Path
import json
import math
import sys
import bpy
from mathutils import Euler, Vector

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT.parent/'Engine/Vecxy/tools/Blender'))
import vecxy_usd
if not hasattr(bpy.types.Scene,'vecxy_usd'):vecxy_usd.register()


def add_ui_area(context):
    if not bpy.data.filepath:raise RuntimeError('Open and save the scene first')
    vecxy_usd.schema.reload_catalog(context,required=True)
    if context.scene.vecxy_usd.editing:bpy.ops.vecxy.finish_prototype()
    if context.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
    source=bpy.data.objects.get('Washer_01')
    if source is None:raise RuntimeError('This example requires Washer_01')
    layer=context.scene.collection
    collection=bpy.data.collections.get('05_UI')
    if collection is None:
        collection=bpy.data.collections.new('05_UI');layer.children.link(collection)
    library=bpy.data.collections.get('90_PrototypeLibrary')
    if library and library.name in layer.children:
        previous=context.view_layer.layer_collection.children[library.name]
        excluded,hidden=previous.exclude,previous.hide_viewport
        layer.children.unlink(library);layer.children.link(library)
        restored=context.view_layer.layer_collection.children[library.name]
        restored.exclude=excluded;restored.hide_viewport=hidden
    manifest=json.loads((ROOT/'Assets.manifest').read_text())
    def asset_id(path):
        return next(a['id'] for a in manifest['assets'] if a['path']==path and a['source']=='Game')
    areas=[]
    for number,document,mode in ((1,'WasherStatus','ViewRay'),(2,'WasherConsole','SurfaceCursor')):
        source=bpy.data.objects.get(f'Washer_{number:02}')
        if source is None:raise RuntimeError(f'This example requires Washer_{number:02}')
        name=f'Washer_{number:02}_UI_Area'
        obj=bpy.data.objects.get(name)
        if obj is None:
            bpy.ops.mesh.primitive_plane_add(size=1)
            obj=context.object;obj.name=name
            for owner in list(obj.users_collection):owner.objects.unlink(obj)
            collection.objects.link(obj)
        # Use mesh coordinates, leaving a unit object scale for predictable editing.
        for vertex,coordinate in zip(obj.data.vertices,((-0.59,-0.3,0),(0.59,-0.3,0),(-0.59,0.3,0),(0.59,0.3,0))):
            vertex.co=coordinate
        obj.scale=(1,1,1)
        # A scene area rather than a child prim inside an instanceable USD asset.
        obj.location=source.matrix_world @ Vector((0,-.38,1.38))
        obj.rotation_euler=(source.matrix_world.to_quaternion() @ Euler((math.pi/2,0,0)).to_quaternion()).to_euler()
        obj.display_type='WIRE';obj.show_wire=True;obj.show_all_edges=True
        obj['Purpose']='UI area only. UI Canvas consumes these dimensions; no source plane is drawn in the game.'
        component=vecxy_usd.schema.add_component(obj.vecxy,'Vecxy.UI.UiCanvas')
        values={'document':asset_id(f'UI/{document}.xml'),'renderMode':'WorldSpace','pixelsPerMeter':600,
                'interactive':True,'doubleSided':False,'inputMode':mode,'activationKey':'E',
                'cursorImage':asset_id('UI/Cursors/WorldPointer.png') if number==2 else '', 'cursorSize':44}
        for field in vecxy_usd.schema.CATALOG[component.type_name]['fields']:
            if field['key'] in values:vecxy_usd.schema.set_param(component,field,values[field['key']])
        areas.append(obj)
    obj=areas[0]
    context.scene.vecxy_usd.selected_component='Vecxy.UI.UiCanvas'
    for selected in context.selected_objects:selected.select_set(False)
    obj.select_set(True);context.view_layer.objects.active=obj
    guide=bpy.data.texts.get('World UI — Guide') or bpy.data.texts.new('World UI — Guide')
    guide.clear();guide.write("""WORLD SPACE UI

05_UI содержит только области холстов. В игре их плейны скрыты.
Положение, поворот и размер области задают UI в мире.

Washer_01_UI_Area — ViewRay: наведи центральную точку на кнопку, нажми ЛКМ или E.
Кнопка включает/выключает питание первой машинки.
Washer_02_UI_Area — SurfaceCursor: наведи взгляд на панель.
Камера перестаёт вращаться, мышь перемещает курсор по панели.
За границей панели курсор исчезает, мышь снова вращает камеру.
Для повторного входа отведи взгляд от панели и наведи снова.
ЛКМ/E нажимают кнопку; меняются цвет и счётчик.

N → Vecxy → UI Canvas:
Document — обычный XML из списка ассетов (сохраняется стабильный AssetId).
Render mode — WorldSpace или ScreenOverlay.
Pointer mode — Mouse (свободная экранная мышь), ViewRay или SurfaceCursor.
Activation key — E по умолчанию; Undefined отключает клавишу.
Cursor image — PNG из ассетов. Можно указать свою иконку; hotspot в левом верхнем углу.
Cursor size — размер иконки в координатах UI.
Pixels per metre — плотность раскладки, физический размер задаёт область.
Interactive — включает события. Double sided — отображение с обратной стороны.

Assets/UI/Crosshair.xml — обычный экранный оверлей с центральной точкой.
Assets/UI/WasherStatus.xml и WasherConsole.xml — документы панелей.
Assets/Fonts/NotoSans.ttf — шрифт, лицензия рядом.
WorldUiDemo.cs — привязка событий кнопок и проверка препятствий через физический raycast.
Все панели используют Vecxy.UI: XML/CSS, раскладку, скругления, шрифты и hot reload.

После изменения области: Ctrl+S → Export scene USD → перезапустить игру.
После изменения компонентов C#: vecxy -p Horror generate.
""")
    return obj


if __name__=='__main__':
    add_ui_area(bpy.context)
    bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
    assert bpy.ops.vecxy.export_scene()=={'FINISHED'}
    print('WORLD_UI_AREA_EXPORTED')
