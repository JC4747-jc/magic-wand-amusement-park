using System;
using System.IO;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ParkVisualPreview
{
    // Render explicit synthetic editor fixtures; never treat these as headset captures.
    public static void Run() {Render(false);}
    public static void RunRecorded() {Render(true);}
    public static void RunBugImpact() {
        string previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        string output=Path.GetFullPath("Reports/bug-impact-visual-simulation");Directory.CreateDirectory(output);
        try {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Synthetic bug impact camera").AddComponent<Camera>();camera.tag="MainCamera";
            camera.transform.position=new Vector3(.22f,.3f,-.45f);camera.transform.LookAt(Vector3.zero);camera.fieldOfView=42;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.14f,.18f);
            var actor=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Park/ParkBugs.prefab")).GetComponent<ParkActor>();
            actor.virtualTarget=true;actor.gameObject.SetActive(true);actor.Build();if(actor.label)actor.label.gameObject.SetActive(false);
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var body=(System.Collections.Generic.List<Renderer>)typeof(ParkActor).GetField("body",flags).GetValue(actor);
            Vector3 initial=body[0].transform.localScale;Vector3 anchor=actor.transform.position;
            var stateField=typeof(ParkActor).GetField("state",flags);object state=stateField.GetValue(actor);state.GetType().GetField("rewarded").SetValue(state,true);stateField.SetValue(actor,state);
            foreach(float progress in new[]{0f,.175f,.6f,1f}) {
                typeof(ParkActor).GetField("bugDefeatProgress",flags).SetValue(actor,progress);
                typeof(ParkActor).GetMethod("ApplyBugDefeatPose",flags).Invoke(actor,null);
                typeof(ParkActor).GetMethod("ApplyVisibility",flags).Invoke(actor,null);
                if(actor.transform.position!=anchor)throw new Exception("Impact moved tracking root");
                if(progress==.175f && !(body[0].transform.localScale.y<initial.y && body[0].transform.localScale.x>initial.x))throw new Exception("Missing impact squash");
                if(progress==1 && body[0].enabled)throw new Exception("Defeated bug remained visible");
                var target=new RenderTexture(800,600,24);var pixels=new Texture2D(800,600,TextureFormat.RGB24,false);var old=RenderTexture.active;
                try {camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,800,600),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(output,"Impact-"+Mathf.RoundToInt(progress*100)+"-SIMULATION.png"),pixels.EncodeToPNG());}
                finally {camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(target);}
            }
            actor.ResetEffects();
            if(body[0].transform.localScale!=initial || !body[0].enabled || actor.VirtualScore!=0)throw new Exception("Reset did not restore bug geometry and score");
            Debug.Log("BUG_IMPACT_VISUAL_PASS: four synthetic presentation states and Reset; no real gesture or device verification");
        } finally {EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);}
    }
    public static void RunPlantBloom() {
        string previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        string output=Path.GetFullPath("Reports/plant-bloom-visual-simulation");Directory.CreateDirectory(output);
        try {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Synthetic blossom camera").AddComponent<Camera>();camera.tag="MainCamera";
            camera.transform.position=new Vector3(.12f,.32f,-.85f);camera.transform.LookAt(new Vector3(0,.28f,0));camera.fieldOfView=45;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.14f,.18f);
            var actor=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Park/ParkPlant.prefab")).GetComponent<ParkActor>();
            actor.gameObject.SetActive(true);actor.virtualTarget=true;actor.Build();
            if(actor.label)actor.label.gameObject.SetActive(false); // Shared UI owns runtime status.
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            actor.growthTime=.8f;typeof(ParkActor).GetField("targetGrowth",flags).SetValue(actor,.8f);
            var stateField=typeof(ParkActor).GetField("state",flags);object state=stateField.GetValue(actor);state.GetType().GetField("bloom").SetValue(state,true);stateField.SetValue(actor,state);
            typeof(ParkActor).GetField("bloomVisibility",flags).SetValue(actor,1f);actor.SendMessage("Update");
            var target=new RenderTexture(1000,800,24);var pixels=new Texture2D(1000,800,TextureFormat.RGB24,false);var old=RenderTexture.active;
            try {camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1000,800),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(output,"Plant-bloom-SIMULATION.png"),pixels.EncodeToPNG());}
            finally {camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(target);}
            Debug.Log("PLANT_BLOOM_VISUAL_RENDERED: explicitly set editor state; no gesture or physical location validation");
        } finally {EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);}
    }
    public static void RunStatueScan() {
        string previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        string output=Path.GetFullPath("Reports/statue-scan-visual-simulation");Directory.CreateDirectory(output);
        try {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Synthetic statue camera").AddComponent<Camera>();camera.tag="MainCamera";
            camera.transform.position=new Vector3(.7f,.55f,-1);camera.transform.LookAt(new Vector3(0,.32f,0));camera.fieldOfView=45;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.14f,.18f);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Park/ParkStatue.prefab");
            var actor=UnityEngine.Object.Instantiate(prefab).GetComponent<ParkActor>();actor.gameObject.SetActive(true);actor.virtualTarget=true;actor.Build();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var progress=typeof(ParkActor).GetMethod("SetScanProgress",flags);
            foreach(float fraction in new[]{0f,.5f,.95f,1f}) {
                progress.Invoke(actor,new object[]{fraction});
                var target=new RenderTexture(1000,800,24);var pixels=new Texture2D(1000,800,TextureFormat.RGB24,false);var old=RenderTexture.active;
                try {camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1000,800),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(output,"Scan-"+Mathf.RoundToInt(fraction*100)+"-SIMULATION.png"),pixels.EncodeToPNG());}
                finally {camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(target);}
            }
            Debug.Log("STATUE_SCAN_VISUAL_RENDERED: four synthetic animation samples; no hand trajectory or PICO validation");
        } finally {EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);}
    }
    public static void RunPetDream() {
        string previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        string output=Path.GetFullPath("Reports/pet-dream-content-simulation");Directory.CreateDirectory(output);
        try {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Synthetic dream camera").AddComponent<Camera>();camera.tag="MainCamera";
            camera.transform.position=new Vector3(0,.4f,-1);camera.transform.LookAt(new Vector3(0,.4f,0));camera.fieldOfView=45;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.14f,.18f);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Park/ParkPet.prefab");
            foreach(string species in new[]{"Cat","Dog"}) {
            var actor=UnityEngine.Object.Instantiate(prefab).GetComponent<ParkActor>();actor.gameObject.SetActive(false);actor.species=species;actor.virtualTarget=true;actor.gameObject.SetActive(true);actor.Build();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var stateField=typeof(ParkActor).GetField("state",flags);
            for(int i=0;i<3;i++) {
                object state=stateField.GetValue(actor);var type=state.GetType();
                type.GetField("bubble").SetValue(state,true);type.GetField("style").SetValue(state,i);stateField.SetValue(actor,state);
                typeof(ParkActor).GetMethod("ApplyVisibility",flags).Invoke(actor,null);
                var target=new RenderTexture(1000,800,24);var pixels=new Texture2D(1000,800,TextureFormat.RGB24,false);var old=RenderTexture.active;
                try {camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1000,800),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(output,species+"-"+new[]{"Food","Star","Heart"}[i]+"-SIMULATION.png"),pixels.EncodeToPNG());}
                finally {camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(target);}
            }
            UnityEngine.Object.DestroyImmediate(actor.gameObject);
            }
            Debug.Log("PET_DREAM_CONTENT_RENDERED: six explicitly set synthetic states; no gesture or PICO validation");
        } finally {EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);}
    }
    static void Render(bool recorded)
    {
        string previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        string output=Path.GetFullPath(recorded?"Reports/shared-ui-recorded-image-simulation":"Reports/shared-ui-visual-simulation");Directory.CreateDirectory(output);
        try {
            foreach(ParkScenario scenario in Enum.GetValues(typeof(ParkScenario))) {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var camera=new GameObject("Simulation camera").AddComponent<Camera>();camera.tag="MainCamera";
                camera.backgroundColor=new Color(.12f,.14f,.18f);camera.clearFlags=CameraClearFlags.SolidColor;camera.nearClipPlane=.05f;
                var root=new GameObject("Offline UI simulation");var controller=root.AddComponent<ParkController>();
                controller.scenario=scenario;controller.simulation=true;controller.sharedInterface=true;
                controller.actorPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Park/"+ParkContentBuilder.Names[(int)scenario]+".prefab").GetComponent<ParkActor>();
                if(!recorded) {controller.SpawnVirtual();controller.selected.SetHighlighted(true);}
                var ui=root.AddComponent<ParkSharedInterface>();ui.controller=controller;ui.replayMode=true;
                ui.SendMessage("Start");ui.Selection.automatic=false;
                string frame=recorded?Resources.Load<TextAsset>("MagicMR/recorded-recognition-images.jsonl").text.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)[0]:"{\"status\":\"OK\",\"scene\":\"discovery\",\"frame_id\":1,\"width\":640,\"height\":480,\"detections\":[]}";
                ui.ApplyJson(frame,true);
                ui.SendMessage("Update");Canvas.ForceUpdateCanvases();
                var target=new RenderTexture(1600,1000,24);var pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false);
                var old=RenderTexture.active;
                try {
                    camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                    pixels.ReadPixels(new Rect(0,0,1600,1000),0,0);pixels.Apply();
                    File.WriteAllBytes(Path.Combine(output,scenario+"-SIMULATION.png"),pixels.EncodeToPNG());
                } finally {
                    camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(target);
                }
            }
            Debug.Log("PARK_VISUAL_SIMULATION_RENDERED: six synthetic editor fixtures; visual review pending; no device validation");
        } finally {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);
        }
    }
}
