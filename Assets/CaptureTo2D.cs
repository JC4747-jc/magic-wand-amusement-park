using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections;

public class CaptureTo2D : MonoBehaviour
{
    [Header("配置面板")]
    public GameObject target3DObject;
    public RawImage card2DUI;
    public Transform backpackIcon;
    public Camera captureCamera;

    [Header("可选：用 Tag 匹配目标（AR 动态生成时更稳）")]
    public string targetTag = "CaptureTarget";

    bool _captured;
    Coroutine _motion;
    GameObject _hiddenTarget;
    public bool IsCaptured => _captured;
    public bool IsAnimating => _motion != null;
    Vector3 _cardInitialScale = Vector3.one;
    Vector2 _cardCenterAnchoredPos = Vector2.zero;
    bool _layoutCached;

    Transform CardRoot
    {
        get
        {
            if(card2DUI==null)return null;
            var parent=card2DUI.transform.parent;
            // A dedicated card panel may include its border/label. The shared
            // Canvas or a container holding the backpack must stay in place.
            if(parent!=null&&parent.GetComponent<Canvas>()==null&&(backpackIcon==null||!backpackIcon.IsChildOf(parent)))return parent;
            return card2DUI.transform;
        }
    }

    void Start()
    {
        if (card2DUI == null)
        {
            Debug.LogError("[CaptureTo2D] card2DUI 未赋值，请确认 GameManager 上有 CaptureDemoBootstrap。");
            return;
        }

        CacheCardLayout();
        ResolveCaptureCamera();
        if (CardRoot != null)
            CardRoot.gameObject.SetActive(false);
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            ResetCapture();
            return;
        }
        if (_captured || card2DUI == null)
            return;

        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[CaptureTo2D] 未找到 MainCamera。");
            return;
        }

        var ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out var hit))
            return;

        if (!IsCaptureTarget(hit.collider.gameObject))
            return;

        TryCapture();
    }

    public bool TryCapture()
    {
        if (!isActiveAndEnabled || _captured || card2DUI == null || target3DObject == null || !target3DObject.activeInHierarchy)
            return false;
        _motion = StartCoroutine(CaptureAndFlyRoutine());
        return true;
    }

    public void ResetCapture()
    {
        if (_motion != null) StopCoroutine(_motion);
        _motion = null;
        _captured = false;
        if (_hiddenTarget != null) _hiddenTarget.SetActive(true);
        _hiddenTarget = null;
        if (CardRoot != null) CardRoot.gameObject.SetActive(false);
        ResetCardToCenter();
    }

    void OnDisable() => ResetCapture();

    bool IsCaptureTarget(GameObject go)
    {
        if (target3DObject != null)
            return go == target3DObject || go.transform.IsChildOf(target3DObject.transform);

        if (!string.IsNullOrEmpty(targetTag) && go.CompareTag(targetTag))
            return true;

        var layer = LayerMask.NameToLayer("3D_Target");
        return layer >= 0 && go.layer == layer;
    }

    IEnumerator CaptureAndFlyRoutine()
    {
        _captured = true;

        // Cube 还在时先拍一张，避免 RenderTexture 是空的
        RefreshCardSnapshot();

        if (target3DObject != null)
        {
            _hiddenTarget = target3DObject;
            target3DObject.SetActive(false);
        }

        ResetCardToCenter();
        if (CardRoot != null)
        {
            CardRoot.gameObject.SetActive(true);
            CardRoot.SetAsLastSibling();
        }

        Debug.Log("[CaptureTo2D] 卡牌已显示在屏幕中央。");

        yield return new WaitForSecondsRealtime(0.5f);

        if (backpackIcon == null)
        {
            Debug.LogWarning("[CaptureTo2D] backpackIcon 未赋值，卡牌将直接隐藏。");
            if (CardRoot != null)
                CardRoot.gameObject.SetActive(false);
            _motion = null;
            yield break;
        }

        float time = 0f;
        const float duration = 1.0f;

        var cardRect = CardRoot as RectTransform ?? card2DUI.rectTransform;
        var startPos = cardRect.position;
        var endPos = backpackIcon.position;
        var startScale = cardRect.localScale;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(time / duration);
            cardRect.position = Vector3.Lerp(startPos, endPos, t);
            cardRect.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            yield return null;
        }

        if (CardRoot != null)
            CardRoot.gameObject.SetActive(false);
        ResetCardToCenter();

        _motion = null;
        Debug.Log("[CaptureTo2D] 本地捕捉动画完成；此演示未发送后台请求。");
    }

    void RefreshCardSnapshot()
    {
        ResolveCaptureCamera();
        if (captureCamera == null)
            return;
        FrameCaptureTarget();
        captureCamera.Render();
    }

    void ResolveCaptureCamera()
    {
        if (captureCamera == null)
        {
            var camGo = GameObject.Find("CaptureCamera");
            if (camGo != null)
                captureCamera = camGo.GetComponent<Camera>();
        }

        // Snapshot cameras render only on capture. Never disable the viewer if
        // an existing scene deliberately assigned its main camera here.
        if(captureCamera!=null&&captureCamera!=Camera.main)captureCamera.enabled=false;
    }

    void FrameCaptureTarget()
    {
        if(target3DObject == null)return;
        var renderers=target3DObject.GetComponentsInChildren<Renderer>();
        if(renderers.Length==0)return;
        Bounds bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
        float radius=bounds.extents.magnitude;
        if(!float.IsFinite(radius)||radius<.001f)return;
        Vector3 direction=captureCamera.transform.position-bounds.center;
        if(direction.sqrMagnitude<.0001f)direction=new Vector3(.8f,.6f,-1.2f);
        float aspect=captureCamera.targetTexture?captureCamera.targetTexture.width/(float)captureCamera.targetTexture.height:captureCamera.aspect;
        float vertical=captureCamera.fieldOfView*Mathf.Deg2Rad*.5f;
        float horizontal=Mathf.Atan(Mathf.Tan(vertical)*Mathf.Max(.01f,aspect));
        float distance=radius*1.12f/Mathf.Sin(Mathf.Clamp(Mathf.Min(vertical,horizontal),.05f,1.5f));
        if(captureCamera.orthographic)captureCamera.orthographicSize=radius*1.12f/Mathf.Min(1,aspect);
        captureCamera.transform.position=bounds.center+direction.normalized*distance;captureCamera.transform.LookAt(bounds.center);
        captureCamera.nearClipPlane=Mathf.Max(.01f,distance-radius*1.5f);
        captureCamera.farClipPlane=distance+radius*2+1;
    }

    void ResetCardToCenter()
    {
        if (card2DUI == null)
            return;

        if (!_layoutCached) CacheCardLayout();

        var cardRect = CardRoot as RectTransform ?? card2DUI.rectTransform;
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = _cardCenterAnchoredPos;
        cardRect.localScale = _cardInitialScale;
    }

    void CacheCardLayout()
    {
        if (card2DUI == null)
            return;

        var cardRect = CardRoot as RectTransform ?? card2DUI.rectTransform;
        _cardInitialScale = cardRect.localScale;
        _cardCenterAnchoredPos = cardRect.anchoredPosition;
        _layoutCached = true;
    }

    public void SetTarget(GameObject target)
    {
        ResetCapture();
        target3DObject = target;
    }

    public void SetCardUI(RawImage card, Transform backpack, Camera snapshotCamera = null)
    {
        ResetCapture();
        card2DUI = card;
        _layoutCached = false;
        backpackIcon = backpack;
        captureCamera = snapshotCamera;
        CacheCardLayout();
    }
}
