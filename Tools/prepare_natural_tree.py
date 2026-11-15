"""Bounded offline LOD and wind rig for Rico Cilliers' CC0 Tree Small 02."""
import json
import math
from pathlib import Path
import shutil
from array import array
import bpy
import numpy as np
from mathutils import Vector

root=Path(__file__).resolve().parents[1]
source=root/'.codex-tmp/asset-candidates/polyhaven_tree_small_02'
output=root/'Assets/ThirdParty/NaturalTrees/SmallTree'
output.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(source/'tree_small_02_1k.gltf'))
original=next(o for o in bpy.context.scene.objects if o.type=='MESH')
bpy.context.view_layer.objects.active=original;original.select_set(True)
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.separate(type='MATERIAL');bpy.ops.object.mode_set(mode='OBJECT')

def leaf_cards(obj):
    """Fit original UV charts to planar cards; retain every leaf group instead of collapsing it to a speck."""
    mesh=obj.data;count=len(mesh.vertices);parents=array('i',range(count))
    def find(i):
        while parents[i]!=i:parents[i]=parents[parents[i]];i=parents[i]
        return i
    for polygon in mesh.polygons:
        first=find(polygon.vertices[0])
        for index in polygon.vertices[1:]:
            other=find(index)
            if first!=other:parents[other]=first
    points=np.empty(count*3,dtype=np.float32);mesh.vertices.foreach_get('co',points);points=points.reshape(-1,3)
    loop_vertices=np.empty(len(mesh.loops),dtype=np.int32);mesh.loops.foreach_get('vertex_index',loop_vertices)
    loop_uv=np.empty(len(mesh.loops)*2,dtype=np.float32);mesh.uv_layers[0].data.foreach_get('uv',loop_uv)
    uv=np.empty((count,2),dtype=np.float32);uv[loop_vertices]=loop_uv.reshape(-1,2)
    groups={}
    for i in range(count):groups.setdefault(find(i),[]).append(i)
    vertices=[];faces=[];coordinates=[]
    for indices in groups.values():
        chart=uv[indices].astype(np.float64);positions=points[indices].astype(np.float64)
        centre=positions.mean(axis=0);mean_uv=chart.mean(axis=0);minimum=chart.min(axis=0);maximum=chart.max(axis=0)
        centred=chart-mean_uv;covariance=centred.T@centred
        if abs(np.linalg.det(covariance))<1e-14:continue
        fit=np.linalg.solve(covariance,centred.T@(positions-centre))
        corners=np.array([(minimum[0],minimum[1]),(maximum[0],minimum[1]),(maximum[0],maximum[1]),(minimum[0],maximum[1])])
        fitted=centre+(corners-mean_uv)@fit
        if not np.isfinite(fitted).all():raise ValueError('Nonfinite leaf card fit')
        start=len(vertices);vertices.extend(map(tuple,fitted));faces.append((start,start+1,start+2,start+3));coordinates.extend(map(tuple,corners))
    result=bpy.data.meshes.new('Original canopy leaf cards');result.from_pydata(vertices,[],faces);result.materials.append(mesh.materials[0])
    layer=result.uv_layers.new(name='UVMap')
    for polygon in result.polygons:
        for loop in polygon.loop_indices:layer.data[loop].uv=coordinates[result.loops[loop].vertex_index]
    obj.data=result;bpy.data.meshes.remove(mesh)
    print('TREE_ORIGINAL_LEAF_CARDS',len(faces),flush=True)

parts={}
for obj in list(bpy.context.scene.objects):
    if obj.type!='MESH':continue
    material=obj.data.materials[0].name
    kind='Leaves' if 'leaves' in material else 'Branches' if 'branches' in material else 'Trunk'
    obj.name=kind;parts[kind]=obj
    triangles=sum(len(p.vertices)-2 for p in obj.data.polygons)
    bpy.context.view_layer.objects.active=obj
    if kind=='Leaves':leaf_cards(obj)
    else:
        target={'Branches':8500,'Trunk':6000}[kind]
        modifier=obj.modifiers.new('Bounded mobile LOD','DECIMATE');modifier.ratio=min(1,target/triangles)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    # Collapse can leave isolated original vertices/edges. Do not export or skin that unused payload.
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.delete_loose(use_verts=True,use_edges=True,use_faces=False);bpy.ops.object.mode_set(mode='OBJECT')
    # glTF branch textures use UV1 with KHR_texture_transform. FBX ignores shader mapping nodes.
    if kind=='Branches':
        uv=obj.data.uv_layers
        mapping=obj.data.materials[0].node_tree.nodes['Mapping']
        scale=mapping.inputs['Scale'].default_value;offset=mapping.inputs['Location'].default_value
        assert max(abs(value) for value in mapping.inputs['Rotation'].default_value)<1e-8
        # Read Blender's converted mapping, rather than applying raw glTF V offsets after Blender has flipped V.
        coordinates=[(loop.uv.x*scale.x+offset.x,loop.uv.y*scale.y+offset.y) for loop in uv[1].data]
        for loop,coordinate in zip(uv[0].data,coordinates):loop.uv=coordinate
    for layer in list(obj.data.uv_layers)[1:]:obj.data.uv_layers.remove(layer)
    for polygon in obj.data.polygons:polygon.use_smooth=True
    print('TREE_LOD',kind,len(obj.data.vertices),sum(len(p.vertices)-2 for p in obj.data.polygons),flush=True)

