"""Convert verified CC0 Bull Head with a restrained, fixed-base two-bone virtual rig. Blender --threads 1."""
import hashlib
import json
from pathlib import Path
import shutil
import bpy
import numpy as np
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT/'.codex-tmp/asset-candidates/polyhaven_bull_head'
OUT = ROOT/'Assets/ThirdParty/NaturalDecor/BronzeBeast'
OUT.mkdir(parents=True, exist_ok=True)
provenance = json.loads((SOURCE/'source.json').read_text())
assert provenance['license'] == 'CC0-1.0' and provenance['total_bytes'] <= 8*1024**2
for item in provenance['files']:
    assert hashlib.sha256((SOURCE/item['path']).read_bytes()).hexdigest() == item['sha256']
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(SOURCE/'bull_head_1k.gltf'))
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
assert len(meshes) == 1
model = meshes[0]
bpy.context.view_layer.objects.active = model
bpy.ops.object.select_all(action='DESELECT'); model.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
triangles = sum(len(p.vertices)-2 for p in model.data.polygons)
assert triangles == 17148
points = [v.co.copy() for v in model.data.vertices]
lo = Vector(tuple(min(v[i] for v in points) for i in range(3)))
hi = Vector(tuple(max(v[i] for v in points) for i in range(3)))
height = hi.z-lo.z; center = (lo+hi)*.5
assert .3 < height < .6
materials = []
for material in model.data.materials:
    entry = {'name': material.name}
    for node in material.node_tree.nodes:
        if node.type != 'TEX_IMAGE' or not node.image: continue
        image = node.image; filename = Path(bpy.path.abspath(image.filepath)).name
        source = next((SOURCE/f['path'] for f in provenance['files'] if Path(f['path']).name == filename), None)
        assert source and source.is_file()
        shutil.copy2(source, OUT/filename)
        if '_diff_' in filename: entry['albedo'] = filename
        elif '_nor_gl_' in filename: entry['normal'] = filename
        elif '_arm_' in filename:
            image.colorspace_settings.name = 'Non-Color'; w,h = image.size; assert w <= 1024 and h <= 1024
            raw = np.empty(w*h*4, dtype=np.float32); image.pixels.foreach_get(raw); raw = raw.reshape((h,w,4))
            packed = np.zeros_like(raw); packed[:,:,0] = raw[:,:,2]; packed[:,:,1] = raw[:,:,0]; packed[:,:,3] = 1-raw[:,:,1]
            generated = bpy.data.images.new('Bronze_URP', width=w, height=h, alpha=True); generated.colorspace_settings.name = 'Non-Color'
            generated.pixels.foreach_set(packed.ravel()); generated.filepath_raw = str(OUT/'Bronze_MetalAO_Smooth.png'); generated.file_format = 'PNG'; generated.save()
            check = bpy.data.images.load(str(OUT/'Bronze_MetalAO_Smooth.png'), check_existing=False); check.colorspace_settings.name = 'Non-Color'
            saved = np.empty(w*h*4, dtype=np.float32); check.pixels.foreach_get(saved)
            error = float(np.abs(saved-packed.ravel()).max()); assert error <= 1/255+.00001
            entry.update(packed='Bronze_MetalAO_Smooth.png', maximumPackingError=error)
            bpy.data.images.remove(check); bpy.data.images.remove(generated)
    assert all(k in entry for k in ('albedo','normal','packed'))
    materials.append(entry)
assert len(materials) == 1
# Source topology, UV and rest silhouette remain unchanged. Only authored bone weights are new.
rig_data = bpy.data.armatures.new('BronzeBeastRig'); rig = bpy.data.objects.new('BronzeBeastRig', rig_data); bpy.context.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig; rig.select_set(True); model.select_set(False)
bpy.ops.object.mode_set(mode='EDIT')
root = rig_data.edit_bones.new('BronzeBase'); root.head = (center.x,center.y,lo.z); root.tail = root.head+Vector((0,0,height*.1))
head = rig_data.edit_bones.new('BronzeHead'); head.head = (center.x,center.y,lo.z+height*.4); head.tail = head.head+Vector((0,0,height*.25)); head.parent = root
bpy.ops.object.mode_set(mode='OBJECT')
base_group = model.vertex_groups.new(name='BronzeBase'); head_group = model.vertex_groups.new(name='BronzeHead')
fixed = moving = 0
for vertex in model.data.vertices:
    t = max(0,min(1,((vertex.co.z-lo.z)/height-.16)/.34)); weight = t*t*(3-2*t)
    if weight < 1: base_group.add([vertex.index],1-weight,'REPLACE')
    if weight > 0: head_group.add([vertex.index],weight,'REPLACE'); moving += 1
    else: fixed += 1
modifier = model.modifiers.new('Restrained virtual awakening','ARMATURE'); modifier.object = rig; model.parent = rig
assert fixed > 100 and moving > 100
assert all((v.co-p).length == 0 for v,p in zip(model.data.vertices, points))
# The source head looks toward Blender -Y; Unity aligns this marker toward the viewer.
marker = bpy.data.objects.new('FrontMarker', None); bpy.context.collection.objects.link(marker); marker.location = center+Vector((0,-height,0))
bpy.ops.object.select_all(action='DESELECT')
for obj in (model,rig,marker): obj.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(filepath=str(OUT/'BronzeBeast.fbx'), use_selection=True, object_types={'MESH','ARMATURE','EMPTY'},
    add_leaf_bones=False, bake_anim=False, axis_forward='-Z', axis_up='Y', path_mode='STRIP')
assert (OUT/'BronzeBeast.fbx').stat().st_size < 4*1024**2
provenance.update(triangles=triangles, meshes=1, bones=2, fixed_base_vertices=fixed, moving_head_vertices=moving, materials=materials,
    original_bounds={'min': list(lo), 'max': list(hi)}, modifications=['Original topology, UV, diffuse and GL normal retained; FBX conversion.',
    'Original ARM numerically repacked for URP R=metal/G=AO/A=smoothness.',
    'Authored two-bone virtual head/neck rig; lower 16% of source height remains rigid. No claim of original artifact articulation.',
    'Generic bronze bovine bust representation; not a reconstruction/identification of a specific recognized museum object.'])
(OUT/'source-and-conversion.json').write_text(json.dumps(provenance,indent=2)+'\n')
(OUT/'LICENSE.txt').write_text('Bull Head by Tina / Poly Haven\nhttps://polyhaven.com/a/bull_head\nCC0 1.0: https://creativecommons.org/publicdomain/zero/1.0/\nOriginal mesh/UV/albedo/normal retained. Modified: channel repack, FBX conversion and virtual two-bone rig.\nNot an identified museum artifact or a measured reconstruction. See source-and-conversion.json for original hashes.\n')
print('BRONZE_BEAST_CONVERSION_PASS', triangles, fixed, moving, materials, flush=True)
