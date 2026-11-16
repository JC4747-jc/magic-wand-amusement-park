"""Preserve the CC BY cat mesh/rig and author restrained in-place pet clips.

The supplied clip has not been accepted as a natural idle. Keep the download
untouched; the derived candidate uses new restrained idle/feed clips instead.
No training, remeshing, eye enlargement, or texture synthesis is performed.
"""
import hashlib
import json
import math
import sys
import argparse
from pathlib import Path
import bpy
from mathutils import Vector, Quaternion, Matrix

root = Path(__file__).resolve().parents[1]
sys.path.insert(0,str(root/'Tools'))
from author_cat_gait import author_walk
source = root / '.codex-tmp/asset-candidates/bicolor-cat/source.glb'
parser=argparse.ArgumentParser()
parser.add_argument('--output',type=Path,default=root/'Assets/ThirdParty/NaturalPets/BicolorCat')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
output = args.output.resolve()
output.mkdir(parents=True, exist_ok=True)
assert hashlib.sha256(source.read_bytes()).hexdigest() == '4b395ba943d647f4c490c982a70fa2175ef91569615773da7ef3429e427ed21a'
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(source))
rig = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
# glTF importer creates an Icosphere as a bone-display shape, not animal anatomy.
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH' and o.name != 'Icosphere']
# Use the unmodified eyes and bind-pose silhouette. Shrinking detached globes
# was treating a pose/export problem as a geometry problem.
source_clips = [a.name for a in bpy.data.actions]
for obj in bpy.context.scene.objects:
    obj.animation_data_clear()
for action in list(bpy.data.actions):
    bpy.data.actions.remove(action)
for pb in rig.pose.bones:
    pb.matrix_basis = Matrix.Identity(4)
    pb.rotation_mode = 'QUATERNION'
bpy.context.view_layer.update()
# The source whiskers were a static mesh and would otherwise lag behind the
# animated head. Bind rigidly to the original head bone without moving them.
head_name = 'Wolf_Neck_TopSHJnt_14'
for obj in meshes:
    if not any(mod.type == 'ARMATURE' for mod in obj.modifiers):
        weights = obj.vertex_groups.new(name=head_name)
        weights.add(list(range(len(obj.data.vertices))), 1.0, 'REPLACE')
        modifier = obj.modifiers.new('Follow original head bone', 'ARMATURE')
        modifier.object = rig
        world = obj.matrix_world.copy()
        obj.parent = rig
        obj.matrix_world = world

def rotate(pb, axis, angle):
    local_axis = pb.bone.matrix_local.to_quaternion().inverted() @ Vector(axis)
    pb.rotation_quaternion = Quaternion(local_axis, angle)

scene = bpy.context.scene
scene.render.fps = 24
rig.animation_data_create()
for clip in ['BicolorCat_Idle', 'BicolorCat_Feed']:
    rig.animation_data.action = bpy.data.actions.new(clip)
    for frame in range(1, 62, 2):
        phase = (frame-1) / 60 * math.tau
        for pb in rig.pose.bones:
            pb.matrix_basis = Matrix.Identity(4)
        rotate(rig.pose.bones[head_name], (0,0,1), math.sin(phase)*.025)
        for i, name in enumerate(['Wolf_Tail_01_02SHJnt_37', 'Wolf_Tail_01_03SHJnt_36', 'Wolf_Tail_01_04SHJnt_35', 'Wolf_Tail_01_05SHJnt_34']):
            rotate(rig.pose.bones[name], (0,0,1), math.sin(phase+i*.35)*.035)
        if clip.endswith('Feed'):
            # A small sniff/nod, not a full crouch or an unverified eating rig.
            rotate(rig.pose.bones[head_name], (1,0,0), -.12*(.5-.5*math.cos(phase)))
        for pb in rig.pose.bones:
            for channel in ['location','rotation_quaternion','scale']:pb.keyframe_insert(data_path=channel, frame=frame, group=pb.name)
    for layer in rig.animation_data.action.layers:
        for strip in layer.strips:
            for slot in rig.animation_data.action.slots:
                bag = strip.channelbag(slot)
                if bag:
                    for curve in bag.fcurves:
                        for point in curve.keyframe_points:
                            point.interpolation = 'LINEAR'
