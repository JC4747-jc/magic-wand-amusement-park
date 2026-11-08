using System;
using System.IO;
using Perception;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Unity.XR.PXR;

public static class BridgeStereoBuild
{
    [MenuItem("Bridge/Configure Stereo YOLO Scene")]
    public static void Configure() => ConfigureScene(false);

    [MenuItem("Bridge/Configure Stereo F2G Pose Scene")]
    public static void ConfigurePose() => ConfigureScene(true);

    static void ConfigureScene(bool withPose, bool repair = false)
    {
        EditorSceneManager.OpenScene("Assets/Scenes/BridgeTest.unity");
        LighterModelBuilder.Apply(GameObject.Find("Lighter"));
        foreach (var c in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string n = c.GetType().Name;
            if (n == "PicoCameraCapture" || n == "PicoCameraPreviewUI" || n == "PicoCameraRayProvider" ||
                n == "PicoYoloEditorTestSource" || n == "InferenceBackendSwitcher" || n == "TargetLockDebugUI" ||
                n.Contains("SpatialMesh")) c.enabled = false;
        }
        var mesh = GameObject.Find("[Building Block] PICO Spatial Mesh");
        if (mesh != null) mesh.SetActive(false);
        var collider = GameObject.Find("Phase3_TestCollider");
        if (collider != null) collider.SetActive(false);
        var camera = GameObject.Find("XR Origin (VR)/Camera Offset/Main Camera").GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.clear;
        var go = new GameObject("Stereo YOLO Positioning");
        var locator = go.AddComponent<StereoYoloLocator>();
        locator.followMode = LighterFollowMode.VisualAverage;
        locator.recordStereoDiagnostics = repair;
        locator.trackedCamera = camera.transform;
        locator.lighter = GameObject.Find("Lighter").transform;
        locator.sender = UnityEngine.Object.FindFirstObjectByType<PicoYoloFrameSender>();
        locator.detections = UnityEngine.Object.FindFirstObjectByType<DetectionManager>();
        locator.transport = UnityEngine.Object.FindFirstObjectByType<Perception.TcpClient>();
        var gestures = UnityEngine.Object.FindFirstObjectByType<MagicMR.GestureManager>();
        gestures.SetEnabledDimensions(MagicMR.EnabledDimensions.All);
        if (gestures.GetComponent<MagicMR.TabletopGestureRecognizer>() == null)
            gestures.gameObject.AddComponent<MagicMR.TabletopGestureRecognizer>();
        foreach (var detector in gestures.GetComponents<MagicMR.HandGestureDetectorBase>())
            if (!(detector is MagicMR.TabletopGestureRecognizer)) detector.enabled = false;
        if (UnityEngine.Object.FindFirstObjectByType<MagicMR.StudyResetWristUi>() == null)
            new GameObject("Tabletop Reset").AddComponent<MagicMR.StudyResetWristUi>();
        var connection = new SerializedObject(locator.transport);
        connection.FindProperty("m_Host").stringValue = "127.0.0.1";
        connection.FindProperty("m_Port").intValue = 5005;
        connection.FindProperty("m_AutoConnect").boolValue = true;
        connection.FindProperty("m_AutoReconnect").boolValue = true;
        connection.ApplyModifiedPropertiesWithoutUndo();
        var capture = go.AddComponent<PicoStereoCapture>();
        capture.locator = locator;
        if (withPose)
        {
            var pose = go.AddComponent<F2GPoseClient>();
            pose.locator = locator;
            capture.poseClient = pose;
            pose.sendFps = 2;
            pose.requestDeadlineMs = 1200;
            pose.maxResultAge = .9f;
            go.AddComponent<PoseControl>().client = pose;
        }
        var so = new SerializedObject(locator.sender);
        so.FindProperty("m_MaxLongEdge").intValue = 640;
        so.FindProperty("m_MaxSendFps").floatValue = 10;
        so.FindProperty("m_JpegQuality").intValue = 90;
        // Keep native row zero at the JPEG top, so YOLO sees an upright image.
        so.FindProperty("m_FlipVerticallyBeforeEncode").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        var anchor = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<AnchorManager>());
        anchor.FindProperty("m_ProjectionMode").enumValueIndex = 2;
        anchor.FindProperty("m_UsePhysicsDepth").boolValue = false;
        anchor.FindProperty("m_StereoLocator").objectReferenceValue = locator;
        anchor.ApplyModifiedPropertiesWithoutUndo();
        var panel = new GameObject("Stereo Debug", typeof(Canvas), typeof(Image));
        panel.transform.SetParent(camera.transform, false);
        var canvas = panel.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
        var rt = panel.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(470, repair ? 420 : withPose ? 350 : 300);
        rt.localScale = Vector3.one * .001f; rt.localPosition = new Vector3(-.5f, .12f, 1.2f);
        panel.GetComponent<Image>().color = new Color(.01f,.02f,.04f,.8f);
        var textGo = new GameObject("Status", typeof(Text)); textGo.transform.SetParent(panel.transform,false);
        var text = textGo.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 23; text.color = Color.white; text.alignment = TextAnchor.UpperLeft;
        text.rectTransform.sizeDelta = new Vector2(440,repair ? 390 : withPose ? 320 : 270); locator.debugText = text;
        var crossGo = new GameObject("Aim Reference", typeof(Canvas));
        crossGo.transform.SetParent(camera.transform,false);
        crossGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        crossGo.GetComponent<Canvas>().worldCamera = camera;
        crossGo.transform.localPosition = new Vector3(0,0,1.2f);
        crossGo.transform.localScale = Vector3.one * .001f;
        var crossText = new GameObject("Cross",typeof(Text)).GetComponent<Text>();
        crossText.transform.SetParent(crossGo.transform,false);
        crossText.font = text.font; crossText.text = "+"; crossText.color = Color.yellow;
        crossText.fontSize = 36; crossText.alignment = TextAnchor.MiddleCenter;
        crossText.rectTransform.sizeDelta = new Vector2(50,50);
        var settings = PXR_ProjectSetting.GetProjectConfig();
        settings.videoSeeThrough = true; settings.spatialMesh = false; settings.secureMR = false;
        EditorUtility.SetDirty(settings);
        string scenePath = repair ? "Assets/Scenes/BridgeStereoRepair.unity" : withPose ? "Assets/Scenes/BridgeStereoPoseFusion.unity" : "Assets/Scenes/BridgeStereoFusion.unity";
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), scenePath);
        if (repair && (UnityEngine.Object.FindObjectsByType<F2GPoseClient>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 0 || capture.poseClient != null))
            throw new Exception("Position repair must not contain an F2G client");
        if (withPose || repair) { AssetDatabase.SaveAssets(); return; }
        // The ordinary Unity Build/Build And Run must now run the integrated main scene.
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
        foreach (var entry in EditorBuildSettings.scenes)
            if (entry.path != "Assets/Scenes/BridgeStereoFusion.unity")
                scenes.Add(new EditorBuildSettingsScene(entry.path, false));
        scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/BridgeStereoFusion.unity", true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
    }
    [MenuItem("Bridge/Build Stereo YOLO APK")]
    public static void Build() => BuildVersion(false);

    [MenuItem("Bridge/Build Stereo F2G Pose APK")]
    public static void BuildPose() => BuildVersion(true);

    [MenuItem("Bridge/Build Position Repair APK (Pose Off)")]
    public static void BuildRepair() => BuildVersion(false, true);

    static void BuildVersion(bool withPose, bool repair = false)
    {
        FlowerModelBuilder.Generate();
        StereoIntegrationChecks.Run();
        if (withPose) F2GPoseChecks.Run();
        ConfigureScene(withPose, repair);
        Directory.CreateDirectory("Builds/Android");
        string originalId = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android);
        string originalName = PlayerSettings.productName;
        bool originalSigning = PlayerSettings.Android.useCustomKeystore;
        string originalVersion = PlayerSettings.bundleVersion;
        int originalCode = PlayerSettings.Android.bundleVersionCode;
        try
        {
        PlayerSettings.Android.useCustomKeystore = false;
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,repair ? "com.yn.picmagicmr.stereo.repair" : withPose ? "com.yn.picmagicmr.stereo.pose" : "com.yn.picmagicmr.stereo");
        PlayerSettings.productName = repair ? "Magic MR Stereo Repair" : withPose ? "Magic MR Stereo Pose" : "Magic MR Stereo YOLO";
        PlayerSettings.bundleVersion = repair ? "1.10.1" : withPose ? "1.11.2" : "1.10.0";
        PlayerSettings.Android.bundleVersionCode = repair ? 25 : withPose ? 27 : 22;
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { repair ? "Assets/Scenes/BridgeStereoRepair.unity" : withPose ? "Assets/Scenes/BridgeStereoPoseFusion.unity" : "Assets/Scenes/BridgeStereoFusion.unity" },
            locationPathName = repair ? "Builds/Android/BridgeStereoRepair.apk" : withPose ? "Builds/Android/BridgeStereoPoseFusion.apk" : "Builds/Android/BridgeStereoFusion.apk",
            target = BuildTarget.Android, options = BuildOptions.Development });
        Debug.Log("BRIDGE_STEREO_BUILD=" + report.summary.result);
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Stereo integration build failed");
        }
        finally
        {
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,originalId);
            PlayerSettings.productName = originalName;
            PlayerSettings.Android.useCustomKeystore = originalSigning;
            PlayerSettings.bundleVersion = originalVersion;
            PlayerSettings.Android.bundleVersionCode = originalCode;
            AssetDatabase.SaveAssets();
        }
    }
}
