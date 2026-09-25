using System.IO;
using UnityEditor;
using UnityEngine;

// Allows a local build request to run in the already-open editor without
// closing the user's project or starting a second editor on the same files.
[InitializeOnLoad]
public static class TabletopBuildRequest
{
    const string Request = ".codex-tmp/build-tabletop.request";
    static TabletopBuildRequest() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer || !File.Exists(Request)) return;
        File.Delete(Request);
        try
        {
            BridgeStereoBuild.Build();
            File.WriteAllText(".codex-tmp/build-tabletop.result", "SUCCESS");
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            File.WriteAllText(".codex-tmp/build-tabletop.result", e.ToString());
        }
    }
}
