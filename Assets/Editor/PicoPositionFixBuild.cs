using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class PicoPositionFixBuild
{
    public static void Run()
    {
        StereoIntegrationChecks.Run();
        RightHandSpellVfxChecks.Run();
        var target = NamedBuildTarget.Android;
        string id = PlayerSettings.GetApplicationIdentifier(target);
        string name = PlayerSettings.productName;
        string version = PlayerSettings.bundleVersion;
        int code = PlayerSettings.Android.bundleVersionCode;
        bool signing = PlayerSettings.Android.useCustomKeystore;
        try
        {
            PlayerSettings.SetApplicationIdentifier(target, "com.yn.picmagicmr.stereo.test20261112");
            PlayerSettings.productName = "Magic MR Box Debug";
            PlayerSettings.bundleVersion = "1.10.4-follow.20261127";
            PlayerSettings.Android.bundleVersionCode = 27;
            PlayerSettings.Android.useCustomKeystore = false;
            Directory.CreateDirectory("Builds/Android");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Scenes/BridgeStereoFusion.unity" },
                target = BuildTarget.Android,
                locationPathName = "Builds/Android/MagicMR-Follow-20261127.apk",
                options = BuildOptions.Development
            });
            Debug.Log("PICO_CURRENT_TEST_BUILD=" + report.summary.result);
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("PICO test build failed");
        }
        finally
        {
            PlayerSettings.SetApplicationIdentifier(target, id);
            PlayerSettings.productName = name;
            PlayerSettings.bundleVersion = version;
            PlayerSettings.Android.bundleVersionCode = code;
            PlayerSettings.Android.useCustomKeystore = signing;
            AssetDatabase.SaveAssets();
        }
    }
}



