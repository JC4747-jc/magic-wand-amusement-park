using System;
using System.IO;
using System.Linq;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class NaturalPetMotionFrames
{
    public sealed class Snapshot
    {
        readonly Transform root;
        readonly SkinnedMeshRenderer skin;
        readonly Vector3[] source,normals,points,resultNormals;
        readonly Vector4[] tangents,resultTangents;
        readonly BoneWeight[] weights;
        readonly Matrix4x4[] bind,matrices;
        public readonly Mesh Mesh;
        public readonly MeshRenderer Renderer;
        public Snapshot(Transform presentation,SkinnedMeshRenderer renderer)
        {
            root=presentation;skin=renderer;var mesh=skin.sharedMesh;source=mesh.vertices;normals=mesh.normals;tangents=mesh.tangents;
            weights=mesh.boneWeights;bind=mesh.bindposes;matrices=new Matrix4x4[skin.bones.Length];points=new Vector3[source.Length];resultNormals=new Vector3[source.Length];resultTangents=new Vector4[source.Length];
            Mesh=UnityEngine.Object.Instantiate(mesh);var go=new GameObject("Offline pet skin snapshot");go.transform.SetParent(root,false);
            go.AddComponent<MeshFilter>().sharedMesh=Mesh;Renderer=go.AddComponent<MeshRenderer>();Renderer.sharedMaterials=skin.sharedMaterials;skin.enabled=false;
        }
        public Vector3[] Sample()
        {
            var inverse=root.worldToLocalMatrix;for(int n=0;n<matrices.Length;n++)matrices[n]=inverse*skin.bones[n].localToWorldMatrix*bind[n];
            for(int i=0;i<source.Length;i++)
            {
                var w=weights[i];Vector3 point=Vector3.zero,normal=Vector3.zero,tangent=Vector3.zero;
                void Add(int bone,float weight){if(weight<=0)return;var matrix=matrices[bone];point+=matrix.MultiplyPoint3x4(source[i])*weight;normal+=matrix.MultiplyVector(normals[i])*weight;if(tangents.Length==source.Length)tangent+=matrix.MultiplyVector(tangents[i])*weight;}
                Add(w.boneIndex0,w.weight0);Add(w.boneIndex1,w.weight1);Add(w.boneIndex2,w.weight2);Add(w.boneIndex3,w.weight3);
                points[i]=point;resultNormals[i]=normal.normalized;var t=tangent.normalized;resultTangents[i]=new Vector4(t.x,t.y,t.z,tangents.Length==source.Length?tangents[i].w:1);
            }
            Mesh.vertices=points;Mesh.normals=resultNormals;Mesh.tangents=resultTangents;Mesh.RecalculateBounds();return points;
        }
    }
    public static void Run()
    {
        string previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;bool async=ShaderUtil.allowAsyncCompilation;
        try
        {
            ShaderUtil.allowAsyncCompilation=false;
            foreach(var name in new[]{"CatIdle","CatSniff","CatWalk","DogWalk"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.55f,.55f);
                var key=new GameObject("Pet key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.4f;key.transform.rotation=Quaternion.Euler(38,-35,0);key.shadows=LightShadows.Soft;key.shadowStrength=.55f;key.shadowBias=.0001f;key.shadowNormalBias=.001f;key.shadowResolution=LightShadowResolution.VeryHigh;
                var fill=new GameObject("Pet fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.6f;fill.transform.rotation=Quaternion.Euler(25,145,0);
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=Vector3.down*.001f;floor.transform.localScale=new Vector3(20,.002f,20);UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
                var floorMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));floorMaterial.SetColor("_BaseColor",new Color(.24f,.26f,.28f));floorMaterial.SetFloat("_Smoothness",.05f);floor.GetComponent<Renderer>().sharedMaterial=floorMaterial;
                var instance=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/MagicMR/NaturalModels/"+(name=="DogWalk"?"Wolfhound":"BicolorCat")+".prefab"));var rig=instance.GetComponent<NaturalPetRig>();rig.Initialize();rig.Animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                var snapshots=instance.GetComponentsInChildren<SkinnedMeshRenderer>().Select(s=>new Snapshot(instance.transform,s)).ToArray();
                string suffix=name=="DogWalk"?"Wolfhound_WalkSource":name=="CatWalk"?"BicolorCat_Walk":name=="CatIdle"?"BicolorCat_Idle":"BicolorCat_Feed";
                var clip=rig.Animator.runtimeAnimatorController.animationClips.Distinct().Single(c=>c.name.EndsWith(suffix,StringComparison.Ordinal));
                int frameCount=Mathf.RoundToInt(clip.length*24);if(frameCount<24||frameCount>64)throw new Exception("Pet clip exceeds preview duration budget");
                clip.SampleAnimation(rig.Animator.gameObject,0);foreach(var s in snapshots)s.Sample();
                var bounds=snapshots[0].Renderer.bounds;foreach(var s in snapshots)bounds.Encapsulate(s.Renderer.bounds);
                var camera=new GameObject("Pet motion camera").AddComponent<Camera>();camera.orthographic=true;camera.nearClipPlane=.001f;camera.farClipPlane=5;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.24f,.26f,.28f);
                camera.transform.position=bounds.center+new Vector3(.75f,.35f,-1.2f);camera.transform.LookAt(bounds.center);
                float extent=0;for(int i=0;i<8;i++){var p=camera.transform.InverseTransformPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));extent=Mathf.Max(extent,Mathf.Abs(p.x),Mathf.Abs(p.y));}camera.orthographicSize=extent*1.22f;
                string output=Path.GetFullPath(".codex-tmp/pet-motion/"+name);Directory.CreateDirectory(output);var target=new RenderTexture(640,640,24);var pixels=new Texture2D(640,640,TextureFormat.RGB24,false);var old=RenderTexture.active;
                try
                {
                    camera.targetTexture=target;camera.Render();camera.Render();long bytes=0;Vector3[][] initial=null;float movement=0;
                    for(int frame=0;frame<frameCount;frame++)
                    {
                        clip.SampleAnimation(rig.Animator.gameObject,frame/24f);
                        var points=snapshots.Select(s=>s.Sample()).ToArray();if(frame==0)initial=points.Select(p=>p.ToArray()).ToArray();
                        if(frame==frameCount/2)for(int n=0;n<points.Length;n++)for(int i=0;i<points[n].Length;i++)movement=Mathf.Max(movement,(points[n][i]-initial[n][i]).sqrMagnitude);
                        camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,640,640),0,0);pixels.Apply();var png=pixels.EncodeToPNG();bytes+=png.Length;
                        if(bytes>24*1024*1024)throw new Exception("Pet motion frames exceeded disk budget");File.WriteAllBytes(Path.Combine(output,frame.ToString("D3")+".png"),png);
                    }
                    if(movement<1e-10f)throw new Exception("Pet clip produced a static mesh");
                    Debug.Log("PET_OFFLINE_MOTION_FRAMES_PASS "+name+": "+frameCount+" frames/24fps, "+bytes+" bytes, max half-cycle squared displacement="+movement+"; direct imported-clip samples and explicit skin snapshots, not headset video");
                }
                finally{camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);foreach(var s in snapshots)UnityEngine.Object.DestroyImmediate(s.Mesh);UnityEngine.Object.DestroyImmediate(floorMaterial);}
            }
        }
        finally{ShaderUtil.allowAsyncCompilation=async;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);}
    }
}
