using System;
using System.IO;
using System.Linq;
using Perception;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MagicMR;

public static class LegacySceneMaintenance
{
    public static void Run()
    {
        string previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        try
        {
            foreach(var name in new[]{"BridgeTest","MagicMR"})
            {
                var scene=EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity",OpenSceneMode.Single);
                var lighter=GameObject.Find("Lighter");if(!lighter)throw new Exception("Legacy lighter missing: "+name);
                var position=lighter.transform.position;var rotation=lighter.transform.rotation;var scale=lighter.transform.localScale;
                if((scale-new Vector3(.025f,.08f,.012f)).sqrMagnitude>1e-10f)throw new Exception("Unexpected legacy nominal scale; refusing mesh replacement: "+name+" "+scale);
                LighterModelBuilder.Apply(lighter);
                if(lighter.transform.position!=position||lighter.transform.rotation!=rotation||lighter.transform.localScale!=scale)throw new Exception("Lighter model conversion moved legacy anchor");
                foreach(var tcp in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Perception.TcpClient>(true)))
                {
                    // Only migrate the inherited old-machine address. Keep any
                    // user-specific host that is different from that default.
                    if(tcp.Host!="192.168.1.5")continue;
                    var serialized=new SerializedObject(tcp);serialized.FindProperty("m_Host").stringValue="127.0.0.1";serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                Debug.Log("LEGACY_SCENE_MODEL_UPGRADE: "+name+" rounded lighter/metal details installed, root pose/nominal scale preserved");
            }
            var vstScene=EditorSceneManager.OpenScene("Assets/Scenes/VstTest.unity",OpenSceneMode.Single);
            var oldVst=UnityEngine.Object.FindFirstObjectByType<SeeThoughttest>();
            var vst=UnityEngine.Object.FindFirstObjectByType<VstTestRunner>();
            if(!vst)
            {
                var host=oldVst?oldVst.gameObject:new GameObject("VST isolation runner");
                vst=host.AddComponent<VstTestRunner>();
            }
            if(oldVst)oldVst.enabled=false;
            EditorSceneManager.MarkSceneDirty(vstScene);EditorSceneManager.SaveScene(vstScene);
            Debug.Log("LEGACY_VST_ENTRY_UPGRADE: existing guarded retry/configuration runner installed; duplicate one-shot component disabled");
            AssetDatabase.SaveAssets();
        }
        finally{if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);}
    }
}
