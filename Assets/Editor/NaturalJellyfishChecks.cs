using System;
using System.IO;
using System.Linq;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NaturalJellyfishChecks
{
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run()
    {
        var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(NaturalJellyfishAssetBuilder.Prefab));
        try
        {
            var rig=go.GetComponent<NaturalJellyfish>();rig.Initialize();var mesh=go.GetComponent<MeshFilter>().sharedMesh;var points=mesh.vertices;var indices=mesh.triangles;
            Require(go.GetComponentsInChildren<Renderer>().Length==1&&mesh.vertexCount<10000&&indices.Length/3<12000,"Jellyfish draw/geometry budget exceeded");
            Require(points.All(p=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z)&&mesh.bounds.Contains(p)),"Invalid jellyfish vertices/bounds");
            Require(mesh.normals.All(n=>n.sqrMagnitude>.5f&&float.IsFinite(n.x)&&float.IsFinite(n.y)&&float.IsFinite(n.z)),"Invalid jellyfish normals");
            for(int i=0;i<indices.Length;i+=3)Require(Vector3.Cross(points[indices[i+1]]-points[indices[i]],points[indices[i+2]]-points[indices[i]]).sqrMagnitude>1e-20f,"Degenerate jellyfish triangle");
            Require(mesh.uv2.Select(p=>Mathf.RoundToInt(p.x)).Distinct().Count()==4,"Bell/fringe/oral-arm/internal anatomy metadata missing");
            go.transform.SetPositionAndRotation(new Vector3(.7f,.4f,-.2f),Quaternion.Euler(0,37,0));var matrix=go.transform.localToWorldMatrix;
            for(int i=0;i<32;i++)rig.Step(1/60f,i/60f,true);
            long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<512;i++)rig.Step(1/60f,i/60f,true);long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            Require(allocated==0,"Jellyfish Step allocates managed memory: "+allocated);
            Require(go.transform.localToWorldMatrix==matrix&&mesh.vertices.SequenceEqual(points),"Jellyfish CPU driver changed anchor/shared geometry");
            Require(rig.Visibility==1&&float.IsFinite(rig.Phase),"Jellyfish did not finish opening");
            foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity}){rig.Step(.1f,invalid,true);Require(rig.Visibility==0&&rig.Phase==0,"Invalid time retained jellyfish state");}
            rig.Step(-.1f,0,true);Require(rig.Visibility==0,"Negative dt retained visibility");rig.Step(.4f,2,true);rig.ResetPose();
            var block=new MaterialPropertyBlock();rig.Surface.GetPropertyBlock(block);Require(block.GetFloat("_Opacity")==0&&block.GetFloat("_Phase")==0,"Jellyfish reset left shader state");
            Debug.Log("NATURAL_JELLYFISH_RUNTIME_PASS: "+mesh.vertexCount+" vertices, "+indices.Length/3+" nondegenerate triangles, four anatomy roles, one draw; 512 steps zero managed bytes; shared geometry/anchor invariant, invalid-time/reset recovery");
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
    }
    public static void Render()
    {
        Run();string previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;bool async=ShaderUtil.allowAsyncCompilation;
        try
        {
            ShaderUtil.allowAsyncCompilation=false;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(NaturalJellyfishAssetBuilder.Prefab));var rig=go.GetComponent<NaturalJellyfish>();rig.SetVisibility(1);
            var light=new GameObject("Jelly key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1;light.transform.rotation=Quaternion.Euler(35,-35,0);
            var camera=new GameObject("Jelly review camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.028f,.041f,.06f);camera.orthographic=true;camera.orthographicSize=.235f;camera.nearClipPlane=.01f;
            camera.transform.position=new Vector3(.3f,.075f,-.65f);camera.transform.LookAt(new Vector3(0,-.08f,0));
            var target=new RenderTexture(800,900,24);var pixels=new Texture2D(800,900,TextureFormat.RGB24,false);var old=RenderTexture.active;string output=Path.GetFullPath(".codex-tmp/natural-jellyfish-review");Directory.CreateDirectory(output);
            try
            {
                camera.targetTexture=target;camera.Render();camera.Render();Color32[] first=null;long bytes=0;
                for(int sample=0;sample<4;sample++)
                {
                    rig.Step(0,.7f+sample*.7f,true);camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,800,900),0,0);pixels.Apply();
                    var colors=pixels.GetPixels32();var png=pixels.EncodeToPNG();bytes+=png.Length;Require(bytes<8*1024*1024,"Jellyfish review exceeded disk cap");File.WriteAllBytes(Path.Combine(output,"Pulse-"+sample+".png"),png);
                    if(sample==0)first=colors;
                    if(sample==2)
                    {
                        long difference=0;int changed=0;for(int i=0;i<colors.Length;i++){int delta=Math.Abs(colors[i].r-first[i].r)+Math.Abs(colors[i].g-first[i].g)+Math.Abs(colors[i].b-first[i].b);difference+=delta;if(delta>12)changed++;}
                        Require(changed>colors.Length*.005f&&difference/(double)(colors.Length*3)>.15,"Rendered GPU pulse stayed static/blank");
                    }
                }
                Require(!ShaderUtil.GetShaderMessages(rig.Surface.sharedMaterial.shader).Any(m=>m.severity.ToString()=="Error"),"Jellyfish shader compilation failed");
                Color32[] seamBefore=null;double seamDifference=0;
                for(int side=0;side<2;side++)
                {
                    rig.Step(0,2.8f+(side==0?-.0001f:.0001f),true);camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,800,900),0,0);pixels.Apply();var colors=pixels.GetPixels32();
                    File.WriteAllBytes(Path.Combine(output,side==0?"LoopBefore.png":"LoopAfter.png"),pixels.EncodeToPNG());
                    if(side==0)seamBefore=colors;else{for(int i=0;i<colors.Length;i++)seamDifference+=Math.Abs(colors[i].r-seamBefore[i].r)+Math.Abs(colors[i].g-seamBefore[i].g)+Math.Abs(colors[i].b-seamBefore[i].b);seamDifference/=colors.Length*3;}
                }
                Require(seamDifference<.1,"Jellyfish shader jumps across loop boundary: mean pixel difference="+seamDifference);
                File.WriteAllText(Path.Combine(output,"loop-validation.txt"),"PASS: actual GPU renders at period +/- 0.1ms; mean RGB difference="+seamDifference+" / 255.\n");
                Debug.Log("NATURAL_JELLYFISH_RENDERS_PASS: four pulse views plus loop seam renders, mean seam difference="+seamDifference+"; changed pixels; no headset/transparency acceptance");
            }
            finally{camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);}
        }
        finally{ShaderUtil.allowAsyncCompilation=async;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);}
    }
}
