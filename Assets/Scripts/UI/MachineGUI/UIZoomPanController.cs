using UnityEngine;
using UnityEngine.EventSystems; // 必须引入事件系统

// 主要功能:检测与执行缩放与平移操作

// 继承滚轮监听、拖拽监听的官方接口
public class UIZoomPanController : MonoBehaviour, IScrollHandler, IDragHandler, IBeginDragHandler
{
    [Header("目标绑定")]
    [Tooltip("要被缩放和平移的那个装满格子的容器")]
    public RectTransform TargetContainer; 

    [Header("缩放配置")]
    public float ZoomSpeed = 0.1f;    // 滚轮缩放速度
    public float MinZoom = 0.5f;      // 最小缩放比例 (0.5倍)
    public float MaxZoom = 3.0f;      // 最大缩放比例 (3倍)

    [Header("平移配置")]
    [Tooltip("使用哪个鼠标按键拖拽？(0左键, 1右键, 2中键)")]
    public int PanMouseButton = 2;    

    // 记录拖拽状态
    private bool _isPanning = false;

    // ==========================================
    // 缩放逻辑 (滚轮)
    // ==========================================
    public void OnScroll(PointerEventData eventData)
    {
        if (TargetContainer == null) return;

        // eventData.scrollDelta.y 获取滚轮滚动的方向和幅度 (+1 向上放大，-1 向下缩小)
        float zoomDelta = eventData.scrollDelta.y * ZoomSpeed;
        
        // 获取当前的缩放值
        Vector3 currentScale = TargetContainer.localScale;
        
        // 计算新的缩放值，并用 Mathf.Clamp 限制在最大最小值之间
        float newScaleX = Mathf.Clamp(currentScale.x + zoomDelta, MinZoom, MaxZoom);
        float newScaleY = Mathf.Clamp(currentScale.y + zoomDelta, MinZoom, MaxZoom);

        // 应用新的缩放
        TargetContainer.localScale = new Vector3(newScaleX, newScaleY, 1f);
    }

    // ==========================================
    // 平移逻辑 (拖拽)
    // ==========================================
    public void OnBeginDrag(PointerEventData eventData)
    {
        // 只有按下指定的按键（如中键或右键）才允许开始平移
        if (eventData.button == (PointerEventData.InputButton)PanMouseButton)
        {
            _isPanning = true;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (TargetContainer == null || !_isPanning) return;

        // eventData.delta 是这一帧鼠标移动的屏幕像素差量
        // 直接将这个差量加到容器的相对坐标上，实现平移
        TargetContainer.anchoredPosition += eventData.delta;
    }

    // Unity 内置方法：当鼠标按键抬起时触发（由于没有继承 IEndDrag，用 Update 检测抬起也行，
    // 但更优雅的做法是在 IEndDrag 里写。不过为了防止和外部冲突，我们简单监听对应按键抬起即可）
    private void Update()
    {
        if (Input.GetMouseButtonUp(PanMouseButton))
        {
            _isPanning = false;
        }
    }
}