# Stable per-connected-leaf alpha supports whole-leaf autumn/winter thinning, not a noisy dissolve.
mesh=parts['Leaves'].data;parents=array('i',range(len(mesh.vertices)))
def find(i):
    while parents[i]!=i:parents[i]=parents[parents[i]];i=parents[i]
    return i
for polygon in mesh.polygons:
    first=find(polygon.vertices[0])
    for index in polygon.vertices[1:]:
        other=find(index)
        if first!=other:parents[other]=first
colour=mesh.color_attributes.new(name='SeasonMask',type='FLOAT_COLOR',domain='POINT')
for i,entry in enumerate(colour.data):
    seed=((find(i)*1664525+1013904223)&0xffffffff)/4294967296
    entry.color=(1,1,1,.001+seed*.998)
mesh.color_attributes.active_color=colour

rig=bpy.data.objects.new('TreeWindRig',bpy.data.armatures.new('Five crown sectors with fixed root'))
bpy.context.collection.objects.link(rig);bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.object.mode_set(mode='EDIT')
base=rig.data.edit_bones.new('TreeRoot');base.head=(0,0,0);base.tail=(0,0,1)
for i in range(5):
    bone=rig.data.edit_bones.new(f'Branch_{i:02d}');bone.head=(0,0,1.15);bone.tail=(0,0,2.15);bone.parent=base
bpy.ops.object.mode_set(mode='OBJECT')
for kind in ('Branches','Leaves'):
    obj=parts[kind];groups=[obj.vertex_groups.new(name='TreeRoot')]+[obj.vertex_groups.new(name=f'Branch_{i:02d}') for i in range(5)]
    for vertex in obj.data.vertices:
        point=obj.matrix_world@vertex.co
        weight=max(0,min(1,(point.z-1.15)/1.3));weight=weight*weight*(3-2*weight)
        sector=(math.atan2(point.y,point.x)%(2*math.pi))*5/(2*math.pi)
        i=int(sector);fraction=sector-i
        if weight<1:groups[0].add([vertex.index],1-weight,'REPLACE')
        if weight>0:
            groups[1+i].add([vertex.index],weight*(1-fraction),'REPLACE')
            if fraction>0:groups[1+(i+1)%5].add([vertex.index],weight*fraction,'REPLACE')
    obj.parent=rig;modifier=obj.modifiers.new('Wind skin - base remains fixed','ARMATURE');modifier.object=rig
triangles=sum(len(p.vertices)-2 for obj in parts.values() for p in obj.data.polygons)
assert triangles<90000
vertices=sum(len(obj.data.vertices) for obj in parts.values())
assert vertices<145000
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(output/'SmallTree.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},
    add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='STRIP')
for suffix in ('diff','nor_gl','branch_diff','branch_nor_gl','leaves_diff','leaves_nor_gl','leaves_alpha'):
    shutil.copyfile(source/'textures'/f'tree_small_02_{suffix}_1k.jpg',output/f'{suffix}.jpg')
metadata=json.loads((source/'source.json').read_text())
metadata.update(triangles=triangles,vertices=vertices,bones=6,wind_sectors=5,
    conversion='Original 1K maps retained, including original leaf alpha. Trunk/branches offline decimated; all nondegenerate leaf UV groups fitted to planar cards to preserve canopy density and texture instead of collapsing leaves. Under 90k triangles. Branch UV1/KHR transform baked to UV0. Five crown-sector bones, root-fixed height weights, per-leaf-card seasonal mask. No training.')
(output/'source-and-conversion.json').write_text(json.dumps(metadata,indent=2),encoding='utf-8')
(output/'LICENSE.txt').write_text('Tree Small 02 by Rico Cilliers / Poly Haven\nhttps://polyhaven.com/a/tree_small_02\n'
    'CC0 1.0: https://creativecommons.org/publicdomain/zero/1.0/\n'
    'Changes: offline LOD, UV transform baking, wind rig and leaf seasonal mask. Original image maps retained.\n',encoding='utf-8')
print('NATURAL_TREE_CONVERSION_PASS',triangles,flush=True)
