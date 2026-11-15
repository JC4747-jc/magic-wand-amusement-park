using System;
using System.IO;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NaturalJellyfishMotionFrames
{
    public static void Run()
    {
        string previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;bool async=ShaderUtil.allowAsyncCompilation;
        try
        {
            ShaderUtil.allowAsyncCompilation=false;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(NaturalJellyfishAssetBuilder.Prefab));var rig=go.GetComponent<NaturalJellyfish>();rig.SetVisibility(1);
            var key=new GameObject("Jelly motion key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1;key.transform.rotation=Quaternion.Euler(35,-35,0);
            var camera=new GameObject("Jelly motion camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.028f,.041f,.06f);camera.orthographic=true;camera.orthographicSize=.225f;camera.nearClipPlane=.01f;
            camera.transform.position=new Vector3(.3f,.075f,-.65f);camera.transform.LookAt(new Vector3(0,-.08f,0));
            var target=new RenderTexture(640,640,24);var pixels=new Texture2D(640,640,TextureFormat.RGB24,false);var old=RenderTexture.active;
            string output=Path.GetFullPath(".codex-tmp/room-motion/JellyfishPulse");Directory.CreateDirectory(output);
            try
            {
                camera.targetTexture=target;camera.Render();camera.Render();long bytes=0;Color32[] first=null;long difference=0;
                for(int frame=0;frame<84;frame++)
                {
                    rig.Step(0,frame/30f,true);camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,640,640),0,0);pixels.Apply();
                    if(frame==0)first=pixels.GetPixels32();
                    if(frame==42){var colors=pixels.GetPixels32();for(int i=0;i<colors.Length;i++)difference+=Math.Abs(colors[i].r-first[i].r)+Math.Abs(colors[i].g-first[i].g)+Math.Abs(colors[i].b-first[i].b);}
                    var png=pixels.EncodeToPNG();bytes+=png.Length;if(bytes>24*1024*1024)throw new Exception("Jellyfish frames exceeded 24 MiB cap");File.WriteAllBytes(Path.Combine(output,frame.ToString("D3")+".png"),png);
                }
                if(difference<640*640*.15f)throw new Exception("Jellyfish movie frames stayed static");
                Debug.Log("JELLYFISH_OFFLINE_MOTION_FRAMES_PASS: 84 frames at 30fps, "+bytes+" bytes; runtime shader phase sampled offline, separate Play Mode evidence required");
            }
            finally{camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(target);}
        }
        finally{ShaderUtil.allowAsyncCompilation=async;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);}
    }
}
