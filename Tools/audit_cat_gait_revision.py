"""Compare the baseline and candidate cat's imported geometry/rig in Blender.

Run with Blender --background --threads 1 --python this_file -- ... .
This checks asset preservation, not measured animal motion or visual quality.
"""
import argparse
import json
import sys
from pathlib import Path

import bpy


def snapshot(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    rigs=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
    assert len(rigs)==1
    rig=rigs[0]
    bones={b.name:(b.parent.name if b.parent else None,[float(x) for row in b.matrix_local for x in row]) for b in rig.data.bones}
    meshes={}
    for obj in bpy.context.scene.objects:
        if obj.type!='MESH':continue
        mesh=obj.data
        meshes[obj.name]={
            'vertices':[list(v.co) for v in mesh.vertices],
            'faces':[tuple(p.vertices) for p in mesh.polygons],
            'weights':[{obj.vertex_groups[g.group].name:float(g.weight) for g in v.groups} for v in mesh.vertices],
            'uv':{layer.name:[list(p.uv) for p in layer.data] for layer in mesh.uv_layers},
            'materials':[m.name if m else None for m in mesh.materials]}
    return bones,meshes


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--baseline',type=Path,required=True)
    parser.add_argument('--candidate',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    a,am=snapshot(args.baseline);b,bm=snapshot(args.candidate)
    assert a.keys()==b.keys() and am.keys()==bm.keys()
    rest_error=0;vertex_error=0;weight_error=0;uv_error=0
    for name,(parent,matrix) in a.items():
        assert parent==b[name][0]
        rest_error=max(rest_error,max(abs(x-y) for x,y in zip(matrix,b[name][1])))
    for name,m in am.items():
        n=bm[name]
        assert m['faces']==n['faces'] and m['materials']==n['materials']
        assert len(m['vertices'])==len(n['vertices']) and len(m['weights'])==len(n['weights']) and m['uv'].keys()==n['uv'].keys()
        for x,y in zip(m['vertices'],n['vertices']):vertex_error=max(vertex_error,max(abs(i-j) for i,j in zip(x,y)))
        for x,y in zip(m['weights'],n['weights']):
            assert x.keys()==y.keys()
            if x:weight_error=max(weight_error,max(abs(x[k]-y[k]) for k in x))
        for layer in m['uv']:
            assert len(m['uv'][layer])==len(n['uv'][layer])
            for x,y in zip(m['uv'][layer],n['uv'][layer]):uv_error=max(uv_error,max(abs(i-j) for i,j in zip(x,y)))
    assert max(rest_error,vertex_error,weight_error,uv_error)<1e-6
    result={'status':'PASS_UNCHANGED_CAT_GEOMETRY_RIG_UV_WEIGHTS','meshes':len(am),'bones':len(a),'vertices':sum(len(m['vertices']) for m in am.values()),'maximumRestMatrixError':rest_error,'maximumVertexCoordinateError':vertex_error,'maximumWeightError':weight_error,'maximumUVError':uv_error,'scope':'Blender FBX import comparison; source mesh indices, material names, bone hierarchy and bind transforms; no animal or PICO acceptance'}
    args.output.parent.mkdir(parents=True,exist_ok=True);args.output.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(result))


if __name__=='__main__':main()
