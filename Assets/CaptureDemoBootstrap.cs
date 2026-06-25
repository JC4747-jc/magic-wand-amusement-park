using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
using UnityEngine.Rendering.Universal;
#endif

/// <summary>
/// 电脑端 Demo：自动创建 CaptureCamera、Canvas、卡牌与背包，并连线 CaptureTo2D。
/// 挂到 GameManager 上即可 Play 测试。
/// </summary>
[DefaultExecutionOrder(-200)]
public class CaptureDemoBootstrap : MonoBehaviour
{
    const string TargetLayerName = "3D_Target";
    const string TargetTagName = "CaptureTarget";

    [Header("资源（可在 Inspector 覆盖）")]
    public RenderTexture cardRenderTexture;
    public Texture2D backpackTexture;

    [Header("场景中的 Cube（留空则自动查找 CaptureTarget / 名为 Cube 的物体）")]
    public GameObject captureCube;

    [Header("桌面演示：禁用 AR / XR 组件")]
    public bool disableArComponentsOnDesktop = true;

    CaptureTo2D _capture;
    Camera _captureCamera;

    void Awake()
    {
        _capture = GetComponent<CaptureTo2D>();
        if (_capture == null)
            _capture = gameObject.AddComponent<CaptureTo2D>();

        if (disableArComponentsOnDesktop)
            DisableArForDesktop();

        EnsureCaptureCube();
        EnsureCaptureCamera();
        var (card, backpack) = EnsureCanvasUI();
        _capture.SetCardUI(card, backpack, _captureCamera);
        _capture.SetTarget(captureCube);
    }

