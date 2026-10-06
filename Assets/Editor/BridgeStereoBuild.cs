using UnityEngine.XR.Management;
using UnityEngine.Rendering;
using UnityEditor.Build;
using UnityEditor.XR.Management;
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
    public static void Configure()
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
        go.AddComponent<PicoStereoCapture>().locator = locator;
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
        var rt = panel.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(470, 300);
        rt.localScale = Vector3.one * .001f; rt.localPosition = new Vector3(-.5f, .12f, 1.2f);
        panel.GetComponent<Image>().color = new Color(.01f,.02f,.04f,.8f);
        var textGo = new GameObject("Status", typeof(Text)); textGo.transform.SetParent(panel.transform,false);
        var text = textGo.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 23; text.color = Color.white; text.alignment = TextAnchor.UpperLeft;
        text.rectTransform.sizeDelta = new Vector2(440,270); locator.debugText = text;
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
        EnsurePicoRuntime();
        var settings = PXR_ProjectSetting.GetProjectConfig();
        settings.videoSeeThrough = true; settings.spatialMesh = false; settings.secureMR = false;
        EditorUtility.SetDirty(settings);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), "Assets/Scenes/BridgeStereoFusion.unity");
        // The ordinary Unity Build/Build And Run must now run the integrated main scene.
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
        foreach (var entry in EditorBuildSettings.scenes)
            if (entry.path != "Assets/Scenes/BridgeStereoFusion.unity")
                scenes.Add(new EditorBuildSettingsScene(entry.path, false));
        scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/BridgeStereoFusion.unity", true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
    }
    // A successful Android build can still be a flat app if XR Plug-in
    // Management lost its Android loader. This scene uses PXR APIs directly,
    // so always build it with the PICO loader and hand tracking enabled.
    static void EnsurePicoRuntime()
    {
        var settings = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(
            "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
        var loader = AssetDatabase.LoadAssetAtPath<PXR_Loader>("Assets/XR/Loaders/PXR_Loader.asset");
        if (settings == null || loader == null)
            throw new BuildFailedException("PICO XR settings or PXR_Loader asset is missing; cannot build a headset APK.");

        EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, settings, true);
        if (!settings.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
            settings.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
        var android = settings.SettingsForBuildTarget(BuildTargetGroup.Android);
        // Only Android is changed; desktop Editor simulation remains untouched.
        if (!android.Manager.TrySetLoaders(new System.Collections.Generic.List<XRLoader> { loader }))
            throw new BuildFailedException("Could not enable the Android PICO XR loader.");
        android.InitManagerOnStart = true;
        EditorUtility.SetDirty(android.Manager);
        EditorUtility.SetDirty(android);
        EditorUtility.SetDirty(settings);

        var pico = PXR_ProjectSetting.GetProjectConfig();
        pico.handTracking = true;
        pico.handTrackingSupportType = HandTrackingSupport.ControllersAndHands;
        EditorUtility.SetDirty(pico);
        AssetDatabase.SaveAssets();
        if (!Unity.XR.PXR.Editor.PXR_BuildProcessor.IsLoaderExists())
            throw new BuildFailedException("PICO SDK cannot find its Android XR loader; refusing to produce a flat APK.");
        Debug.Log("PICO_RUNTIME_CHECK_PASS: Android PXR loader, startup initialization, controllers and hands");
    }

    [MenuItem("Bridge/Build Stereo YOLO APK")]
    public static void Build()
    {
        // PICO's build processor reads EditorUserBuildSettings.activeBuildTarget
        // (rather than the BuildPipeline target). Make Android active first so
        // it cannot see the macOS Metal API during a direct APK build.
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
            !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            throw new Exception("Could not switch Unity to the Android build target.");
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
            new[] { GraphicsDeviceType.OpenGLES3 });
        FlowerModelBuilder.Generate();
        RightHandSpellVfxChecks.Run();
        StereoIntegrationChecks.Run();
        RequestedInteractionChecks.Run();
        Configure();
        Directory.CreateDirectory("Builds/Android");
        string originalId = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android);
        string originalName = PlayerSettings.productName;
        bool originalSigning = PlayerSettings.Android.useCustomKeystore;
        string originalKeystoreName = PlayerSettings.Android.keystoreName;
        string originalKeystorePass = PlayerSettings.Android.keystorePass;
        string originalKeyAlias = PlayerSettings.Android.keyaliasName;
        string originalKeyAliasPass = PlayerSettings.Android.keyaliasPass;
        string originalVersion = PlayerSettings.bundleVersion;
        int originalCode = PlayerSettings.Android.bundleVersionCode;
        try
        {
        // PICO's Unity 6 validation requires all four signing fields, even for
        // a local Development build. Use Android's conventional debug key
        // temporarily; it is only for installing the APK on a test headset.
        string debugKeystore = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".android", "debug.keystore");
        if (!File.Exists(debugKeystore))
            throw new FileNotFoundException("Android debug keystore was not found.", debugKeystore);
        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = debugKeystore;
        PlayerSettings.Android.keystorePass = "android";
        PlayerSettings.Android.keyaliasName = "androiddebugkey";
        PlayerSettings.Android.keyaliasPass = "android";
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,"com.yn.picmagicmr.stereo");
        PlayerSettings.productName = "Magic MR Stereo YOLO";
        PlayerSettings.bundleVersion = "1.11.0";
        PlayerSettings.Android.bundleVersionCode = 24;
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/BridgeStereoFusion.unity" },
            locationPathName = "Builds/Android/BridgeStereoFusion.apk",
            target = BuildTarget.Android, options = BuildOptions.Development });
        Debug.Log("BRIDGE_STEREO_BUILD=" + report.summary.result);
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Stereo integration build failed");
        }
        finally
        {
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,originalId);
            PlayerSettings.productName = originalName;
            PlayerSettings.Android.useCustomKeystore = originalSigning;
            PlayerSettings.Android.keystoreName = originalKeystoreName;
            PlayerSettings.Android.keystorePass = originalKeystorePass;
            PlayerSettings.Android.keyaliasName = originalKeyAlias;
            PlayerSettings.Android.keyaliasPass = originalKeyAliasPass;
            PlayerSettings.bundleVersion = originalVersion;
            PlayerSettings.Android.bundleVersionCode = originalCode;
        }
    }
}

/// <summary>
/// PICO's build validation requires explicit signing fields even for an APK
/// installed only on a local headset. This makes Unity's ordinary Build / Build
/// And Run button work on a developer Mac as well as the custom Bridge menu.
/// </summary>
public sealed class PicoDevelopmentSigning : IPreprocessBuildWithReport
{
    public int callbackOrder => -10000;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android)
            return;

        string debugKeystore = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".android", "debug.keystore");
        if (!File.Exists(debugKeystore))
            throw new FileNotFoundException("Android debug keystore was not found.", debugKeystore);

        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = debugKeystore;
        PlayerSettings.Android.keystorePass = "android";
        PlayerSettings.Android.keyaliasName = "androiddebugkey";
        PlayerSettings.Android.keyaliasPass = "android";
    }
}
