using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Compilation/packing artifact. Keeps the normal scene list and app IDs intact;
// it does not add a production navigation flow or claim device compatibility.
public static class AllScenesValidationBuild
{
    public static void Build()
    {
        ProjectSceneAudit.Run();StereoIntegrationChecks.Run();F2GPoseChecks.Run();
        const string first="Assets/Scenes/BridgeStereoFusion.unity";
        var scenes=new[]{first}.Concat(AssetDatabase.FindAssets("t:Scene",new[]{"Assets/Scenes"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p!=first).OrderBy(p=>p)).ToArray();
        if(scenes.Length!=14||scenes.Distinct().Count()!=14)throw new Exception("Expected all 14 unique project scenes");
        string id=PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),product=PlayerSettings.productName,version=PlayerSettings.bundleVersion;
        int code=PlayerSettings.Android.bundleVersionCode;bool signing=PlayerSettings.Android.useCustomKeystore;
        try
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.yn.picmagicmr.validation");PlayerSettings.productName="Magic MR All Scenes Validation";
            PlayerSettings.bundleVersion="0.1.0";PlayerSettings.Android.bundleVersionCode=1;PlayerSettings.Android.useCustomKeystore=false;
            Directory.CreateDirectory("Builds/Android");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=scenes,target=BuildTarget.Android,locationPathName="Builds/Android/AllScenesValidation.apk",options=BuildOptions.None});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("All-scenes validation build failed");
            ResourceBoundedAndroidBuild.RecordAssets(report);
            Debug.Log("ALL_SCENES_ANDROID_BUILD_PASS: all 14 project scenes compiled/packed; separate validation app ID, no device/navigation acceptance");
        }
        finally
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,id);PlayerSettings.productName=product;PlayerSettings.bundleVersion=version;
            PlayerSettings.Android.bundleVersionCode=code;PlayerSettings.Android.useCustomKeystore=signing;
        }
    }
}