    void DisableArForDesktop()
    {
        var arSession = GameObject.Find("AR Session");
        if (arSession != null)
            arSession.SetActive(false);

        var xrOrigin = GameObject.Find("XR Origin (VR)") ?? GameObject.Find("XR Origin");
        if (xrOrigin != null)
            xrOrigin.SetActive(false);

        var legacyCam = GameObject.Find("Main Camera");
        if (legacyCam != null && legacyCam.GetComponent<Camera>() != null && !legacyCam.activeInHierarchy)
        {
            legacyCam.tag = "MainCamera";
            legacyCam.SetActive(true);
            var cam = legacyCam.GetComponent<Camera>();
            cam.enabled = true;
        }

        foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (behaviour == null)
                continue;

            var typeName = behaviour.GetType().Name;
            if (typeName.Contains("TrackedImage") || typeName.Contains("ARPlane") || typeName.Contains("ARRaycast"))
                behaviour.enabled = false;
        }
    }

    void EnsureCaptureCube()
    {
        if (captureCube != null)
        {
            ApplyTargetLayerAndTag(captureCube);
            PositionCubeForDesktopIfNeeded(captureCube);
            return;
        }

        var tagged = GameObject.FindGameObjectWithTag(TargetTagName);
        if (tagged != null)
        {
            captureCube = tagged;
            ApplyTargetLayerAndTag(captureCube);
            PositionCubeForDesktopIfNeeded(captureCube);
            return;
        }

        var cubeByName = GameObject.Find("Cube");
        if (cubeByName != null)
        {
            captureCube = cubeByName;
            ApplyTargetLayerAndTag(captureCube);
            PositionCubeForDesktopIfNeeded(captureCube);
            return;
        }

        captureCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        captureCube.name = "Cube";
        captureCube.transform.position = new Vector3(0f, 0.5f, 2f);
        ApplyTargetLayerAndTag(captureCube);
    }

    void PositionCubeForDesktopIfNeeded(GameObject cube)
    {
        if (!disableArComponentsOnDesktop)
            return;

        if (cube.transform.position == Vector3.zero)
            cube.transform.position = new Vector3(0f, 0.5f, 2f);
    }

    static void ApplyTargetLayerAndTag(GameObject go)
    {
        var layer = LayerMask.NameToLayer(TargetLayerName);
        if (layer >= 0)
            SetLayerRecursively(go, layer);

        try
        {
            if (!go.CompareTag(TargetTagName))
                go.tag = TargetTagName;
        }
        catch (UnityException)
        {
            Debug.LogWarning($"[CaptureDemoBootstrap] 请在 Tag Manager 中添加标签 \"{TargetTagName}\"。");
        }
    }

    static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    void EnsureCaptureCamera()
    {
        if (_captureCamera != null)
            return;

        var existing = GameObject.Find("CaptureCamera");
        if (existing != null)
        {
            _captureCamera = existing.GetComponent<Camera>();
            if (_captureCamera != null)
                return;
        }

        var layer = LayerMask.NameToLayer(TargetLayerName);
        var layerMask = layer >= 0 ? (1 << layer) : ~0;

        var camGo = new GameObject("CaptureCamera");
        _captureCamera = camGo.AddComponent<Camera>();
        _captureCamera.clearFlags = CameraClearFlags.SolidColor;
        _captureCamera.backgroundColor = new Color(0.12f, 0.14f, 0.18f, 1f);
        _captureCamera.cullingMask = layerMask;
        _captureCamera.orthographic = false;
        _captureCamera.fieldOfView = 40f;
        _captureCamera.nearClipPlane = 0.1f;
        _captureCamera.farClipPlane = 10f;
        _captureCamera.depth = -10;
        _captureCamera.enabled = true;

#if UNITY_RENDER_PIPELINE_UNIVERSAL
        var urpData = camGo.AddComponent<UniversalAdditionalCameraData>();
        urpData.renderType = CameraRenderType.Base;
        urpData.requiresColorTexture = false;
        urpData.requiresDepthTexture = false;
#endif

        if (cardRenderTexture == null)
            cardRenderTexture = Resources.Load<RenderTexture>("CardTexture");
        if (cardRenderTexture != null)
            _captureCamera.targetTexture = cardRenderTexture;

        if (captureCube != null)
        {
            var targetPos = captureCube.transform.position;
            camGo.transform.position = targetPos + new Vector3(0.8f, 0.6f, -1.2f);
            camGo.transform.LookAt(targetPos + Vector3.up * 0.1f);
        }
        else
        {
            camGo.transform.position = new Vector3(0.8f, 1.1f, 0.8f);
            camGo.transform.LookAt(new Vector3(0f, 0.5f, 2f));
        }
    }

    (RawImage card, Transform backpack) EnsureCanvasUI()
    {
        var existingCard = GameObject.Find("2D_Card_UI");
        if (existingCard != null && existingCard.TryGetComponent<RawImage>(out var existingRaw))
        {
            var bp = GameObject.Find("Backpack_Icon");
            return (existingRaw, bp != null ? bp.transform : null);
        }

        EnsureEventSystem();
        EnsureDesktopMainCamera();

        if (cardRenderTexture == null)
            cardRenderTexture = Resources.Load<RenderTexture>("CardTexture");

        var canvasGo = new GameObject("CaptureCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        var cardRoot = new GameObject("2D_Card_UI", typeof(RectTransform));
        cardRoot.transform.SetParent(canvasGo.transform, false);
        var cardRect = cardRoot.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(320f, 320f);

        var frameGo = new GameObject("CardFrame", typeof(RectTransform));
        frameGo.transform.SetParent(cardRoot.transform, false);
        var frameRect = frameGo.GetComponent<RectTransform>();
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.offsetMin = Vector2.zero;
        frameRect.offsetMax = Vector2.zero;
        var frameImage = frameGo.AddComponent<Image>();
        frameImage.color = new Color(1f, 0.95f, 0.75f, 1f);

        var imageGo = new GameObject("CardImage", typeof(RectTransform));
        imageGo.transform.SetParent(cardRoot.transform, false);
        var imageRect = imageGo.GetComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = new Vector2(12f, 12f);
        imageRect.offsetMax = new Vector2(-12f, -12f);

        var raw = imageGo.AddComponent<RawImage>();
        if (cardRenderTexture != null)
            raw.texture = cardRenderTexture;
        raw.color = Color.white;

        cardRoot.SetActive(false);

        var backpackGo = new GameObject("Backpack_Icon", typeof(RectTransform));
        backpackGo.transform.SetParent(canvasGo.transform, false);
        var bpRect = backpackGo.GetComponent<RectTransform>();
        bpRect.anchorMin = new Vector2(1f, 0f);
        bpRect.anchorMax = new Vector2(1f, 0f);
        bpRect.pivot = new Vector2(1f, 0f);
        bpRect.anchoredPosition = new Vector2(-40f, 40f);
        bpRect.sizeDelta = new Vector2(96f, 96f);

        var bpImage = backpackGo.AddComponent<Image>();
        if (backpackTexture == null)
            backpackTexture = Resources.Load<Texture2D>("Backage_icon");
        if (backpackTexture != null)
        {
            bpImage.sprite = Sprite.Create(
                backpackTexture,
                new Rect(0f, 0f, backpackTexture.width, backpackTexture.height),
                new Vector2(0.5f, 0.5f));
        }

        return (raw, backpackGo.transform);
    }

    static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
            return;

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    static void EnsureDesktopMainCamera()
    {
        if (Camera.main != null)
            return;

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 50f;
        camGo.AddComponent<AudioListener>();
        camGo.transform.position = new Vector3(0f, 1.2f, -4f);
        camGo.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
    }
}
