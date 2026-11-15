using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ProjectSceneAudit
{
    public static void RunWithTimeline()
    {
        Run();ParkNaturalModelChecks.Render();ParkAnimationTimelineChecks.Run();
    }
    public static void RunCaptureValidation()
    {
        Run();ParkSharedInteractionChecks.Run();StereoIntegrationChecks.Run();F2GPoseChecks.Run();
        CaptureDemoTimelineChecks.Run();
    }
    [Serializable] class SceneEntry
    {
        public string scene;
        public int objects,missingScripts,unassignedMaterials,renderers,particles;
        public long serializedMeshTriangles;
        public string[] animationAndInteractionComponents;
        public string scope="Serialized scene inspection only; runtime-generated content and headset behavior require separate checks";
    }
    [Serializable] class Report {public SceneEntry[] scenes;}
    public static bool IsProjectBehaviour(MonoBehaviour behaviour)
    {
        if(!behaviour)return false;
        var script=MonoScript.FromMonoBehaviour(behaviour);
        return script&&AssetDatabase.GetAssetPath(script).StartsWith("Assets/",StringComparison.Ordinal);
    }
    public static void Run()
    {
        string previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        var results=new List<SceneEntry>();
        try
        {
            var paths=AssetDatabase.FindAssets("t:Scene",new[]{"Assets/Scenes"}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p).ToArray();
            // Candidate scenes may coexist with the integrated set. Check required
            // identities rather than rejecting extra scenes or accepting a count
            // that could conceal a missing required scene.
            var required=new[]{"BridgeTest","BridgeStereoFusion","BridgeStereoPoseFusion","BridgeStereoRepair","MagicMR","VstTest","SampleScene","DesktopCaptureDemo"}
                .Concat(ParkContentBuilder.Names).Select(n=>"Assets/Scenes/"+n+".unity");
            var missing=required.Except(paths).ToArray();
            if(missing.Length>0)throw new Exception("Missing integrated project scenes: "+string.Join(", ",missing));
            foreach(var path in paths)
            {
                var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                var objects=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Select(t=>t.gameObject).ToArray();
                var renderers=objects.SelectMany(g=>g.GetComponents<Renderer>()).ToArray();
                var entry=new SceneEntry {scene=path,objects=objects.Length,renderers=renderers.Length,
                    missingScripts=objects.Sum(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount),
                    unassignedMaterials=renderers.Sum(r=>r.sharedMaterials.Count(m=>!m)),
                    particles=objects.Sum(g=>g.GetComponents<ParticleSystem>().Length),
                    animationAndInteractionComponents=objects.SelectMany(g=>g.GetComponents<MonoBehaviour>()).Where(IsProjectBehaviour)
                        .Select(b=>b.GetType().FullName).Distinct().OrderBy(n=>n).ToArray()};
                foreach(var mesh in objects.SelectMany(g=>g.GetComponents<MeshFilter>()).Select(f=>f.sharedMesh).Where(m=>m))
                    for(int i=0;i<mesh.subMeshCount;i++)if(mesh.GetTopology(i)==MeshTopology.Triangles)entry.serializedMeshTriangles+=mesh.GetIndexCount(i)/3;
                results.Add(entry);
            }
            Directory.CreateDirectory(".codex-tmp/scene-audit");
            File.WriteAllText(".codex-tmp/scene-audit/scenes.json",JsonUtility.ToJson(new Report {scenes=results.ToArray()},true));
            if(results.Any(r=>r.missingScripts>0))throw new Exception("Missing scripts found; inspect scene audit report");
            Debug.Log("PROJECT_SCENE_AUDIT_PASS: "+results.Count+" scenes inspected, all 14 required scenes present, no missing scripts; unassigned material slots="+results.Sum(r=>r.unassignedMaterials)+"; runtime animation checks recorded separately");
        }
        finally {if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);}
    }
}
