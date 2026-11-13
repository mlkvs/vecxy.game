"""Headless equivalent of the Vecxy sidebar Export Scene button.
Run: blender --background Horror/Authoring/Sandbox.blend --python Horror/Tools/export_usd.py
"""
from pathlib import Path
import sys
import bpy

repo=Path(__file__).resolve().parents[2]
if not hasattr(bpy.types.Scene,'vecxy_usd'):
    sys.path.insert(0,str(repo/'Engine/Vecxy/tools/Blender'))
    import vecxy_usd
    vecxy_usd.register()
result=bpy.ops.vecxy.export_scene()
if result!={'FINISHED'}:
    raise RuntimeError('Vecxy USD export failed; inspect the addon error above')
