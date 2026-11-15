using MagicMR;
using UnityEngine;

// Independent linear-blend skinning reference in presentation-root coordinates.
// Avoids interpreting BakeMesh's FBX scale as another renderer-local transform.
public sealed class InsectSkinReference
{
    readonly NaturalInsectRig rig;
    readonly Vector3[] source,result,normals,resultNormals;
    readonly BoneWeight[] weights;
    readonly Matrix4x4[] bind,matrices;
    readonly Transform[] bones;
    public InsectSkinReference(NaturalInsectRig value)
    {
        rig=value;rig.Initialize();var mesh=rig.Skin.sharedMesh;source=mesh.vertices;normals=mesh.normals;weights=mesh.boneWeights;bind=mesh.bindposes;bones=rig.Skin.bones;
        matrices=new Matrix4x4[bones.Length];result=new Vector3[source.Length];
        resultNormals=new Vector3[source.Length];
    }
    public Vector3[] Normals=>resultNormals;
    public Vector3[] Sample()
    {
        var inverse=rig.transform.worldToLocalMatrix;
        for(int i=0;i<bones.Length;i++)matrices[i]=inverse*bones[i].localToWorldMatrix*bind[i];
        for(int i=0;i<source.Length;i++)
        {
            var w=weights[i];var point=matrices[w.boneIndex0].MultiplyPoint3x4(source[i])*w.weight0;var normal=matrices[w.boneIndex0].MultiplyVector(normals[i])*w.weight0;
            if(w.weight1>0){point+=matrices[w.boneIndex1].MultiplyPoint3x4(source[i])*w.weight1;normal+=matrices[w.boneIndex1].MultiplyVector(normals[i])*w.weight1;}
            if(w.weight2>0){point+=matrices[w.boneIndex2].MultiplyPoint3x4(source[i])*w.weight2;normal+=matrices[w.boneIndex2].MultiplyVector(normals[i])*w.weight2;}
            if(w.weight3>0){point+=matrices[w.boneIndex3].MultiplyPoint3x4(source[i])*w.weight3;normal+=matrices[w.boneIndex3].MultiplyVector(normals[i])*w.weight3;}
            result[i]=point;resultNormals[i]=normal.normalized;
        }
        return result;
    }
}
