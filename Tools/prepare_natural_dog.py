"""Convert a pinned Wildfire Games dog candidate; CPU/thread bound by caller.

CC BY-SA 3.0 applies to the imported/modified dog assets, not project code.
Candidate only: not installed into Resources until visual/animation review.
"""
import hashlib
import json
import math
import argparse
import sys
from pathlib import Path
import shutil
import bpy
from mathutils import Vector, Matrix

root=Path(__file__).resolve().parents[1]
source=root/'.codex-tmp/natural-dog-source'
parser=argparse.ArgumentParser();parser.add_argument('--breed',choices=['Mastiff','Wolfhound'],default='Wolfhound')
breed=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []).breed
base=breed.lower()
output=root/'Assets/ThirdParty/NaturalPets'/breed
review=root/'.codex-tmp/natural-dog-review'
output.mkdir(parents=True,exist_ok=True);review.mkdir(parents=True,exist_ok=True)
provenance=json.loads((source/('source.json' if breed=='Mastiff' else 'wolfhound-source.json')).read_text())
assert provenance['commit']=='61a3b9507d974084e6badb88a0826bd89a6d5b8b'
for item in provenance['files']:
    assert hashlib.sha256((source/item['file']).read_bytes()).hexdigest()==item['sha256']
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.wm.collada_import(filepath=str(source/(base+'.dae')))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH')
assert len(rig.data.bones)<=64 and len(mesh.data.vertices)<5000
scene=bpy.context.scene;scene.render.fps=24
clips=[]
names=[base+'_idle_long.dae',base+('_idle2.dae' if breed=='Mastiff' else '_idle1.dae'),base+('_run.dae' if breed=='Mastiff' else '_walk.dae')]
for filename,name in zip(names,[breed+'_Idle',breed+'_IdleLook',breed+('_RunSource' if breed=='Mastiff' else '_WalkSource')]):
    previous=set(bpy.context.scene.objects)
    bpy.ops.wm.collada_import(filepath=str(source/filename))
    imported=set(bpy.context.scene.objects)-previous
    animated=next(o for o in imported if o.type=='ARMATURE' and o.animation_data and o.animation_data.action)
    mesh_only=set(rig.data.bones.keys())-set(animated.data.bones.keys())
    animation_only=set(animated.data.bones.keys())-set(rig.data.bones.keys())
    assert not animation_only and mesh_only<=({'prop-collar'} if breed=='Wolfhound' else set()),('Source bone names differ',filename,sorted(mesh_only),sorted(animation_only))
    # The wolfhound mesh has an unweighted attachment for the separate collar
    # prop. That accessory is not imported; all deforming bones must match.
    for attachment_name in mesh_only:
        group=mesh.vertex_groups.get(attachment_name)
        assert not group or not any(g.group==group.index and g.weight>0 for v in mesh.data.vertices for g in v.groups),('Unanimated weighted bone',attachment_name)
    rest_error=max(max(abs((rig.data.bones[b.name].matrix_local-b.matrix_local)[r][c]) for r in range(4) for c in range(4)) for b in animated.data.bones)
    assert rest_error<.0001,('Source rigs differ',filename,rest_error)
    action=animated.animation_data.action;action.name=name
    clips.append(dict(name=name,frames=list(action.frame_range),seconds=(action.frame_range.y-action.frame_range.x)/24))
    for obj in imported:bpy.data.objects.remove(obj,do_unlink=True)
