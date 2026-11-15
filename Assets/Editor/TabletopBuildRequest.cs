using System.IO;
using UnityEditor;
using UnityEngine;

// Allows a local build request to run in the already-open editor without
// closing the user's project or starting a second editor on the same files.
[InitializeOnLoad]
public static class TabletopBuildRequest
{
    const string Request = ".codex-tmp/build-tabletop.request";
    const string PoseRequest = ".codex-tmp/build-pose.request";
    const string RepairRequest = ".codex-tmp/build-repair.request";
    static TabletopBuildRequest() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
        bool pose = File.Exists(PoseRequest);
        bool repair = File.Exists(RepairRequest);
        if (!repair && !pose && !File.Exists(Request)) return;
        if (UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().isDirty) return;
        File.Delete(repair ? RepairRequest : pose ? PoseRequest : Request);
        string result = repair ? ".codex-tmp/build-repair.result" : pose ? ".codex-tmp/build-pose.result" : ".codex-tmp/build-tabletop.result";
        try
        {
            if (repair) BridgeStereoBuild.BuildRepair(); else if (pose) BridgeStereoBuild.BuildPose(); else BridgeStereoBuild.Build();
            File.WriteAllText(result, "SUCCESS");
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            File.WriteAllText(result, e.ToString());
        }
    }
}
