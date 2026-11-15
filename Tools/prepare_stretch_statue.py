"""Author a complete humanoid statue candidate and an in-place stretch, using a CC0 base.

Run with Blender --background --threads 1 --python-exit-code 1 --python this_file
-- --source <verified FBX> --output <candidate directory>. No downloads or installation.
The source is Quaternius Universal Base Characters [Standard], not a historic scan.
"""
import argparse,hashlib,json,math,sys
from pathlib import Path
import bpy
import bmesh
from mathutils import Vector,Quaternion

args=argparse.ArgumentParser();args.add_argument('--source',required=True);args.add_argument('--output',required=True)
opt=args.parse_args(sys.argv[sys.argv.index('--')+1:]);source=Path(opt.source).resolve();out=Path(opt.output).resolve();out.mkdir(parents=True,exist_ok=True)
assert source.is_file() and source.stat().st_size<5*1024**2
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(source))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
rig.name='StandingStatueRig';rig.animation_data_clear()
for o in list(bpy.context.scene.objects):
 if o.type=='MESH' and o.name=='Eyebrows':bpy.data.objects.remove(o,do_unlink=True)
body=[o for o in bpy.context.scene.objects if o.type=='MESH']

stone=bpy.data.materials.new('Warm limestone - authored');stone.diffuse_color=(.56,.51,.42,1);stone.use_nodes=True
nodes=stone.node_tree.nodes;shader=nodes.get('Principled BSDF');shader.inputs['Base Color'].default_value=(.56,.51,.42,1);shader.inputs['Roughness'].default_value=.78
noise=nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=95;noise.inputs['Detail'].default_value=2
bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.16;bump.inputs['Distance'].default_value=.006
stone.node_tree.links.new(noise.outputs['Fac'],bump.inputs['Height']);stone.node_tree.links.new(bump.outputs['Normal'],shader.inputs['Normal'])
for o in body:
 o.data.materials.clear();o.data.materials.append(stone)
 for face in o.data.polygons:face.material_index=0;face.use_smooth=True

# A draped waist wrap makes the stylized adult base read as a fictional classical sculpture.
# Weight it entirely to the pelvis: the stretch leaves hips, legs, pedestal and anchor fixed.
skin=next(o for o in body if o.name=='SuperHero_Male')
evaluated=skin.evaluated_get(bpy.context.evaluated_depsgraph_get())
evaluated_mesh=evaluated.to_mesh()
points=[evaluated.matrix_world@v.co for v in evaluated_mesh.vertices]
evaluated.to_mesh_clear()
hip_points=[p for p in points if .5<p.z<1.12]
hip_y_min=min(p.y for p in hip_points);hip_y_max=max(p.y for p in hip_points)
verts=[];faces=[];N=64
for z,rx,ry in [(.58,.225,.16),(.64,.23,.16),(.80,.225,.155),(.94,.18,.13),(1.05,.15,.105)]:
 section=[p for p in points if abs(p.z-z)<.085]
 yc=(hip_y_min+hip_y_max)*.5
 ry=max(ry,(hip_y_max-hip_y_min)*.5+.04)
 rx=max(rx,max(abs(p.x) for p in section)+.01);ry=max(ry,(max(p.y for p in section)-min(p.y for p in section))*.5+.01)
 # Bounding-box radii alone cut through curved hips at diagonal angles.
 enclosure=max(1,max(math.sqrt((p.x/rx)**2+((p.y-yc)/ry)**2) for p in section)/.965+.04)
 rx*=enclosure;ry*=enclosure
 for n in range(N):
  a=n*2*math.pi/N;fold=1+.035*math.cos(a*12);verts.append((rx*math.cos(a)*fold,yc+ry*math.sin(a)*fold,z+.009*math.sin(a*2)))
for j in range(4):
 for n in range(N):faces.append((j*N+n,j*N+(n+1)%N,(j+1)*N+(n+1)%N,(j+1)*N+n))
mesh=bpy.data.meshes.new('Authored pleated waist wrap');mesh.from_pydata(verts,[],faces);mesh.update()
wrap=bpy.data.objects.new('Stone waist drape',mesh);bpy.context.collection.objects.link(wrap);wrap.parent=rig
wrap.vertex_groups.new(name='pelvis').add(list(range(len(verts))),1,'REPLACE');mod=wrap.modifiers.new('Shared body rig','ARMATURE');mod.object=rig
wrap.data.materials.append(stone)
for face in mesh.polygons:face.use_smooth=True
body.append(wrap)
# Remove fully covered body faces, as with a fitted costume. Keeping hidden hip/thigh
# surfaces causes intersections after independent FBX skinning and wastes triangles.
bm=bmesh.new();bm.from_mesh(skin.data)
hidden=[f for f in bm.faces if .625 < (skin.matrix_world@f.calc_center_median()).z < 1.015]
bmesh.ops.delete(bm,geom=hidden,context='FACES_ONLY');bm.to_mesh(skin.data);bm.free();skin.data.update()

# Local rotation axes are derived from the bind pose, not guessed from FBX bone Euler conventions.
def turn(name,axis,degrees):
 bone=rig.pose.bones[name];bone.rotation_mode='QUATERNION'
 local=rig.data.bones[name].matrix_local.to_quaternion().inverted()@Vector(axis)
 bone.rotation_quaternion=Quaternion(local,math.radians(degrees))

