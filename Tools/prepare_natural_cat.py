"""Run with Blender --background --factory-startup --threads 1 --python.

Convert the CC0 Innerscene cat candidate, merge draw calls, add a bounded
generic display rig and in-place idle/walk clips. This is authored animation,
not measured animal motion. Never executes scripts from a downloaded blend.
"""
import json
import hashlib
import math
from pathlib import Path

import bpy
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[1]
SOURCE=ROOT/'.codex-tmp/asset-candidates/innerscene-cat/cat.glb'
OUTPUT=ROOT/'Assets/ThirdParty/NaturalPets/Cat'
OUTPUT.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(SOURCE))
objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
assert objects, 'Candidate contains no geometry'
paws=[o.matrix_world.translation.copy() for o in objects if o.name.startswith('Rounded paw')]
assert len(paws)==4, 'Expected four authored paw locations'
nose=next(o.matrix_world.translation.copy() for o in objects if o.name=='Pink cat nose')
# Author an original procedural coat in 3D, then bake it into a bounded 1K UV
# map. The downloaded candidate's "coat" is only a flat palette texture.
coat=bpy.data.materials.get('Short tabby coat')
coat_mesh=next(o for o in objects if any(m==coat for m in o.data.materials))
nodes=coat.node_tree.nodes;links=coat.node_tree.links
shader=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
coordinates=nodes.new('ShaderNodeTexCoord')
noise=nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=55;noise.inputs['Detail'].default_value=2
links.new(coordinates.outputs['Generated'],noise.inputs['Vector'])
wave=nodes.new('ShaderNodeTexWave');wave.bands_direction='X';wave.inputs['Scale'].default_value=6;wave.inputs['Distortion'].default_value=5;wave.inputs['Detail Scale'].default_value=3
links.new(coordinates.outputs['Generated'],wave.inputs['Vector'])
ramp=nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].position=.35;ramp.color_ramp.elements[0].color=(.07,.048,.03,1)
ramp.color_ramp.elements[1].position=.68;ramp.color_ramp.elements[1].color=(.40,.32,.23,1)
links.new(wave.outputs['Color'],ramp.inputs['Fac'])
mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=.23
links.new(ramp.outputs['Color'],mix.inputs[1]);links.new(noise.outputs['Fac'],mix.inputs[2]);links.new(mix.outputs[0],shader.inputs['Base Color'])
shader.inputs['Roughness'].default_value=.88
image=bpy.data.images.new('Authored tabby coat',width=1024,height=1024,alpha=False)
target=nodes.new('ShaderNodeTexImage');target.image=image;nodes.active=target
bpy.ops.object.select_all(action='DESELECT');coat_mesh.select_set(True);bpy.context.view_layer.objects.active=coat_mesh
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.01);bpy.ops.object.mode_set(mode='OBJECT')
bpy.context.scene.render.engine='CYCLES';bpy.context.scene.cycles.device='CPU';bpy.context.scene.cycles.samples=8
bpy.context.scene.render.bake.use_pass_direct=False;bpy.context.scene.render.bake.use_pass_indirect=False;bpy.context.scene.render.bake.use_pass_color=True;bpy.context.scene.render.bake.margin=8
bpy.ops.object.bake(type='DIFFUSE')
links.new(target.outputs['Color'],shader.inputs['Base Color'])
materials=[]
for material in bpy.data.materials:
    if not material.use_nodes: continue
    shader=next((n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
    if not shader: continue
    entry={'name':material.name,'color':list(shader.inputs['Base Color'].default_value),'roughness':float(shader.inputs['Roughness'].default_value)}
    links=shader.inputs['Base Color'].links
    if links and links[0].from_node.type=='TEX_IMAGE':
        entry['color']=[1,1,1,1]
        image=links[0].from_node.image
        filename='albedo_'+str(len(materials))+'.png'
        image.filepath_raw=str(OUTPUT/filename);image.file_format='PNG';image.save()
        entry['albedo']=filename
    materials.append(entry)
bpy.ops.object.select_all(action='DESELECT')
for obj in objects: obj.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
before=sum(len(o.data.polygons) for o in objects)
for obj in objects:
    if len(obj.data.polygons)>10000:
        bpy.context.view_layer.objects.active=obj
        modifier=obj.modifiers.new('Mobile candidate reduction','DECIMATE');modifier.ratio=.65
        bpy.ops.object.modifier_apply(modifier=modifier.name)
bpy.context.view_layer.objects.active=objects[0]
bpy.ops.object.join()
skin=bpy.context.object;skin.name='NaturalCatSkin'
data=bpy.data.armatures.new('NaturalCatSkeleton')
rig=bpy.data.objects.new('NaturalCatRig',data);bpy.context.collection.objects.link(rig)
bpy.context.view_layer.objects.active=rig;skin.select_set(False);rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
segments={}
def bone(name,head,tail,parent=None):
    b=data.edit_bones.new(name);b.head=head;b.tail=tail
    if parent:b.parent=data.edit_bones[parent]
    segments[name]=(Vector(head),Vector(tail));return b
bone('Spine',(-.16,0,.17),(.07,0,.20))
bone('Neck',(.07,0,.20),(.17,0,.27),'Spine')
bone('Head',(.17,0,.27),(.24,0,.27),'Neck')
bone('Tail0',(-.22,0,.18),(-.27,0,.28),'Spine')
bone('Tail1',(-.27,0,.28),(-.30,0,.38),'Tail0')
bone('Tail2',(-.30,0,.38),(-.26,0,.46),'Tail1')
leg_names=[]
for index,paw in enumerate(sorted(paws,key=lambda p:(-p.x,p.y))):
    name=('Front' if index<2 else 'Rear')+('.L' if paw.y>0 else '.R')
    leg_names.append(name)
    bone(name,(paw.x*.9,paw.y*.8,.18),(paw.x,paw.y,.085),'Spine')
    bone(name+'Lower',(paw.x,paw.y,.085),(paw.x,paw.y,max(.015,paw.z)),name)
bpy.ops.object.mode_set(mode='OBJECT')
groups={name:skin.vertex_groups.new(name=name) for name in segments}
def distance(point,start,end):
    delta=end-start
    t=max(0,min(1,(point-start).dot(delta)/max(delta.length_squared,1e-8)))
    return (point-start-delta*t).length
for vertex in skin.data.vertices:
    p=vertex.co
    if p.x>.12 and p.z>.20: names=['Head','Neck']
    elif p.x<-.17 and p.z>.23:names=['Tail0','Tail1','Tail2','Spine']
    elif p.z<.15:names=leg_names+[n+'Lower' for n in leg_names]+['Spine']
    else:names=['Spine','Neck']
    nearest=sorted(((name,1/(distance(p,*segments[name])+.012)**3) for name in names),key=lambda pair:-pair[1])[:3]
    total=sum(w for _,w in nearest)
    for name,weight in nearest:groups[name].add([vertex.index],weight/total,'REPLACE')
modifier=skin.modifiers.new('NaturalCat display skeleton','ARMATURE');modifier.object=rig
skin.parent=rig
rig.animation_data_create()
for clip in ['NaturalCat_Idle','NaturalCat_Walk']:
    rig.animation_data.action=bpy.data.actions.new(clip)
    for frame in range(1,62,3):
        phase=(frame-1)/60*math.pi*2
        for pb in rig.pose.bones:pb.rotation_mode='XYZ';pb.rotation_euler=(0,0,0)
        rig.pose.bones['Head'].rotation_euler.z=math.sin(phase)*.035
        rig.pose.bones['Neck'].rotation_euler.x=math.sin(phase)*.008
        for index,name in enumerate(['Tail0','Tail1','Tail2']):rig.pose.bones[name].rotation_euler.z=math.sin(phase+index*.6)*.045
        if clip.endswith('Walk'):
            for index,name in enumerate(leg_names):
                swing=math.sin(phase+(0 if index in [0,3] else math.pi))
                rig.pose.bones[name].rotation_euler.z=swing*.16
                rig.pose.bones[name+'Lower'].rotation_euler.z=max(0,-swing)*.10
        for pb in rig.pose.bones:pb.keyframe_insert(data_path='rotation_euler',frame=frame,group=pb.name)
rig.animation_data.action=bpy.data.actions['NaturalCat_Idle']
bpy.context.scene.frame_set(1);bpy.context.scene.render.fps=30
marker=bpy.data.objects.new('HeadMarker',None);bpy.context.collection.objects.link(marker);marker.location=nose;marker.parent=rig
bpy.ops.object.select_all(action='DESELECT')
for obj in [skin,rig,marker]:obj.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(OUTPUT/'NaturalCat.fbx'),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},
    add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,
    axis_forward='-Z',axis_up='Y',path_mode='COPY',embed_textures=False)
report={'source':'https://www.innerscene.com/tools/library/3d-parts/domestic-cat-walking-4e8ff6c2',
        'source_sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
        'license':'CC0-1.0','original_has_animation':False,'derived_animation':'Authored generic in-place idle/walk; not real animal motion capture',
        'authored_material':'Original procedural 1K tabby coat, CPU color bake; source palette otherwise retained',
        'input_polygon_count':before,'output_polygon_count':len(skin.data.polygons),'vertices':len(skin.data.vertices),
        'bones':len(data.bones),'materials':materials,'clips':['NaturalCat_Idle','NaturalCat_Walk']}
(OUTPUT/'source-and-conversion.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
(OUTPUT/'LICENSE.txt').write_text('CC0-1.0\nSource: '+report['source']+'\nOriginal model published by Innerscene under CC0.\nDerived mesh reduction and display animation authored for Magic Wand Amusement Park.\nhttps://creativecommons.org/publicdomain/zero/1.0/\n',encoding='utf-8')
print('NATURAL_CAT_CONVERSION_COMPLETE',json.dumps({k:v for k,v in report.items() if k!='materials'}))
