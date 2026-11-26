using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
public static class PerformanceAblationBuild
{
    public static void Run()
    {
        var target = NamedBuildTarget.Android;
        var id = PlayerSettings.GetApplicationIdentifier(target);
        var name = PlayerSettings.productName;
        bool signing = PlayerSettings.Android.useCustomKeystore;
        try
        {
            PlayerSettings.SetApplicationIdentifier(target, "com.yn.picmagicmr.ablation");
            PlayerSettings.productName = "Magic MR Ablation";
            PlayerSettings.Android.useCustomKeystore = false;
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Scenes/BridgeStereoFusion.unity" },
                target = BuildTarget.Android,
                locationPathName = "Builds/Android/MagicMR-Ablation.apk",
                extraScriptingDefines = new[] { "PERFORMANCE_ABLATION" },
                options = BuildOptions.Development
            });
            if (result.summary.result != BuildResult.Succeeded) throw new Exception("Ablation build failed");
        }
        finally
        {
            PlayerSettings.SetApplicationIdentifier(target, id);
            PlayerSettings.productName = name;
            PlayerSettings.Android.useCustomKeystore = signing;
            AssetDatabase.SaveAssets();
        }
    }
}
