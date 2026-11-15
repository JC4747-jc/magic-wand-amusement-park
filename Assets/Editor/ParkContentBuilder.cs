using System;
using System.IO;
using System.Linq;
using MagicMR;
using Perception;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ParkContentBuilder
{
    public static readonly string[] Names={"ParkTree","ParkBugs","ParkStatue","ParkRoom","ParkPlant","ParkPet"};
    const string Content="Assets/MagicMR/Park";
    public static void AttachSharedInterfaceToActiveScene()
    {
        var oldEntry=UnityEngine.Object.FindFirstObjectByType<ParkLobby>();
        if(oldEntry)oldEntry.enabled=false;
        var shared=UnityEngine.Object.FindFirstObjectByType<ParkSharedInterface>();
        if(!shared)shared=new GameObject("Shared Scene Interface").AddComponent<ParkSharedInterface>();
        shared.enabled=true;
        shared.transport=UnityEngine.Object.FindFirstObjectByType<Perception.TcpClient>();
        shared.replay=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/MagicMR/recognition-replay.jsonl.bytes");
        EditorUtility.SetDirty(shared);
    }
    public static void ConfigureLighterEntry()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/BridgeStereoFusion.unity");
        AttachSharedInterfaceToActiveScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        var scenes=EditorBuildSettings.scenes.ToList();
        foreach(var name in Names)
        {
            string path="Assets/Scenes/"+name+".unity";
            int index=scenes.FindIndex(s=>s.path==path);
            if(index<0)scenes.Add(new EditorBuildSettingsScene(path,true));
            else scenes[index]=new EditorBuildSettingsScene(path,true);
        }
        scenes.RemoveAll(s=>s.path=="Assets/Scenes/BridgeStereoFusion.unity");
        scenes.Insert(0,new EditorBuildSettingsScene("Assets/Scenes/BridgeStereoFusion.unity",true));
        EditorBuildSettings.scenes=scenes.ToArray();
        ParkSharedInteractionChecks.CheckDefaultBuildEntry();
        AssetDatabase.SaveAssets();
        ParkSharedInteractionChecks.Run();
    }
    [MenuItem("MagicMR/Park/Generate six scenes")]
    public static void GenerateAll()
    {
        Directory.CreateDirectory(Content); AssetDatabase.Refresh();
        var shader=Shader.Find("MagicMR/ParkSurface"); if(!shader)throw new Exception("ParkSurface shader missing");
        var material=AssetDatabase.LoadAssetAtPath<Material>(Content+"/ParkShared.mat");
        if(!material) { material=new Material(shader); AssetDatabase.CreateAsset(material,Content+"/ParkShared.mat"); }
        EditorSceneManager.OpenScene("Assets/Scenes/BridgeStereoFusion.unity");
        AttachSharedInterfaceToActiveScene();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        for(int i=0;i<Names.Length;i++)
        {
            // Generated from the current main lighter scene, never overwrite BridgeTest.
            EditorSceneManager.OpenScene("Assets/Scenes/BridgeStereoFusion.unity");
            foreach(var b in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None)) {
                var ns=b.GetType().Namespace;
                if(ns=="MagicMR" && !(b is PicoVstKeeper) && !(b is PicoVideoSeeThrough) && !(b is HandVisualSuppressor)) b.enabled=false;
                if(ns=="Perception" && !(b is Perception.TcpClient) && !(b is PicoYoloFrameSender))b.enabled=false;
            }
            foreach(var canvas in UnityEngine.Object.FindObjectsByType<UnityEngine.Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None))canvas.gameObject.SetActive(false);
            var lighter=GameObject.Find("Lighter"); if(lighter)lighter.SetActive(false);
            var root=new GameObject("Park Scene Controller"); var controller=root.AddComponent<ParkController>(); controller.scenario=(ParkScenario)i;
            controller.sharedInterface=true;
            controller.simulation=false;
            var template=new GameObject(Names[i]+"VirtualActor"); var actor=template.AddComponent<ParkActor>(); actor.scenario=(ParkScenario)i; actor.sharedMaterial=material;
            actor.interactionRadius=i==0?.9f:.65f; actor.environmentRadius=.6f;
            actor.species=i==1?"Cockroach":i==5?"Cat":Names[i]; template.SetActive(false);
            var prefab=PrefabUtility.SaveAsPrefabAsset(template,Content+"/"+Names[i]+".prefab"); UnityEngine.Object.DestroyImmediate(template);
            controller.actorPrefab=prefab.GetComponent<ParkActor>();
            var receiver=root.AddComponent<ParkStereoReceiver>(); receiver.controller=controller; controller.receiver=receiver;
            receiver.sender=UnityEngine.Object.FindFirstObjectByType<PicoYoloFrameSender>(); receiver.transport=UnityEngine.Object.FindFirstObjectByType<Perception.TcpClient>();
            var ui=root.AddComponent<ParkSharedInterface>();ui.controller=controller;ui.receiver=receiver;ui.transport=receiver.transport;
            ui.replay=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/MagicMR/recognition-replay.jsonl.bytes");
            var capture=UnityEngine.Object.FindFirstObjectByType<PicoStereoCapture>(); if(!capture)throw new Exception("PICO capture missing from source scene"); capture.locator=null; capture.parkReceiver=receiver;
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),"Assets/Scenes/"+Names[i]+".unity");
        }
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/BridgeStereoFusion.unity",true)}.Concat(Names.Select(n=>new EditorBuildSettingsScene("Assets/Scenes/"+n+".unity",true))).ToArray();
        var registry=Names.Select(n=>AssetDatabase.LoadAssetAtPath<GameObject>(Content+"/"+n+".prefab").GetComponent<ParkActor>()).ToArray();
        foreach(var name in Names) {
            EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity");
            var controller=UnityEngine.Object.FindFirstObjectByType<ParkController>();
            controller.registeredActors=registry;
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }
        AssetDatabase.SaveAssets(); EditorSceneManager.OpenScene("Assets/Scenes/BridgeStereoFusion.unity");
        Debug.Log("PARK_CONTENT_GENERATED: 6 scenes, 6 prefabs, shared material; lighter source retained");
    }
    [MenuItem("MagicMR/Park/Open virtual test")]
    public static void OpenVirtual() {
        EditorSceneManager.OpenScene("Assets/Scenes/ParkTree.unity");
        // An explicit editor preview choice; generated Android scenes remain real.
        var controller=UnityEngine.Object.FindFirstObjectByType<ParkController>();
        if(controller)controller.simulation=true;
    }
    public static void ValidateAndGenerate() {
        GenerateAll();ParkRegressionChecks.Run();ParkGestureSafetyChecks.Run();SharedInterfaceChecks.Run();
    }
}
