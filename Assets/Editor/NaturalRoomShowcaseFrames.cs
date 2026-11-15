using System;
using System.IO;
using System.Linq;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class NaturalRoomShowcaseFrames
{
    public static void Run()
    {
        string previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;bool async=ShaderUtil.allowAsyncCompilation;
        try
        {
            ShaderUtil.allowAsyncCompilation=false;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.43f,.45f,.47f);
            var key=new GameObject("Room showcase key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.4f;key.transform.rotation=Quaternion.Euler(38,-35,0);key.shadows=LightShadows.Soft;key.shadowStrength=.6f;key.shadowBias=.0001f;key.shadowNormalBias=.001f;
            var fill=new GameObject("Room showcase fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.45f;fill.transform.rotation=Quaternion.Euler(30,145,0);
            var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Park/ParkRoom.prefab"));var actor=go.GetComponent<ParkActor>();go.SetActive(true);actor.Build();actor.label.gameObject.SetActive(false);
            var jelly=go.GetComponentInChildren<NaturalJellyfish>(true);jelly.transform.parent.gameObject.SetActive(true);jelly.SetVisibility(1);
            var renderers=actor.visual.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            var camera=new GameObject("Room showcase camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.18f,.20f);camera.orthographic=true;camera.nearClipPlane=.01f;
            camera.transform.position=bounds.center+new Vector3(.9f,.65f,-1.6f);camera.transform.LookAt(bounds.center);float extent=0;
            for(int i=0;i<8;i++){var p=camera.transform.InverseTransformPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));extent=Mathf.Max(extent,Mathf.Abs(p.x),Mathf.Abs(p.y));}camera.orthographicSize=extent*1.12f;
            var target=new RenderTexture(640,640,24);var pixels=new Texture2D(640,640,TextureFormat.RGB24,false);var old=RenderTexture.active;
            string output=Path.GetFullPath(".codex-tmp/room-motion/ReadingCornerPulse");Directory.CreateDirectory(output);
            try
            {
                camera.targetTexture=target;camera.Render();camera.Render();long bytes=0,difference=0;Color32[] first=null;
                for(int frame=0;frame<84;frame++)
                {
                    jelly.Step(0,frame/30f,true);camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,640,640),0,0);pixels.Apply();
                    if(frame==0)first=pixels.GetPixels32();if(frame==42){var colors=pixels.GetPixels32();for(int i=0;i<colors.Length;i++)difference+=Math.Abs(colors[i].r-first[i].r)+Math.Abs(colors[i].g-first[i].g)+Math.Abs(colors[i].b-first[i].b);}
                    var png=pixels.EncodeToPNG();bytes+=png.Length;if(bytes>24*1024*1024)throw new Exception("Room showcase frames exceeded disk cap");File.WriteAllBytes(Path.Combine(output,frame.ToString("D3")+".png"),png);
                }
                if(difference<640*640*.02f)throw new Exception("Room showcase lamp remained static");
                Debug.Log("ROOM_SHOWCASE_FRAMES_PASS: 84x640x640/30fps, "+bytes+" bytes; full fixed reading corner plus offline shader phase, no world bob/input or headset video");
            }
            finally{camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(target);}
        }
        finally{ShaderUtil.allowAsyncCompilation=async;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);}
    }
}