rig.animation_data_create();rig.animation_data.action=bpy.data.actions[breed+'_Idle']
if rig.animation_data.action.slots:rig.animation_data.action_slot=rig.animation_data.action.slots[0]
scene.frame_set(0);bpy.context.view_layer.update()
# Keep the source idle/locomotion untouched. A short, bounded sniff is needed
# for the park's Feed interaction; preserve the imported rotation modes so the
# original Euler/quaternion tracks continue to work after creating this clip.
neutral_pose={pb.name:pb.matrix_basis.copy() for pb in rig.pose.bones}
assert 'Head' in rig.pose.bones and 'Neck3' in rig.pose.bones
rig.animation_data.action=bpy.data.actions.new(breed+'_Feed')
for frame in range(1,62,2):
    phase=(frame-1)/60*math.tau
    for pb in rig.pose.bones:pb.matrix_basis=neutral_pose[pb.name]
    for bone_name,amount in [('Neck3',.04),('Head',.12)]:
        pb=rig.pose.bones[bone_name]
        axis=pb.bone.matrix_local.to_quaternion().inverted()@Vector((1,0,0))
        pb.matrix_basis=neutral_pose[bone_name]@Matrix.Rotation(amount*(.5-.5*math.cos(phase)),4,axis)
    for pb in rig.pose.bones:
        rotation='rotation_quaternion' if pb.rotation_mode=='QUATERNION' else 'rotation_axis_angle' if pb.rotation_mode=='AXIS_ANGLE' else 'rotation_euler'
        for channel in ['location',rotation,'scale']:pb.keyframe_insert(data_path=channel,frame=frame,group=pb.name)
clips.append(dict(name=breed+'_Feed',frames=[1,61],seconds=2.5,authorship='MagicMR restrained sniff; source neutral pose, feet and tracking root held'))
rig.animation_data.action=bpy.data.actions[breed+'_Idle']
if rig.animation_data.action.slots:rig.animation_data.action_slot=rig.animation_data.action.slots[0]
scene.frame_set(0);bpy.context.view_layer.update()
coat_file='mastiff_a_01.png' if breed=='Mastiff' else 'wolfhound.png'
image=bpy.data.images.load(str(source/coat_file),check_existing=True)
material=bpy.data.materials.new(breed+' coat - Wildfire Games CC BY-SA 3.0');material.use_nodes=True
shader=material.node_tree.nodes.get('Principled BSDF');shader.inputs['Roughness'].default_value=.85
texture=material.node_tree.nodes.new('ShaderNodeTexImage');texture.image=image
material.node_tree.links.new(texture.outputs['Color'],shader.inputs['Base Color'])
mesh.data.materials.clear();mesh.data.materials.append(material)
for polygon in mesh.data.polygons:polygon.use_smooth=True
points=[mesh.matrix_world@Vector(p) for p in mesh.bound_box]
low=Vector(tuple(min(p[i] for p in points) for i in range(3)))
high=Vector(tuple(max(p[i] for p in points) for i in range(3)))
# Source game art has no measured real pet scale; use a nominal 70cm head height.
container=bpy.data.objects.new(breed+' nominal representation',None);bpy.context.collection.objects.link(container)
for obj in [rig]+[o for o in [mesh] if o.parent!=rig]:
    world=obj.matrix_world.copy();obj.parent=container;obj.matrix_world=world
scale=.7/(high.z-low.z);container.scale=Vector((scale,scale,scale))
container.location=Vector((-(low.x+high.x)*.5*scale,-(low.y+high.y)*.5*scale,-low.z*scale))
bpy.context.view_layer.update()
rig.animation_data.action=bpy.data.actions[breed+'_Feed']
if rig.animation_data.action.slots:rig.animation_data.action_slot=rig.animation_data.action.slots[0]
sniff_points=[];sniff_tips=[];rig_world=rig.matrix_world.copy()
for frame in [1,16,31,46,61]:
    scene.frame_set(frame);bpy.context.view_layer.update()
    evaluated=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get());sample=evaluated.to_mesh()
    try:sniff_points.append([evaluated.matrix_world@v.co for v in sample.vertices])
    finally:evaluated.to_mesh_clear()
    sniff_tips.append(rig.matrix_world@rig.pose.bones['Head'].tail)
    assert max(abs((rig.matrix_world-rig_world)[r][c]) for r in range(4) for c in range(4))<1e-6,'Sniff moved model root'
