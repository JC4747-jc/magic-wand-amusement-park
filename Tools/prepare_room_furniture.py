"""Convert two verified small CC0 props; preserve original mesh/UV/textures.

The ARM map is numeric material data, packed for URP as R=metal, G=AO,
A=smoothness. No image synthesis, training or mesh upscaling.
"""
import hashlib,json,shutil
from pathlib import Path
import bpy
import numpy as np
from mathutils import Vector

root=Path(__file__).resolve().parents[1]
for asset,name in [('modern_arm_chair_01','ReadingChair'),('ceramic_vase_01','CeramicVase')]:
    source=root/'.codex-tmp/asset-candidates'/('polyhaven_'+asset)
    output=root/'Assets/ThirdParty/NaturalDecor'/name;output.mkdir(parents=True,exist_ok=True)
    provenance=json.loads((source/'source.json').read_text())
    for item in provenance['files']:
        assert hashlib.sha256((source/item['path']).read_bytes()).hexdigest()==item['sha256']
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    for data in list(bpy.data.materials):bpy.data.materials.remove(data)
    bpy.ops.import_scene.gltf(filepath=str(source/(asset+'_1k.gltf')))
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    triangles=sum(len(p.vertices)-2 for o in meshes for p in o.data.polygons)
    assert 0<triangles<15000 and len(meshes)<=4
    points=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
    lo=Vector(tuple(min(p[i] for p in points) for i in range(3)));hi=Vector(tuple(max(p[i] for p in points) for i in range(3)));center=(lo+hi)*.5
    materials=[]
    for material in sorted(set(m for o in meshes for m in o.data.materials),key=lambda m:m.name):
        entry={'name':material.name}
        for node in material.node_tree.nodes:
            if node.type!='TEX_IMAGE' or not node.image:continue
            image=node.image;filename=Path(bpy.path.abspath(image.filepath)).name
            if not filename:continue
            actual=next((source/item['path'] for item in provenance['files'] if Path(item['path']).name==filename),None)
            assert actual and actual.exists(),filename
            shutil.copy2(actual,output/filename)
            if '_diff_' in filename:entry['albedo']=filename
            elif '_nor_gl_' in filename:entry['normal']=filename
            elif '_arm_' in filename:
                image.colorspace_settings.name='Non-Color';w,h=image.size;assert w<=1024 and h<=1024
                pixels=np.empty(w*h*4,dtype=np.float32);image.pixels.foreach_get(pixels);pixels=pixels.reshape((h,w,4))
                packed=np.zeros_like(pixels);packed[:,:,0]=pixels[:,:,2];packed[:,:,1]=pixels[:,:,0];packed[:,:,3]=1-pixels[:,:,1]
                generated=bpy.data.images.new(material.name+'_URP',width=w,height=h,alpha=True);generated.colorspace_settings.name='Non-Color';generated.pixels.foreach_set(packed.ravel())
                converted=material.name+'_URP_MetalAO_Smooth.png';generated.filepath_raw=str(output/converted);generated.file_format='PNG';generated.save();entry['packed']=converted
                # Verify saved channels against the original data, including alpha.
                check=bpy.data.images.load(str(output/converted),check_existing=False);check.colorspace_settings.name='Non-Color';saved=np.empty(w*h*4,dtype=np.float32);check.pixels.foreach_get(saved)
                error=float(np.max(np.abs(saved-packed.ravel())));assert error<=1/255+.00001,error;entry['maximumPackingError']=error
                bpy.data.images.remove(check);bpy.data.images.remove(generated)
        assert all(key in entry for key in ['albedo','normal','packed']),entry
        materials.append(entry)
    # Explicit front reference is reviewed in Unity; the chair's seat opens
    # toward Blender -Y in the source. No arbitrary antiparallel rotation.
    marker=bpy.data.objects.new('FrontMarker',None);bpy.context.collection.objects.link(marker);marker.location=center+Vector((0,-(hi.z-lo.z),0))
    bpy.ops.object.select_all(action='DESELECT');selected=set(meshes+[marker])
    for obj in list(selected):
        parent=obj.parent
        while parent is not None:selected.add(parent);parent=parent.parent
    for obj in selected:obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(output/(name+'.fbx')),use_selection=True,object_types={'MESH','EMPTY'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='STRIP')
    provenance.update(triangles=triangles,meshes=len(meshes),materials=materials,modifications=['Original mesh, UV and 1K diffuse/GL normal retained; FBX conversion','ARM numeric channel repack to URP R=metal/G=AO/A=smoothness; original ARM retained','Unity display normalization/front alignment applied by asset builder; no measured room reconstruction'])
    (output/'source-and-conversion.json').write_text(json.dumps(provenance,indent=2)+'\n')
    (output/'LICENSE.txt').write_text(asset+' by '+', '.join(provenance['info']['authors'])+' / Poly Haven\nCC0 1.0: https://creativecommons.org/publicdomain/zero/1.0/\n'+provenance['source']+'\nChanges and per-file original SHA-256: source-and-conversion.json\n')
    print('ROOM_FURNITURE_CONVERTED',name,triangles,len(meshes),materials,flush=True)
