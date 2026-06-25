using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
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
    Vector3 _cardInitialScale = Vector3.one;
    Vector2 _cardCenterAnchoredPos = Vector2.zero;

    Transform CardRoot => card2DUI != null ? card2DUI.transform.parent : null;

    void Start()
    {
        if (card2DUI == null)
        {
            Debug.LogError("[CaptureTo2D] card2DUI 未赋值，请确认 GameManager 上有 CaptureDemoBootstrap。");
            return;
        }

        CacheCardLayout();
        if (CardRoot != null)
            CardRoot.gameObject.SetActive(false);
    }

    void Update()
    {
        if (_captured || card2DUI == null)
            return;

        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
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

        StartCoroutine(CaptureAndFlyRoutine());
    }

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
            target3DObject.SetActive(false);

        ResetCardToCenter();
        if (CardRoot != null)
        {
            CardRoot.gameObject.SetActive(true);
            CardRoot.SetAsLastSibling();
        }

        Debug.Log("[CaptureTo2D] 卡牌已显示在屏幕中央。");

        yield return new WaitForSeconds(0.5f);

        if (backpackIcon == null)
        {
            Debug.LogWarning("[CaptureTo2D] backpackIcon 未赋值，卡牌将直接隐藏。");
            if (CardRoot != null)
                CardRoot.gameObject.SetActive(false);
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
            time += Time.deltaTime;
            var t = Mathf.Clamp01(time / duration);
            cardRect.position = Vector3.Lerp(startPos, endPos, t);
            cardRect.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            yield return null;
        }

        if (CardRoot != null)
            CardRoot.gameObject.SetActive(false);
        ResetCardToCenter();

        Debug.Log("【数据埋点】成功捕捉目标，已发送至 Django 后台！");
    }

    void RefreshCardSnapshot()
    {
        if (captureCamera == null)
        {
            var camGo = GameObject.Find("CaptureCamera");
            if (camGo != null)
                captureCamera = camGo.GetComponent<Camera>();
        }

        if (captureCamera == null || !captureCamera.enabled)
            return;

        captureCamera.Render();
    }

    void ResetCardToCenter()
    {
        if (card2DUI == null)
            return;

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
    }

    public void SetTarget(GameObject target)
    {
        target3DObject = target;
    }

    public void SetCardUI(RawImage card, Transform backpack, Camera snapshotCamera = null)
    {
        card2DUI = card;
        backpackIcon = backpack;
        captureCamera = snapshotCamera;
        CacheCardLayout();
    }
}
