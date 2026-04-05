using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // 【新增】必须引入事件系统命名空间

// 【修改】：继承鼠标进入和离开的接口
public class UICell : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI 引用")]
    public Image Background; 
    public Image TopBorder;  
    public Image BottomBorder; 
    public Image LeftBorder;  
    public Image RightBorder; 

    public Vector2Int LogicPos { get; private set; }
    
    // 记录原本的底色，以便红绿灯预览结束后恢复
    private Color _baseColor; 

    public void Init(Vector2Int logicPos, CellVisualConfig config, bool isDeadCell)
    {
        LogicPos = logicPos;
        
        // 记录并设置基础颜色
        _baseColor = isDeadCell ? new Color(0.2f, 0.2f, 0.2f, 0.8f) : new Color(0.8f, 0.8f, 0.8f, 0.5f);
        if (Background != null) Background.color = _baseColor;

        Color darkBorder = new Color(0, 0, 0, 0.2f); 
        Color lightBorder = new Color(1, 1, 1, 0.8f); 

        if (TopBorder != null) TopBorder.color = config.ConnectTop ? darkBorder : lightBorder;
        if (BottomBorder != null) BottomBorder.color = config.ConnectBottom ? darkBorder : lightBorder;
        if (LeftBorder != null) LeftBorder.color = config.ConnectLeft ? darkBorder : lightBorder;
        if (RightBorder != null) RightBorder.color = config.ConnectRight ? darkBorder : lightBorder;
    }

    // ==========================================
    // 表现层方法：改变底色 (供 Controller 调用)
    // ==========================================
    public void SetHighlight(Color color)
    {
        if (Background != null) Background.color = color;
    }

    public void ClearHighlight()
    {
        if (Background != null) Background.color = _baseColor;
    }

    // ==========================================
    // 鼠标事件监听：向总控汇报自己的坐标
    // ==========================================
    public void OnPointerEnter(PointerEventData eventData)
    {
        // 【新增探针】：确认 Unity 到底有没有把鼠标事件发给这个格子！
        Debug.Log($"[UI 射线] 鼠标成功触碰到了格子：逻辑坐标 {LogicPos}");
        
        if (MachineGUIController.Instance != null)
            MachineGUIController.Instance.OnCellHoverEnter(LogicPos);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (MachineGUIController.Instance != null)
            MachineGUIController.Instance.OnCellHoverExit(LogicPos);
    }
}