first=sniff_points[0];minimum=min(p.z for p in first);height=max(p.z for p in first)-minimum
feet=[i for i,p in enumerate(first) if p.z<minimum+height*.04]
assert len(feet)>=8,'No grounded foot sample vertices'
foot_error=max((points[i]-first[i]).length for points in sniff_points for i in feet)
head_motion=max((p-sniff_tips[0]).length for p in sniff_tips)
assert foot_error<.0001,('Sniff moved feet',foot_error)
assert .001<head_motion<.15,('Empty or excessive sniff',head_motion)
sniff_validation=dict(frames=[1,16,31,46,61],footVertices=len(feet),maximumFootDisplacementMeters=foot_error,maximumHeadTipDisplacementMeters=head_motion,scope='Blender evaluated skin at nominal scale; Unity validation pending')
rig.animation_data.action=bpy.data.actions[breed+'_Idle']
if rig.animation_data.action.slots:rig.animation_data.action_slot=rig.animation_data.action.slots[0]
scene.frame_set(0);bpy.context.view_layer.update()
scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=16
scene.render.resolution_x=640;scene.render.resolution_y=640;scene.render.resolution_percentage=100
scene.world.color=(.22,.22,.22)
floor_material=bpy.data.materials.new('Preview floor');floor_material.diffuse_color=(.19,.20,.21,1)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.002));floor=bpy.context.object;floor.data.materials.append(floor_material)
for name,location,energy,size in [('Key',(-2,-3,4),450,4),('Fill',(3,0,2.5),250,3),('Rim',(0,3,3),350,3)]:
    light=bpy.data.lights.new(name,'AREA');light.energy=energy;light.shape='DISK';light.size=size
    obj=bpy.data.objects.new(name,light);bpy.context.collection.objects.link(obj);obj.location=location;obj.rotation_euler=(Vector((0,0,.4))-obj.location).to_track_quat('-Z','Y').to_euler()
camera_data=bpy.data.cameras.new('Review camera');camera=bpy.data.objects.new('Review camera',camera_data);bpy.context.collection.objects.link(camera);scene.camera=camera
camera_data.type='ORTHO';camera_data.ortho_scale=1.1
for index,offset in enumerate([(1.5,-2,.9),(-1.5,-2,.9),(1.8,.2,.8)]):
    camera.location=Vector(offset);camera.rotation_euler=(Vector((0,0,.35))-camera.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(review/f'{breed}-source-{index}.png');bpy.ops.render.render(write_still=True)
# Selected export excludes review lighting/floor. No topology upscaling or AI texture.
bpy.ops.object.select_all(action='DESELECT')
for obj in [container,rig,mesh]:obj.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(output/(breed+'.fbx')),use_selection=True,axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,bake_anim_step=1,path_mode='STRIP')
shutil.copy2(source/coat_file,output/(breed+'_Albedo.png'))
shutil.copy2(source/'LICENSE-art.txt',output/'LICENSE.txt')
provenance.update(modifications=['Imported original Collada mesh and three source clips; source animation bind rigs verified equal','New 2.5s restrained head/upper-neck sniff from the source neutral pose; original clip rotation modes preserved','Smooth normals, original topology and 256px coat; nominal 0.7m head height, no physical-scale claim','Converted FBX axis convention; original master downloads unchanged'],clips=clips,sniffValidation=sniff_validation,vertices=len(mesh.data.vertices),triangles=sum(len(p.vertices)-2 for p in mesh.data.polygons),bones=len(rig.data.bones))
(output/'source-and-conversion.json').write_text(json.dumps(provenance,indent=2),encoding='utf-8')
(output/'CANDIDATE_STATUS.txt').write_text('UNACCEPTED: original-source model candidate. Not in Resources or runtime Dog. Blender import/render evidence only; Unity and interaction checks pending.\n',encoding='utf-8')
print('NATURAL_DOG_CANDIDATE_CONVERTED',json.dumps(dict(breed=breed,vertices=provenance['vertices'],triangles=provenance['triangles'],bones=len(rig.data.bones),clips=clips)))
