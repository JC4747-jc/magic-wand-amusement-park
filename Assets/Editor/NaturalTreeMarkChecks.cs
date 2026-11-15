using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class NaturalTreeMarkChecks
{
    public static void RebuildAndRender(){NaturalTreeMarkAssetBuilder.Run();Render();}
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    // Independent nearest-point test, rather than reusing the builder's ray intersection.
    static Vector3 Closest(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
    {
        var ab=b-a;var ac=c-a;var ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);
        if(d1<=0&&d2<=0)return a;var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return b;
        float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return a+ab*(d1/(d1-d3));
        var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return c;
        float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return a+ac*(d2/(d2-d6));
        float va=d3*d6-d5*d4;if(va<=0&&d4-d3>=0&&d5-d6>=0)return b+(c-b)*((d4-d3)/(d4-d3+d5-d6));
        float sum=va+vb+vc;if(Mathf.Abs(sum)<1e-20f)return a;return a+ab*(vb/sum)+ac*(vc/sum);
    }
    public static void Run()
    {
        var tree=PrefabUtility.LoadPrefabContents("Assets/Resources/MagicMR/NaturalModels/SmallTree.prefab");var mark=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/MagicMR/NaturalModels/TreeScar.prefab"));
        try
        {
            var anchor=tree.transform.Find("TreeGrowth/FantasyScarAnchor");Require(anchor,"Missing bark anchor");mark.transform.SetParent(anchor,false);
            var trunk=tree.GetComponentsInChildren<MeshFilter>().Single(f=>f.name=="Trunk");var source=trunk.sharedMesh.vertices.Select(trunk.transform.TransformPoint).ToArray();var triangles=trunk.sharedMesh.triangles;var mesh=mark.GetComponent<MeshFilter>().sharedMesh;
            Require(mesh.vertexCount==97&&mesh.triangles.Length==480,"Unexpected bark geometry budget");Require(mesh.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)),"Invalid bark geometry");
            float maximum=0;foreach(var local in mesh.vertices)
            {
                var point=mark.transform.TransformPoint(local);float distance=float.PositiveInfinity;
                for(int i=0;i<triangles.Length;i+=3)distance=Mathf.Min(distance,(point-Closest(point,source[triangles[i]],source[triangles[i+1]],source[triangles[i+2]])).sqrMagnitude);
                maximum=Mathf.Max(maximum,Mathf.Sqrt(distance));
            }
            Require(maximum<.001f,"Bark mark detached from trunk: "+maximum);var shader=mark.GetComponent<Renderer>().sharedMaterial.shader;Require(shader.name=="MagicMR/NaturalBarkMark"&&!ShaderUtil.ShaderHasError(shader),"Invalid bark shader");
            Require(mesh.colors.All(c=>c.r<=.35f&&c.g<=.26f&&c.b<=.15f),"Bark palette changed");
            Debug.Log("NATURAL_TREE_MARK_CHECK_PASS: independent nearest-triangle max distance="+maximum+" m, 97 vertices/160 triangles, matte bark palette, shared attached geometry");
        }
        finally{UnityEngine.Object.DestroyImmediate(mark);PrefabUtility.UnloadPrefabContents(tree);}
    }
    public static void Render()
    {
        Run();string previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;bool async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.5f,.51f,.52f);
            var tree=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/MagicMR/NaturalModels/SmallTree.prefab"));var anchor=tree.transform.Find("TreeGrowth/FantasyScarAnchor");
            var mark=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/MagicMR/NaturalModels/TreeScar.prefab"),anchor,false);
            var light=new GameObject("Review light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(35,-35,0);
            var camera=new GameObject("Bark detail review").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=.023f;camera.nearClipPlane=.001f;camera.farClipPlane=2;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.2f,.22f,.24f);camera.transform.position=anchor.position+anchor.forward*.18f;camera.transform.LookAt(anchor.position);
            var target=new RenderTexture(600,600,24);var pixels=new Texture2D(600,600,TextureFormat.RGB24,false);var old=RenderTexture.active;
            try{camera.targetTexture=target;camera.Render();camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,600,600),0,0);pixels.Apply();Directory.CreateDirectory(".codex-tmp/tree-mark-review");File.WriteAllBytes(".codex-tmp/tree-mark-review/Bark-EDITOR.png",pixels.EncodeToPNG());}
            finally{camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(target);}
            Debug.Log("NATURAL_TREE_MARK_RENDER_PASS: actual Unity closeup; authored fantasy bark mark, visual review required");
        }
        finally{ShaderUtil.allowAsyncCompilation=async;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);}
    }
}
