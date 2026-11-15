"""Convert the attributed CC0 Poly Haven bust; run with Blender --threads 1."""
import hashlib
import json
from pathlib import Path
import bpy

root = Path(__file__).resolve().parents[1]
source = root / '.codex-tmp/asset-candidates/marble-bust/source.glb'
output = root / 'Assets/ThirdParty/NaturalDecor/Statue'
output.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(source))
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
triangles = sum(len(p.vertices)-2 for o in meshes for p in o.data.polygons)
assert triangles <= 20000, triangles
for image in bpy.data.images:
    if image.size[0] and image.packed_file:
        image.filepath_raw = str(output / (image.name + '.jpg'))
        image.file_format = 'JPEG'
        image.save()
bpy.ops.object.select_all(action='DESELECT')
for obj in meshes:
    obj.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(output/'MarbleBust.fbx'), use_selection=True,
                         object_types={'MESH'}, bake_anim=False, axis_forward='-Z', axis_up='Y',
                         add_leaf_bones=False, path_mode='STRIP')
(output/'source-and-conversion.json').write_text(json.dumps(dict(
    source='https://polyhaven.com/a/marble_bust_01',
    mirror='https://www.innerscene.com/tools/library/3d-parts/marble-bust-f5604fc9',
    author='Rico Cilliers (Poly Haven)', license='CC0-1.0',
    source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(), triangles=triangles,
    conversion='Original geometry and embedded PBR textures; FBX export, no generated anatomy or rig',
), indent=2), encoding='utf-8')
(output/'LICENSE.txt').write_text('Marble Bust 01 by Rico Cilliers / Poly Haven\n'
    'https://polyhaven.com/a/marble_bust_01\nhttps://polyhaven.com/license\n'
    'CC0 1.0 Universal: https://creativecommons.org/publicdomain/zero/1.0/\n'
    'Downloaded from the attributed CC0 Innerscene mirror. Geometry and PBR images retained.\n', encoding='utf-8')
print('STATUE_CONVERSION_PASS', triangles)
