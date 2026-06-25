using UnityEngine;

/// <summary>
/// 让 SampleScene 在 Unity 编辑器 / 电脑端 Play 时不黑屏，方便没有 AR 设备时预览。
/// 真机 Android/iOS 打包时不生效。
/// </summary>
[DefaultExecutionOrder(-300)]
public class SampleSceneDesktopPreview : MonoBehaviour
{
    [SerializeField] bool enableDesktopPreview = true;
    [SerializeField] Vector3 cubePreviewPosition = new Vector3(0f, 0.5f, 2f);
    [SerializeField] Vector3 cameraPreviewPosition = new Vector3(0f, 1.2f, -4f);
    [SerializeField] Vector3 cameraPreviewEuler = new Vector3(12f, 0f, 0f);

    void Awake()
    {
        if (!enableDesktopPreview || !ShouldUseDesktopPreview())
            return;

        ApplyDesktopPreview();
    }

    static bool ShouldUseDesktopPreview()
    {
#if UNITY_EDITOR
        return true;
#elif UNITY_ANDROID || UNITY_IOS
        return false;
#else
        return true;
#endif
    }

    void ApplyDesktopPreview()
    {
        var arSession = GameObject.Find("AR Session");
        if (arSession != null)
            arSession.SetActive(false);

        DisableArBehaviours();

        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.transform.SetPositionAndRotation(cameraPreviewPosition, Quaternion.Euler(cameraPreviewEuler));
        }

        var cube = GameObject.Find("Cube");
        if (cube != null)
            cube.transform.position = cubePreviewPosition;

        Debug.Log("[SampleSceneDesktopPreview] 已切换为编辑器预览：Skybox 背景 + Cube 在相机前方。");
    }

    static void DisableArBehaviours()
    {
        foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (behaviour == null)
                continue;

            var typeName = behaviour.GetType().Name;
            if (typeName.Contains("ARCameraBackground")
                || typeName.Contains("ARCameraManager")
                || typeName.Contains("TrackedPose")
                || typeName.Contains("TrackedImage")
                || typeName.Contains("ARPlane")
                || typeName.Contains("ARRaycast")
                || typeName.Contains("ARSessionController"))
            {
                behaviour.enabled = false;
            }
        }
    }
}