walk_validation=author_walk(rig,meshes)
rig.animation_data.action = bpy.data.actions['BicolorCat_Idle']
scene.frame_set(1)
triangles = sum(len(p.vertices)-2 for obj in meshes for p in obj.data.polygons)
assert triangles < 15000 and len(rig.data.bones) <= 64
marker = bpy.data.objects.new('HeadMarker', None)
bpy.context.collection.objects.link(marker)
marker.parent = rig
marker.location = rig.data.bones[head_name].head_local
# glTF's cat normal map is on TEXCOORD_2, while URP/Lit uses UV0 for both
# albedo and normal. Reproject the shader's evaluated surface normal onto
# UV0, including the correct target tangent basis; copying texture pixels or
# sampling UV2 with UV0 tangents would both be wrong.
coat = bpy.data.materials['cat_diffuse']
normal_image = bpy.data.images.new('BicolorCat_UV0_Normal', width=1024, height=1024, alpha=False)
normal_image.colorspace_settings.name = 'Non-Color'
target = coat.node_tree.nodes.new('ShaderNodeTexImage')
target.image = normal_image
coat.node_tree.nodes.active = target
skin = max(meshes, key=lambda o:len(o.data.vertices))
bpy.ops.object.select_all(action='DESELECT')
skin.select_set(True)
bpy.context.view_layer.objects.active = skin
skin.data.uv_layers.active_index = 0
scene.render.engine = 'CYCLES'
scene.cycles.device = 'CPU'
scene.cycles.samples = 1
scene.render.bake.normal_space = 'TANGENT'
scene.render.bake.margin = 8
bpy.ops.object.bake(type='NORMAL')
normal_image.filepath_raw = str(output/'BicolorCat_UV0_Normal.png')
normal_image.file_format = 'PNG'
normal_image.save()
coat.node_tree.nodes.remove(target)
materials = []
for material in sorted(set(m for obj in meshes for m in obj.data.materials), key=lambda m:m.name):
    entry = dict(name=material.name, color=[1,1,1,1], roughness=.8, normalUV=2 if material.name=='cat_diffuse' else 0)
    for node in material.node_tree.nodes:
        if node.type == 'TEX_IMAGE' and node.image:
            image = node.image
            filename = image.name + '.png'
            image.filepath_raw = str(output / filename)
            image.file_format = 'PNG'
            image.save()
            links = [link for link in material.node_tree.links if link.from_node == node]
            if any(link.to_node.type == 'BSDF_PRINCIPLED' and link.to_socket.name == 'Base Color' for link in links):
                entry['albedo'] = filename
            elif any(link.to_node.type == 'NORMAL_MAP' for link in links):
                entry['normal'] = filename
    materials.append(entry)
    if material == coat:
        entry['normal'] = 'BicolorCat_UV0_Normal.png'
        entry['normalUV'] = 0
bpy.ops.object.select_all(action='DESELECT')
selected = set(meshes + [rig, marker])
for obj in list(selected):
    parent = obj.parent
    while parent is not None:
        selected.add(parent)
        parent = parent.parent
for obj in selected:
    obj.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(filepath=str(output/'BicolorCat.fbx'), use_selection=True,
    object_types={'ARMATURE','MESH','EMPTY'}, add_leaf_bones=False, bake_anim=True,
    bake_anim_use_nla_strips=False, bake_anim_use_all_actions=True, bake_anim_simplify_factor=0,bake_anim_step=.5,
    axis_forward='-Z', axis_up='Y', path_mode='STRIP')
(output/'source-and-conversion.json').write_text(json.dumps(dict(
    author='kenchoo; original cat by guillaume bolis', license='CC-BY-4.0',
    source='https://sketchfab.com/3d-models/bicolor-cat-e623a618ca344a8393d7ba4d63ec23cf',
    original='https://skfb.ly/6TKqH', mirror='https://github.com/code4fukui/vr-cats',
    mirror_commit=(source.parent/'mirror-commit.txt').read_text().strip(),
    source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(), triangles=triangles,
    bones=len(rig.data.bones), materials=materials, original_clips_excluded=source_clips,
    clips=['BicolorCat_Idle', 'BicolorCat_Feed','BicolorCat_Walk'],walk_validation=walk_validation,
    walk_trajectory=dict(revision='cat-contact-v2',stance_fraction=.65,
        swing='Quintic Hermite longitudinal return matching stance velocity and zero acceleration at contact; sin^3 lift',
        export='97 pose keys per 2-second cycle; FBX sampling/interpolation is checked separately, not claimed continuously C2'),
    conversion='Original topology, albedo, eyes and 40-bone rig retained. UV2 source normals reprojected into a 1K tangent-space UV0 map for URP. Authored restrained idle/sniff and four-beat in-place walk replace the supplied clip; static whiskers bound to original head. Not motion capture; integration status in DEVELOPMENT_STATUS.md.',
), indent=2), encoding='utf-8')
(output/'LICENSE.txt').write_text('Bicolor Cat - rebuilt and animated by kenchoo\n'
    'https://sketchfab.com/3d-models/bicolor-cat-e623a618ca344a8393d7ba4d63ec23cf\n'
    'Based on "3d modelling my cat: Fripouille" by guillaume bolis: https://skfb.ly/6TKqH\n'
    'CC BY 4.0: https://creativecommons.org/licenses/by/4.0/\n'
    'Changes: FBX/material conversion; topology, albedo, eyes and rig retained; UV2 normals baked to 1K UV0; authored idle/sniff/four-beat in-place walk clips replace original clip; whiskers bound to head.\n'
    'The public GLB mirror is code4fukui/vr-cats; its MIT code licence does not replace this asset licence.\n', encoding='utf-8')
(output/'CANDIDATE_STATUS.txt').write_text('DERIVED_CAT: UV0-corrected version with authored idle/sniff/four-beat in-place walk is installed by BicolorCatAssetBuilder.Install. Current integration/validation status is recorded in DEVELOPMENT_STATUS.md. Re-run model, Play Mode and visual checks after conversion; source clip is excluded. Real animal/headset acceptance remains pending.\n', encoding='utf-8')
print('BICOLOR_CAT_CONVERSION_PASS', triangles, len(rig.data.bones))
