"""Open the existing authoring file and ensure the Vecxy sidebar is available.
Run with Blender --python; this helper never regenerates or overwrites scene geometry.
"""
from pathlib import Path
import sys
import bpy

root=Path(__file__).resolve().parents[1]
if not bpy.data.filepath:
    bpy.ops.wm.open_mainfile(filepath=str(root/'Authoring/Sandbox.blend'))
if not hasattr(bpy.types.Scene,'vecxy_usd'):
    sys.path.insert(0,str(root.parent/'Engine/Vecxy/tools/Blender'))
    import vecxy_usd
    vecxy_usd.register()
print('Vecxy authoring scene ready; open N > Vecxy')
