using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Unity.XR.PXR;
using PackageInfo=UnityEditor.PackageManager.PackageInfo;

// Preserve explicitly versioned project requirements after the SDK's cold-import
// default-asset creation. SDK package code and production scene logic stay intact.
public sealed class PicoRuntimeFeatureGuard : IPreprocessBuildWithReport
{
    [Serializable] sealed class Policy
    {
        public int schema;
        public string packageVersion;
        public string configPath;
        public string configGuid;
        public bool handTracking;
        public bool videoSeeThrough;
        public bool portalInited;
    }
    [Serializable] sealed class Receipt
    {
        public string checkedAtUtc,phase,packageVersion,configPath,configGuid;
        public bool changed,beforeHandTracking,beforeVideoSeeThrough,beforePortalInited;
        public bool afterHandTracking,afterVideoSeeThrough,afterPortalInited;
    }
    public int callbackOrder=>-10000;
    public void OnPreprocessBuild(BuildReport report)
    {
        if(report.summary.platform==BuildTarget.Android)Apply("android-preprocess");
    }
    public static void RunNarrativeChecks()
    {
        Apply("source-candidate-render");
        HciStatueNarrativeChecks.Run();
    }
    static void Apply(string phase)
    {
        var policy=JsonUtility.FromJson<Policy>(File.ReadAllText("ProjectSettings/PicoRuntimeRequirements.json"));
        if(policy==null||policy.schema!=1)throw new BuildFailedException("Unknown PICO runtime requirement policy");
        var package=PackageInfo.GetAllRegisteredPackages().Single(p=>p.name=="com.unity.xr.picoxr");
        if(package.version!=policy.packageVersion)throw new BuildFailedException("Review PICO requirement policy for this SDK version before building");
        if(AssetDatabase.AssetPathToGUID(policy.configPath)!=policy.configGuid)
            throw new BuildFailedException("PICO runtime settings GUID differs; no replacement asset created");
        var config=PXR_ProjectSetting.GetProjectConfig();
        if(!config||AssetDatabase.GetAssetPath(config)!=policy.configPath)
            throw new BuildFailedException("SDK did not resolve the explicit project runtime config");
        var receipt=new Receipt{checkedAtUtc=DateTime.UtcNow.ToString("o"),phase=phase,packageVersion=package.version,
            configPath=policy.configPath,configGuid=policy.configGuid,beforeHandTracking=config.handTracking,
            beforeVideoSeeThrough=config.videoSeeThrough,beforePortalInited=config.portalInited};
        receipt.changed=config.handTracking!=policy.handTracking||config.videoSeeThrough!=policy.videoSeeThrough||config.portalInited!=policy.portalInited;
        if(receipt.changed)
        {
            config.handTracking=policy.handTracking;
            config.videoSeeThrough=policy.videoSeeThrough;
            config.portalInited=policy.portalInited;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }
        var resolved=PXR_ProjectSetting.GetProjectConfig();
        if(resolved!=config||resolved.handTracking!=policy.handTracking||resolved.videoSeeThrough!=policy.videoSeeThrough||resolved.portalInited!=policy.portalInited)
            throw new BuildFailedException("PICO runtime requirements did not persist; build refused");
        receipt.afterHandTracking=resolved.handTracking;receipt.afterVideoSeeThrough=resolved.videoSeeThrough;receipt.afterPortalInited=resolved.portalInited;
        var folder=Path.GetFullPath(".codex-tmp/pico-runtime-policy");
        Directory.CreateDirectory(folder);
        var path=Path.Combine(folder,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff")+"-"+phase+".json");
        using(var file=new StreamWriter(new FileStream(path,FileMode.CreateNew,FileAccess.Write)))
            file.Write(JsonUtility.ToJson(receipt,true));
        Debug.Log("PICO_RUNTIME_POLICY_PASS: handTracking="+resolved.handTracking+" VST="+resolved.videoSeeThrough+" corrected="+receipt.changed);
    }
}
