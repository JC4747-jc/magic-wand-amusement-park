using UnityEditor;
using UnityEngine;

/// <summary>Bound project import concurrency without terminating active imports or touching assets.</summary>
[InitializeOnLoad]
public static class ResourceBoundedEditorImport
{
    const string Previous="MagicMR.ImportWorkers.Previous";
    const string OptOut="MagicMR.ImportWorkers.SessionOptOut";
    static ResourceBoundedEditorImport()
    {
        if(SessionState.GetBool(OptOut,false))return;
        Limit();
    }

    [MenuItem("MagicMR/Resources/Use one import worker")]
    public static void Limit()
    {
        SessionState.SetBool(OptOut,false);
        if(SessionState.GetInt(Previous,-1)<0)
            SessionState.SetInt(Previous,AssetDatabase.DesiredWorkerCount);
        AssetDatabase.DesiredWorkerCount=1;
        EditorApplication.update-=RetireIdleWorkers;
        EditorApplication.update+=RetireIdleWorkers;
    }

    static void RetireIdleWorkers()
    {
        if(EditorApplication.isUpdating||EditorApplication.isCompiling||
           EditorApplication.isPlayingOrWillChangePlaymode||BuildPipeline.isBuildingPlayer)return;
        EditorApplication.update-=RetireIdleWorkers;
        // Unity's API only retires idle workers; never kill the user's editor/import processes.
        AssetDatabase.ForceToDesiredWorkerCount();
        Debug.Log("[MagicMR] Asset imports limited to one worker; idle workers retired by Unity.");
    }

    [MenuItem("MagicMR/Resources/Restore import workers for this session")]
    public static void Restore()
    {
        EditorApplication.update-=RetireIdleWorkers;
        SessionState.SetBool(OptOut,true);
        int previous=SessionState.GetInt(Previous,-1);
        if(previous>=0)AssetDatabase.DesiredWorkerCount=previous;
        // Let Unity grow the pool on demand instead of immediately spawning more processes.
    }
}