def pose(frame,raise_factor,arch,lean=0):
 for b in rig.pose.bones:b.rotation_mode='QUATERNION';b.rotation_quaternion=Quaternion();b.location=Vector((0,0,0));b.scale=Vector((1,1,1))
 turn('upperarm_l',(0,1,0),75-140*raise_factor);turn('upperarm_r',(0,1,0),-75+140*raise_factor)
 turn('lowerarm_l',(0,1,0),-12*raise_factor);turn('lowerarm_r',(0,1,0),12*raise_factor)
 turn('clavicle_l',(0,1,0),-7*raise_factor);turn('clavicle_r',(0,1,0),7*raise_factor)
 turn('spine_01',(1,0,0),arch*.35);turn('spine_02',(1,0,0),arch*.35);turn('spine_03',(1,0,0),arch*.3)
 turn('neck_01',(1,0,0),-arch*.35);turn('Head',(1,0,0),-arch*.25)
 if lean:
  b=rig.pose.bones['spine_03'];local=rig.data.bones[b.name].matrix_local.to_quaternion().inverted()@Vector((0,1,0));b.rotation_quaternion=b.rotation_quaternion@Quaternion(local,math.radians(lean))
 for b in rig.pose.bones:b.keyframe_insert('rotation_quaternion',frame=frame,group=b.name)

# Anticipation -> arms reaching overhead -> torso extension -> small side release -> settle.
for values in [(1,0,0,0),(16,.06,-3,0),(43,.9,7,0),(64,1,10,0),(81,1,10,3),(98,1,8,-3),(116,.75,3,0),(145,0,0,0),(153,0,0,0)]:pose(*values)
action=rig.animation_data.action;action.name='Statue_OverheadStretch'
scene=bpy.context.scene;scene.render.fps=30;scene.frame_start=1;scene.frame_end=153
for curve in action.fcurves:
 for key in curve.keyframe_points:key.interpolation='BEZIER';key.handle_left_type='AUTO_CLAMPED';key.handle_right_type='AUTO_CLAMPED'

samples=[]
for frame in range(1,154):
 scene.frame_set(frame);bpy.context.view_layer.update()
 world=lambda name:list(rig.matrix_world@rig.pose.bones[name].head)
 samples.append({'frame':frame,'leftHand':world('hand_l'),'rightHand':world('hand_r'),'leftFoot':world('foot_l'),'rightFoot':world('foot_r'),'pelvis':world('pelvis')})
rest=samples[0]
for s in samples:
 for k in ['leftFoot','rightFoot','pelvis']:assert (Vector(s[k])-Vector(rest[k])).length<1e-5,(s['frame'],k)
assert max(s['leftHand'][2] for s in samples)-rest['leftHand'][2]>.9
assert (Vector(samples[-1]['leftHand'])-Vector(rest['leftHand'])).length<1e-5
scene.frame_set(1)
triangles=sum(len(p.vertices)-2 for o in body for p in o.data.polygons);assert triangles<16000
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
for o in body:o.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(out/'StandingStatue.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},bake_anim=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_simplify_factor=0,add_leaf_bones=False,axis_forward='-Z',axis_up='Y',path_mode='STRIP')
(out/'trajectory.json').write_text(json.dumps(samples,indent=2),encoding='utf-8')
(out/'provenance.json').write_text(json.dumps({'author':'Quaternius','source':'https://quaternius.itch.io/universal-base-characters','license':'CC0-1.0','sourceSHA256':hashlib.sha256(source.read_bytes()).hexdigest(),'triangles':triangles,'frames':153,'fps':30,'adaptation':'Authored stone material, pelvis-weighted pleated drape, removal of hidden body faces and complete overhead stretching animation. Fictional sculpture, not a historical artifact scan.','status':'Candidate; requires Unity rendering, production gesture integration and PICO verification.'},indent=2),encoding='utf-8')

# Offline visual review only. Lights/camera/pedestal are not exported into the character FBX.
bpy.ops.mesh.primitive_cylinder_add(vertices=64,radius=.32,depth=.07,location=(0,0,-.035));bpy.context.object.data.materials.append(stone)
world=bpy.data.worlds.new('Review world');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.12,.14,.16,1);world.node_tree.nodes['Background'].inputs[1].default_value=.6;scene.world=world
def area(name,pos,power,size):
 data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size;o=bpy.data.objects.new(name,data);scene.collection.objects.link(o);o.location=pos;o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
area('Soft key',(3,4,5),500,4);area('Fill',(-3,1,2),240,3);area('Rim',(0,-3,4),500,2)
camera=bpy.data.objects.new('Review camera',bpy.data.cameras.new('Review camera'));scene.collection.objects.link(camera);scene.camera=camera;camera.location=(3,6,2.6);camera.rotation_euler=(Vector((0,0,1.05))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=2.5
scene.render.engine='CYCLES';scene.cycles.samples=16;scene.cycles.use_denoising=True;scene.cycles.device='CPU';scene.render.threads_mode='FIXED';scene.render.threads=1;scene.render.resolution_x=720;scene.render.resolution_y=900;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX'
for frame in [1,43,64,98,153]:scene.frame_set(frame);scene.render.filepath=str(out/f'pose-{frame:03}.png');bpy.ops.render.render(write_still=True)
print('STATUE_CANDIDATE_PASS triangles='+str(triangles)+'; feet and pelvis stationary; complete loop closure; Unity acceptance pending')
