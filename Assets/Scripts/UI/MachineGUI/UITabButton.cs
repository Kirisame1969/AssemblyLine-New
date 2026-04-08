using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// 继承 IPointerClickHandler 接口来接管点击事件
public class UITabButton : MonoBehaviour, IPointerClickHandler
{
    [Header("配置")]
    public int TabIndex; // 这个按钮代表第几页？(比如 0 代表主页面，1 代表统计页)
    
    [Header("视觉表现参数")]
    public float SelectedWidth = 40f;   // 选中时的宽度（较短的黄色矩形）
    public float UnselectedWidth = 60f; // 未选中时的宽度（较长的黄色矩形）

    private RectTransform _rectTrans;
    private Image _image;

    private void Awake()
    {
        _rectTrans = GetComponent<RectTransform>();
        _image = GetComponent<Image>();
    }

    // ==========================================
    // 交互：当鼠标点击这个 Tab 时
    // ==========================================
    public void OnPointerClick(PointerEventData eventData)
    {
        if (MachineGUIController.Instance != null)
        {
            MachineGUIController.Instance.SwitchTab(TabIndex);
        }
    }

    // ==========================================
    // 表现：改变自身长短与高亮
    // ==========================================
    public void SetSelectedStatus(bool isSelected)
    {
        if (_rectTrans == null) return;

        // 修改按钮的宽度
        _rectTrans.sizeDelta = new Vector2(isSelected ? SelectedWidth : UnselectedWidth, _rectTrans.sizeDelta.y);
        
        
    }
}