#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CaptureDemoSetup
{
    const string DemoScenePath = "Assets/Scenes/DesktopCaptureDemo.unity";

    [MenuItem("Magic Wand/Setup Desktop Capture Demo")]
    public static void SetupDesktopDemo()
    {
        TagManagerSetup.EnsureCaptureTargetTag();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var light = Object.FindFirstObjectByType<Light>();
        if (light != null)
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Cube";
        cube.transform.position = new Vector3(0f, 0.5f, 2f);

        var layer = LayerMask.NameToLayer("3D_Target");
        if (layer >= 0)
            cube.layer = layer;
        cube.tag = "CaptureTarget";

        var manager = new GameObject("GameManager");
        var bootstrap = manager.AddComponent<CaptureDemoBootstrap>();
        bootstrap.disableArComponentsOnDesktop = false;
        bootstrap.captureCube = cube;

        bootstrap.cardRenderTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/CardTexture.renderTexture");
        bootstrap.backpackTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Backage_icon.png");

        manager.AddComponent<CaptureTo2D>();

        EditorSceneManager.SaveScene(scene, DemoScenePath);
        Debug.Log($"[Magic Wand] 桌面演示场景已保存: {DemoScenePath}，点击 Play 后鼠标点击 Cube 测试。");
    }

    [MenuItem("Magic Wand/Add GameManager To Open Scene")]
    public static void AddGameManagerToOpenScene()
    {
        TagManagerSetup.EnsureCaptureTargetTag();

        if (GameObject.Find("GameManager") != null)
        {
            Debug.LogWarning("场景中已有 GameManager。");
            return;
        }

        var manager = new GameObject("GameManager");
        var bootstrap = manager.AddComponent<CaptureDemoBootstrap>();
        bootstrap.cardRenderTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/CardTexture.renderTexture");
        bootstrap.backpackTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Backage_icon.png");
        manager.AddComponent<CaptureTo2D>();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[Magic Wand] 已在当前场景添加 GameManager（含自动装配）。保存场景后 Play 测试。");
    }
}

static class TagManagerSetup
{
    public static void EnsureCaptureTargetTag()
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0)
            return;

        var tagManager = new SerializedObject(assets[0]);
        var tags = tagManager.FindProperty("tags");
        for (var i = 0; i < tags.arraySize; i++)
        {
            if (tags.GetArrayElementAtIndex(i).stringValue == "CaptureTarget")
                return;
        }

        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = "CaptureTarget";
        tagManager.ApplyModifiedProperties();
    }
}
#endif
