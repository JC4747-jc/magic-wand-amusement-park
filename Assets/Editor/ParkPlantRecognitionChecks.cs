using System;
using System.IO;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ParkPlantRecognitionChecks {
    public static void Run() {RunFile("Reports/plant-sample-actual-inference-replies.jsonl",1);}
    public static void RunNegativeV4() {RunFile("Reports/plant-negative-v4-actual-inference-replies.jsonl",2);}
    static void RunFile(string replyPath,int expectedCandidates) {
        string previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        try {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("Explicit local-image replay camera").AddComponent<Camera>().tag="MainCamera";
            var root=new GameObject("Actual inference reply / synthetic location fixture");
            var controller=root.AddComponent<ParkController>();controller.simulation=true;controller.scenario=ParkScenario.Tree;
            controller.registeredActors=new ParkActor[6];
            for(int i=0;i<6;i++)controller.registeredActors[i]=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Park/"+ParkContentBuilder.Names[i]+".prefab").GetComponent<ParkActor>();
            controller.actorPrefab=controller.registeredActors[0];
            var ui=root.AddComponent<ParkSharedInterface>();ui.controller=controller;ui.replayMode=true;ui.SendMessage("Start");
            string json=File.ReadAllLines(replyPath)[0];
            ui.ApplyJson(json,true);
            if(controller.scenario!=ParkScenario.Tree)throw new Exception("Pending model automatically switched content");
            if(ui.Selection.Candidates.Count!=expectedCandidates || ui.Selection.Candidates[0].target_scene!="plant")throw new Exception("Actual plant TCP reply missing from replay candidates");
            if(!ui.CurrentFrame.model_validation.Contains("PENDING"))throw new Exception("Model validation marker was lost");
            ui.Choose(ui.Selection.Candidates[0].instance_id);
            if(controller.scenario!=ParkScenario.Plant || !controller.selected || !controller.selected.virtualTarget || !controller.simulation)throw new Exception("Manual replay selection did not create explicit virtual plant preview");
            ui.Command("Reset");
            if(controller.selected || controller.recognitionLock!=null)throw new Exception("Reset retained preview selection");
            Debug.Log("PLANT_ACTUAL_REPLY_ROUTING_PASS: real public-image inference/TCP reply -> candidate -> manual plant selection -> explicit virtual preview -> Reset; pending auto gate preserved; no stereo/PICO acceptance");
        } finally {EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);}
    }
}
