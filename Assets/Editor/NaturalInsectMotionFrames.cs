using System;
using System.IO;
using System.Linq;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class NaturalInsectMotionFrames
{
    public static void Run()
    {
        string previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;bool async=ShaderUtil.allowAsyncCompilation;
        try
        {
            ShaderUtil.allowAsyncCompilation=false;
            foreach(var name in new[]{"Cockroach","Spider"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.35f,.37f,.39f);
                var key=new GameObject("Gait key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.4f;key.transform.rotation=Quaternion.Euler(38,-35,0);key.shadows=LightShadows.Hard;key.shadowStrength=.65f;key.shadowBias=.0001f;key.shadowNormalBias=.001f;key.shadowResolution=LightShadowResolution.VeryHigh;
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Contact plane";floor.transform.localPosition=new Vector3(0,-.001f,0);floor.transform.localScale=new Vector3(2,.002f,2);
                UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());var floorMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));floorMaterial.SetColor("_BaseColor",new Color(.24f,.26f,.28f));floorMaterial.SetFloat("_Smoothness",.05f);floor.GetComponent<Renderer>().sharedMaterial=floorMaterial;
                var instance=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/MagicMR/NaturalModels/"+name+".prefab"));var rig=instance.GetComponent<NaturalInsectRig>();rig.Initialize();rig.ResetPose();
                var reference=new InsectSkinReference(rig);var snapshot=new GameObject("Offline pose snapshot");snapshot.transform.SetParent(rig.transform,false);
                var mesh=UnityEngine.Object.Instantiate(rig.Skin.sharedMesh);snapshot.AddComponent<MeshFilter>().sharedMesh=mesh;snapshot.AddComponent<MeshRenderer>().sharedMaterial=rig.Skin.sharedMaterial;rig.Skin.enabled=false;
                // Explicit mesh updates avoid same-editor-frame GPU skin cache reuse in offline sampling.
                mesh.vertices=reference.Sample();mesh.normals=reference.Normals;mesh.RecalculateBounds();var bounds=snapshot.GetComponent<Renderer>().bounds;
                var camera=new GameObject("Motion camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.24f,.26f,.28f);camera.nearClipPlane=.001f;camera.farClipPlane=2;camera.orthographic=true;
                camera.gameObject.AddComponent<AudioListener>();camera.transform.position=bounds.center+new Vector3(.23f,.4f,-.35f);camera.transform.LookAt(bounds.center);
                float extent=0;for(int i=0;i<8;i++){var point=camera.transform.InverseTransformPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));extent=Mathf.Max(extent,Mathf.Abs(point.x),Mathf.Abs(point.y));}
                camera.orthographicSize=extent*1.25f;
                string output=Path.GetFullPath(".codex-tmp/insect-motion/"+name);Directory.CreateDirectory(output);
                var target=new RenderTexture(640,640,24);var pixels=new Texture2D(640,640,TextureFormat.RGB24,false);var old=RenderTexture.active;
                try
                {
                    camera.targetTexture=target;camera.Render();camera.Render();Vector3[] first=null;long bytes=0;
                    for(int frame=0;frame<48;frame++)
                    {
                        rig.Step(frame/24f,true);var points=reference.Sample();
                        if(frame==0)first=points.ToArray();if(frame==24&&points.Zip(first,(a,b)=>(a-b).sqrMagnitude).Max()<1e-6f)throw new Exception("Motion snapshot stayed static");
                        mesh.vertices=points;mesh.normals=reference.Normals;mesh.RecalculateBounds();camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,640,640),0,0);pixels.Apply();
                        var png=pixels.EncodeToPNG();bytes+=png.Length;if(bytes>24*1024*1024)throw new Exception("Motion frames exceeded disk budget");File.WriteAllBytes(Path.Combine(output,frame.ToString("D3")+".png"),png);
                    }
                    Debug.Log("INSECT_OFFLINE_MOTION_FRAMES_PASS "+name+": 48 frames at 24fps, "+bytes+" bytes; runtime IK with explicit skin snapshots, not live Play Mode or headset video");
                }
                finally{camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(floorMaterial);}
            }
        }
        finally{ShaderUtil.allowAsyncCompilation=async;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);}
    }
}
