using UnityEngine;
using UnityEngine.EventSystems;

public class UIZoomPanController : MonoBehaviour, IScrollHandler, IDragHandler, IBeginDragHandler
{
    [Header("目标绑定")]
    public RectTransform TargetContainer;

    [Header("缩放配置")]
    public float ZoomSpeed = 0.1f;
    public float MinZoom = 0.5f;
    public float MaxZoom = 3.0f;

    [Header("平移配置")]
    public int PanMouseButton = 2;

    private bool _isPanning = false;

    // ==========================================
    // 新增：动态边界缓存
    // ==========================================
    private Vector2 _minBounds;
    private Vector2 _maxBounds;
    private bool _hasBounds = false;

    /// <summary>
    /// 由 TechTreeGridUI 在生成完所有节点后调用，注入动态计算出的网格极值
    /// </summary>
    public void SetBounds(Vector2 min, Vector2 max)
    {
        _minBounds = min;
        _maxBounds = max;
        _hasBounds = true;
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (TargetContainer == null) return;

        float zoomDelta = eventData.scrollDelta.y * ZoomSpeed;
        Vector3 currentScale = TargetContainer.localScale;

        float newScale = Mathf.Clamp(currentScale.x + zoomDelta, MinZoom, MaxZoom);
        TargetContainer.localScale = new Vector3(newScale, newScale, 1f);

        // 缩放后重新约束位置，防止边缘缩放时卡出边界
        ApplyBounds();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button == (PointerEventData.InputButton)PanMouseButton)
        {
            _isPanning = true;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (TargetContainer == null || !_isPanning) return;

        TargetContainer.anchoredPosition += eventData.delta;
        ApplyBounds();
    }

    private void ApplyBounds()
    {
        if (!_hasBounds || TargetContainer == null) return;

        // 计算当前缩放下的实际可视范围偏移
        Vector2 currentPos = TargetContainer.anchoredPosition;

        // 使用 Mathf.Clamp 将位置死死锁在极值范围内
        currentPos.x = Mathf.Clamp(currentPos.x, _minBounds.x, _maxBounds.x);
        currentPos.y = Mathf.Clamp(currentPos.y, _minBounds.y, _maxBounds.y);

        TargetContainer.anchoredPosition = currentPos;
    }

    private void Update()
    {
        if (Input.GetMouseButtonUp(PanMouseButton))
        {
            _isPanning = false;
        }
    }
    // ==========================================
    // 新增：丝滑的坐标焦点追踪系统
    // ==========================================
    public void SmoothPanTo(Vector2 targetCenterPos)
    {
        StopAllCoroutines(); // 打断当前可能正在进行的移动
        StartCoroutine(PanCoroutine(targetCenterPos));
    }

    private System.Collections.IEnumerator PanCoroutine(Vector2 targetPos)
    {
        Vector2 startPos = TargetContainer.anchoredPosition;
        float elapsed = 0f;
        float duration = 0.3f; // 0.3秒的丝滑过渡时间，可根据手感调整

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            // 数学核心：Cubic Ease-Out (三次函数缓出)，模拟物理阻尼刹车效果
            float ease = 1f - Mathf.Pow(1f - t, 3f);

            TargetContainer.anchoredPosition = Vector2.Lerp(startPos, targetPos, ease);
            ApplyBounds(); // 移动过程中依然保持绝对安全的物理边界锁死
            yield return null;
        }

        TargetContainer.anchoredPosition = targetPos;
        ApplyBounds();
    }
}