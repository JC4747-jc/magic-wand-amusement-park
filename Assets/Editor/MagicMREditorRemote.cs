#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MagicMR.Editor
{
    /// <summary>
    /// File/menu remote so an agent can leave the URP empty template and Play MagicMR.
    /// </summary>
    [InitializeOnLoad]
    public static class MagicMREditorRemote
    {
        const string ScenePath = "Assets/Scenes/MagicMR.unity";
        const string CmdPath = "Temp/MagicMREditorCommand.txt";
        const string VerifyFlag = "Temp/MagicMRVerifyKeys.flag";

        static string s_Pending;

        static MagicMREditorRemote()
        {
            EditorApplication.delayCall += ProcessCommandFile;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        [MenuItem("Magic Wand/Open MagicMR Scene And Play")]
        public static void OpenMagicMrAndPlay()
        {
            OpenAndPlay(verifyKeys: false);
        }

        [MenuItem("Magic Wand/Verify Editor Keys 1-4")]
        public static void OpenAndVerify()
        {
            OpenAndPlay(verifyKeys: true);
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode || string.IsNullOrEmpty(s_Pending))
                return;

            var pending = s_Pending;
            s_Pending = null;
            EditorApplication.delayCall += () => OpenAndPlay(pending.Contains("verify"));
        }

        static void ProcessCommandFile()
        {
            if (!File.Exists(CmdPath))
                return;

            var cmd = File.ReadAllText(CmdPath).Trim();
            File.Delete(CmdPath);
            if (string.IsNullOrEmpty(cmd))
                return;

            if (EditorApplication.isPlaying)
            {
                s_Pending = cmd;
                EditorApplication.isPlaying = false;
                return;
            }

            OpenAndPlay(cmd.Contains("verify"));
        }

        static void OpenAndPlay(bool verifyKeys)
        {
            if (EditorApplication.isPlaying)
            {
                s_Pending = verifyKeys ? "open-play-verify" : "open-play";
                EditorApplication.isPlaying = false;
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath);

            Directory.CreateDirectory("Temp");
            if (verifyKeys)
                File.WriteAllText(VerifyFlag, "1");
            else if (File.Exists(VerifyFlag))
                File.Delete(VerifyFlag);

            EditorApplication.isPlaying = true;
            Debug.Log("[MagicMR] Editor remote: playing Assets/Scenes/MagicMR.unity");
        }
    }
}
#endif
