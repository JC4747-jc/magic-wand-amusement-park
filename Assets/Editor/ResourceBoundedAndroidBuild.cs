using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build.Reporting;
using System.Security.Cryptography;
using UnityEngine;

// Opt-in entry point for 16 GB development machines; restore project settings afterwards.
public sealed class ResourceBoundedAndroidBuild : IPostGenerateGradleAndroidProject
{
    static bool active;
    [Serializable] sealed class AssetManifest {public string apk,sha256;public string[] sourceAssets;}
    public static void RecordAssets(BuildReport report)
    {
        if(!active||report.summary.result!=BuildResult.Succeeded)return;
        if(report.packedAssets.Length==0)throw new Exception("BuildReport has no packed-asset provenance; repack player content before acceptance");
        var assets=report.packedAssets.SelectMany(p=>p.contents).Select(p=>p.sourceAssetPath).Where(p=>!string.IsNullOrEmpty(p)).Distinct().OrderBy(p=>p).ToArray();
        var required=new[]{"Assets/ThirdParty/NaturalTrees/SmallTree/SmallTree.fbx","Assets/MagicMR/NaturalTreeLeaves.shader","Assets/ThirdParty/NaturalTrees/SmallTree/leaves_alpha.jpg",
            "Assets/ThirdParty/NaturalPlants/Haworthia/Haworthia.fbx","Assets/ThirdParty/NaturalDecor/Statue/MarbleBust.fbx",
            "Assets/Resources/MagicMR/NaturalMaterials/Wood.mat","Assets/Resources/MagicMR/NaturalMaterials/Linen.mat","Assets/Resources/MagicMR/NaturalMaterials/Plaster.mat",
            "Assets/MagicMR/NaturalInsects/Cockroach.fbx","Assets/MagicMR/NaturalInsects/Spider.fbx","Assets/MagicMR/NaturalCuticle.shader",
            "Assets/ThirdParty/NaturalPets/BicolorCat/BicolorCat.fbx","Assets/ThirdParty/NaturalPets/BicolorCat/Image_0.png","Assets/ThirdParty/NaturalPets/BicolorCat/BicolorCat_UV0_Normal.png",
            "Assets/ThirdParty/NaturalPets/Wolfhound/Wolfhound.fbx","Assets/ThirdParty/NaturalPets/Wolfhound/Wolfhound_Albedo.png",
            "Assets/ThirdParty/NaturalDecor/ReadingChair/ReadingChair.fbx","Assets/ThirdParty/NaturalDecor/CeramicVase/CeramicVase.fbx",
            "Assets/ThirdParty/NaturalDecor/ReadingChair/modern_arm_chair_01_legs_diff_1k.jpg","Assets/ThirdParty/NaturalDecor/ReadingChair/modern_arm_chair_01_legs_nor_gl_1k.jpg","Assets/ThirdParty/NaturalDecor/ReadingChair/modern_arm_chair_01_legs_URP_MetalAO_Smooth.png",
            "Assets/ThirdParty/NaturalDecor/ReadingChair/modern_arm_chair_01_pillow_diff_1k.jpg","Assets/ThirdParty/NaturalDecor/ReadingChair/modern_arm_chair_01_pillow_nor_gl_1k.jpg","Assets/ThirdParty/NaturalDecor/ReadingChair/modern_arm_chair_01_pillow_URP_MetalAO_Smooth.png",
            "Assets/ThirdParty/NaturalDecor/CeramicVase/ceramic_vase_01_diff_1k.jpg","Assets/ThirdParty/NaturalDecor/CeramicVase/ceramic_vase_01_nor_gl_1k.jpg","Assets/ThirdParty/NaturalDecor/CeramicVase/ceramic_vase_01_URP_MetalAO_Smooth.png",
            "Assets/Resources/MagicMR/NaturalMaterials/RoomMetal.mat","Assets/Resources/MagicMR/NaturalMaterials/BookCloth.mat","Assets/Resources/MagicMR/NaturalMaterials/BookPaper.mat",
            "Assets/MagicMR/NaturalJellyfish/Jellyfish.mesh.asset","Assets/MagicMR/NaturalJellyfish.shader",
            "Assets/MagicMR/NaturalTreeScar/Scar.mesh.asset","Assets/MagicMR/NaturalTreeScar/Scar.mat","Assets/MagicMR/NaturalBarkMark.shader",
            "Assets/ThirdParty/NaturalDecor/BronzeBeast/BronzeBeast.fbx","Assets/ThirdParty/NaturalDecor/BronzeBeast/Bronze.mat",
            "Assets/ThirdParty/NaturalDecor/BronzeBeast/bull_head_diff_1k.jpg","Assets/ThirdParty/NaturalDecor/BronzeBeast/bull_head_nor_gl_1k.jpg","Assets/ThirdParty/NaturalDecor/BronzeBeast/Bronze_MetalAO_Smooth.png"};
        foreach(var path in required)if(!assets.Contains(path))throw new Exception("Accepted natural asset absent from BuildReport: "+path);
        if(assets.Any(p=>p.StartsWith("Assets/ThirdParty/NaturalPets/",StringComparison.Ordinal)&&!p.StartsWith("Assets/ThirdParty/NaturalPets/BicolorCat/",StringComparison.Ordinal)&&!p.StartsWith("Assets/ThirdParty/NaturalPets/Wolfhound/",StringComparison.Ordinal)))throw new Exception("Unaccepted pet candidate entered player build");
        if(assets.Contains("Assets/ThirdParty/NaturalPets/BicolorCat/Image_2.png"))throw new Exception("UV2 source normal entered UV0 player material");
        if(assets.Any(p=>(p.StartsWith("Assets/ThirdParty/NaturalDecor/ReadingChair/",StringComparison.Ordinal)||p.StartsWith("Assets/ThirdParty/NaturalDecor/CeramicVase/",StringComparison.Ordinal)||p.StartsWith("Assets/ThirdParty/NaturalDecor/BronzeBeast/",StringComparison.Ordinal))&&p.EndsWith("_arm_1k.jpg",StringComparison.Ordinal)))throw new Exception("Unused original ARM map entered player instead of converted URP data");
        string apk=report.summary.outputPath;string hash;using(var stream=File.OpenRead(apk))using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        Directory.CreateDirectory(".codex-tmp");File.WriteAllText(".codex-tmp/android-assets-"+Path.GetFileNameWithoutExtension(apk)+".json",JsonUtility.ToJson(new AssetManifest{apk=Path.GetFileName(apk),sha256=hash,sourceAssets=assets},true));
        Debug.Log("ANDROID_PACKED_NATURAL_ASSETS_PASS: "+required.Length+" required source assets verified in BuildReport; rejected pet/UV2 map absent; APK SHA-256 bound to source manifest");
    }
    public int callbackOrder=>10000;
    public void OnPostGenerateGradleAndroidProject(string path)
    {
        if(!active)return;
        string properties=Path.GetFullPath(Path.Combine(path,"..","gradle.properties"));
        if(!File.Exists(properties))throw new Exception("Generated Gradle properties missing");
        var keys=new[]{"org.gradle.jvmargs=","org.gradle.parallel=","org.gradle.workers.max=","org.gradle.daemon=","org.gradle.logging.stacktrace="};
        var lines=File.ReadAllLines(properties).Where(line=>!keys.Any(key=>line.StartsWith(key,StringComparison.Ordinal))).ToList();
        lines.AddRange(new[]{"org.gradle.jvmargs=-Xmx2048M -XX:MaxMetaspaceSize=512M -XX:ActiveProcessorCount=2 -Djava.util.concurrent.ForkJoinPool.common.parallelism=1","org.gradle.parallel=false","org.gradle.workers.max=1","org.gradle.daemon=false","org.gradle.logging.stacktrace=all"});
        File.WriteAllLines(properties,lines);
        // All three app IDs reuse this generated Gradle project. Avoid patching
        // its previous APK: packageDebug has twice hit OOM in that incremental
        // path, while standalone/fresh packaging succeeds at the same heap cap.
        // Remove only this disposable output, keeping compiled code and caches.
        string gradleRoot=Path.GetDirectoryName(properties);
        string expectedRoot=Path.GetFullPath("Library/Bee/Android/Prj/IL2CPP/Gradle");
        if(!string.Equals(gradleRoot,expectedRoot,StringComparison.OrdinalIgnoreCase))throw new Exception("Unexpected generated Gradle directory; refusing package cleanup: "+gradleRoot);
        foreach(string mode in new[]{"debug","release"})
        {
            string generatedApk=Path.Combine(gradleRoot,"launcher","build","outputs","apk",mode,"launcher-"+mode+".apk");
            if(File.Exists(generatedApk)){File.Delete(generatedApk);Debug.Log("ANDROID_FRESH_PACKAGE_OUTPUT: removed only Library launcher-"+mode+".apk; compiled code/caches and Builds artifacts retained");}
        }
        string rootScript=Path.Combine(gradleRoot,"build.gradle");
        const string marker="// MagicMR verified JVM resource guard";
        if(!File.ReadAllText(rootScript).Contains(marker))File.AppendAllText(rootScript,@"
// MagicMR verified JVM resource guard
gradle.projectsEvaluated {
    def runtime = Runtime.getRuntime()
    def meta = java.lang.management.ManagementFactory.memoryPoolMXBeans.find { it.name == 'Metaspace' }.usage.max
    def pool = java.util.concurrent.ForkJoinPool.getCommonPoolParallelism()
    println ""ANDROID_JVM_ACTUAL_LIMITS: cpus=${runtime.availableProcessors()} common_pool=${pool} heap=${runtime.maxMemory()} metaspace=${meta}""
    if (runtime.availableProcessors() != 2 || pool != 1 || runtime.maxMemory() > 2147483648L || meta != 536870912L) {
        throw new GradleException('MagicMR bounded JVM limits missing; use Tools/build_android_bounded.ps1')
    }
}
");
        Debug.Log("ANDROID_RESOURCE_LIMITS: Gradle heap 2048 MiB, metaspace 512 MiB, worker 1, Java CPUs 2, common ForkJoin pool 1, no daemon");
    }
    public static void Run()=>Build(BridgeStereoBuild.Build,true);
    public static void RunPose()=>Build(BridgeStereoBuild.BuildPose,false);
    public static void RunRepair()=>Build(BridgeStereoBuild.BuildRepair,false);
    public static void RunAllScenes()=>Build(AllScenesValidationBuild.Build,false);
    public static void ReleaseEditorBuildMemory()
    {
        if(!active||Environment.GetEnvironmentVariable("MAGICMR_BUILD_JOBS")!="1")return;
        long before=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
        EditorUtility.UnloadUnusedAssetsImmediate();
        GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        Debug.Log("ANDROID_EDITOR_CACHE_RELEASE: allocated_bytes_before="+before+" after="+UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()+"; unused assets and managed garbage only, not disk cache deletion");
    }
    static void Build(Action build,bool checkModelReferences)
    {
        string previous=PlayerSettings.GetAdditionalIl2CppArgs();
        int previousHeap=AndroidExternalToolsSettings.maxJvmHeapSize;
        string previousSdk=AndroidExternalToolsSettings.sdkRootPath,previousNdk=AndroidExternalToolsSettings.ndkRootPath,previousJdk=AndroidExternalToolsSettings.jdkRootPath;
        if(previous.Contains("--jobs"))throw new Exception("Existing IL2CPP jobs argument requires explicit reconciliation");
        int jobs=2;string requestedJobs=Environment.GetEnvironmentVariable("MAGICMR_BUILD_JOBS");
        if(!string.IsNullOrEmpty(requestedJobs)&&(!int.TryParse(requestedJobs,out jobs)||jobs<1||jobs>2))
            throw new Exception("MAGICMR_BUILD_JOBS must be 1 or 2");
        try
        {
            active=true;PlayerSettings.SetAdditionalIl2CppArgs((previous+" --jobs="+jobs).Trim());
            Debug.Log("ANDROID_IL2CPP_JOBS: "+jobs);
            string sharedToolchain=Environment.GetEnvironmentVariable("MAGICMR_ANDROID_TOOLCHAIN");
            if(!string.IsNullOrEmpty(sharedToolchain)) {
                foreach(string directory in new[]{"SDK","NDK","OpenJDK"})if(!Directory.Exists(Path.Combine(sharedToolchain,directory)))throw new Exception("Missing shared Android toolchain directory: "+directory);
                AndroidExternalToolsSettings.sdkRootPath=Path.Combine(sharedToolchain,"SDK");AndroidExternalToolsSettings.ndkRootPath=Path.Combine(sharedToolchain,"NDK");AndroidExternalToolsSettings.jdkRootPath=Path.Combine(sharedToolchain,"OpenJDK");
                Debug.Log("ANDROID_SHARED_TOOLCHAIN: existing SDK/NDK/JDK selected for this build only; no duplicate download");
            }
            // Unity passes this as -Dorg.gradle.jvmargs, which overrides gradle.properties.
            AndroidExternalToolsSettings.maxJvmHeapSize=2048;
            Debug.Log("ANDROID_LAUNCHER_HEAP_LIMIT: "+AndroidExternalToolsSettings.maxJvmHeapSize+" MiB");
            ParkNaturalModelChecks.Run();ParkSharedInteractionChecks.Run();
            if(checkModelReferences)FlowerModelBuilder.CheckStablePartReferences();
            build();
        }
        finally {active=false;PlayerSettings.SetAdditionalIl2CppArgs(previous);AndroidExternalToolsSettings.maxJvmHeapSize=previousHeap;RestoreToolchainPath(previousSdk,"SDK",v=>AndroidExternalToolsSettings.sdkRootPath=v);RestoreToolchainPath(previousNdk,"NDK",v=>AndroidExternalToolsSettings.ndkRootPath=v);RestoreToolchainPath(previousJdk,"OpenJDK",v=>AndroidExternalToolsSettings.jdkRootPath=v);AssetDatabase.SaveAssets();}
    }
    static void RestoreToolchainPath(string value,string directory,Action<string> setter) {
        string embedded=Path.GetFullPath(Path.Combine(EditorApplication.applicationContentsPath,"PlaybackEngines","AndroidPlayer",directory));
        // Empty means embedded mode. Restoring its resolved but uninstalled
        // directory through the custom-path setter would instead throw.
        setter(string.IsNullOrEmpty(value)||string.Equals(Path.GetFullPath(value),embedded,StringComparison.OrdinalIgnoreCase)?"":value);
    }
}
