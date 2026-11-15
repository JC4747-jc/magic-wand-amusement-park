"""Preserve Poly Haven's CC0 Haworthia and add one rigid bone per original leaf."""
import json
from pathlib import Path
import shutil
import bpy
from mathutils import Vector

root=Path(__file__).resolve().parents[1]
source=root/'.codex-tmp/asset-candidates/polyhaven_potted_plant_04'
output=root/'Assets/ThirdParty/NaturalPlants/Haworthia'
output.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(source/'potted_plant_04_1k.gltf'))
plant=bpy.data.objects['potted_plant_04_plant']
bpy.ops.object.select_all(action='DESELECT');plant.select_set(True);bpy.context.view_layer.objects.active=plant
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.separate(type='LOOSE');bpy.ops.object.mode_set(mode='OBJECT')
leaves=sorted(bpy.context.selected_objects,key=lambda o:o.name)
assert len(leaves)==40
rig=bpy.data.objects.new('FoliageRig',bpy.data.armatures.new('Haworthia leaf skeleton'))
bpy.context.collection.objects.link(rig);bpy.context.view_layer.objects.active=rig
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
base=rig.data.edit_bones.new('PlantRoot');base.head=(0,0,.14);base.tail=(0,0,.15)
for index,leaf in enumerate(leaves):
    points=[leaf.matrix_world@v.co for v in leaf.data.vertices]
    lowest=min(p.z for p in points)
    roots=[p for p in points if p.z<=lowest+.004]
    head=sum(roots,Vector())/len(roots)
    tip=max(points,key=lambda p:(p-head).length_squared)
    bone=rig.data.edit_bones.new(f'Leaf_{index:02d}');bone.head=head;bone.tail=tip;bone.parent=base
bpy.ops.object.mode_set(mode='OBJECT')
for index,leaf in enumerate(leaves):
    group=leaf.vertex_groups.new(name=f'Leaf_{index:02d}');group.add(list(range(len(leaf.data.vertices))),1,'REPLACE')
bpy.ops.object.select_all(action='DESELECT')
for leaf in leaves:leaf.select_set(True)
bpy.context.view_layer.objects.active=leaves[0];bpy.ops.object.join();foliage=bpy.context.object;foliage.name='Foliage'
foliage.parent=rig
modifier=foliage.modifiers.new('Original leaf rigid skin','ARMATURE');modifier.object=rig
container=[bpy.data.objects[name] for name in ['potted_plant_04_ground','potted_plant_04_dirt','potted_plant_04_pot']]
bpy.ops.object.select_all(action='DESELECT')
for obj in container:obj.select_set(True)
bpy.context.view_layer.objects.active=container[0];bpy.ops.object.join();bpy.context.object.name='Container'
triangles=sum(len(p.vertices)-2 for obj in bpy.context.scene.objects if obj.type=='MESH' for p in obj.data.polygons)
assert triangles<12000
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(output/'Haworthia.fbx'),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},
    add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='STRIP')
for suffix in ['diff','nor_gl','arm']:
    shutil.copyfile(source/'textures'/f'potted_plant_04_{suffix}_1k.jpg',output/f'{suffix}.jpg')
provenance=json.loads((source/'source.json').read_text())
provenance.update(triangles=triangles,bones=41,leaf_bones=40,
    conversion='Original mesh, UV and 1K textures retained. Container merged; original loose leaves joined and each assigned to one rigid bone. No training or texture generation.')
(output/'source-and-conversion.json').write_text(json.dumps(provenance,indent=2),encoding='utf-8')
(output/'LICENSE.txt').write_text('Potted Plant 04 by James Ray Cock / Poly Haven\nhttps://polyhaven.com/a/potted_plant_04\n'
    'CC0 1.0: https://creativecommons.org/publicdomain/zero/1.0/\n'
    'Changes: container merge and rigid per-leaf skinning; original shape, UV and textures retained.\n',encoding='utf-8')
print('NATURAL_PLANT_CONVERSION_PASS',triangles,'triangles',40,'leaf bones')
