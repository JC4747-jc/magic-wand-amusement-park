using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class NaturalPlantChecks
{
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/MagicMR/NaturalModels/Haworthia.prefab");
        Require(prefab,"Natural plant prefab missing");var instance=UnityEngine.Object.Instantiate(prefab);var baked=new Mesh();
        try
        {
            var skin=instance.GetComponentInChildren<SkinnedMeshRenderer>();
            var container=instance.GetComponentsInChildren<MeshRenderer>().Single();
            var growth=instance.transform.Find("Growth");
            Require(skin&&growth&&skin.transform.IsChildOf(growth)&&!container.transform.IsChildOf(growth),"Foliage/container growth isolation failed");
            int triangles=skin.sharedMesh.triangles.Length/3+container.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3;
            Require(triangles<12000&&skin.bones.Length<=64,"Plant exceeded geometry/skinning budget");
            Require(skin.bones.Count(b=>b.name.StartsWith("Leaf_"))==40,"Plant leaf bones lost during import");
            var fixedMatrix=container.localToWorldMatrix;var fixedBounds=container.bounds;
            skin.BakeMesh(baked);var before=baked.vertices;
            foreach(var bone in skin.bones.Where(b=>b.name.StartsWith("Leaf_")))bone.localRotation*=Quaternion.Euler(8,0,0);
            skin.BakeMesh(baked);var after=baked.vertices;int moved=0;
            for(int i=0;i<after.Length;i++)
            {
                Require(float.IsFinite(after[i].x)&&float.IsFinite(after[i].y)&&float.IsFinite(after[i].z),"Leaf skin generated a nonfinite point");
                if((after[i]-before[i]).sqrMagnitude>1e-10f)moved++;
            }
            Require(moved>after.Length*.25f,"Bone pose did not deform the imported leaf mesh");
            var pivot=growth.position;growth.localScale=new Vector3(1,1.4f,1);growth.localRotation=Quaternion.Euler(0,0,3);
            Require((growth.position-pivot).sqrMagnitude<1e-12f,"Growth moved its soil pivot");
            Require(container.localToWorldMatrix==fixedMatrix&&container.bounds==fixedBounds,"Growth moved/scaled the pot or soil");
            Debug.Log("NATURAL_PLANT_SKIN_PASS: "+triangles+" triangles, "+moved+"/"+after.Length+" vertices deformed; pot/soil invariant through growth and sway");
        }
        finally{UnityEngine.Object.DestroyImmediate(baked);UnityEngine.Object.DestroyImmediate(instance);}
    }
}
