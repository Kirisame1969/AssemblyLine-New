using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class UIModuleEntry : MonoBehaviour, IPointerClickHandler
{
    [Header("UI 视图绑定")]
    [Tooltip("左侧显示模块图标的 Image 组件")]
    public Image IconImage; 
    
    [Tooltip("右侧显示模块名字的 Text 组件")]
    public TMP_Text NameText; // 【注】：如果你项目中习惯使用 TextMeshPro，请将 Text 改为 TMP_Text，并引入 TMPro 命名空间

    private ModuleDefinition _moduleDef;

    // ==========================================
    // 初始化：严格的 Data 到 View 的单向绑定
    // ==========================================
    public void Init(ModuleDefinition def)
    {
        _moduleDef = def;
        if (def == null) return;

        // 1. 获取并渲染专属的“包装盒图标”（带回退安全保护）
        if (IconImage != null)
        {
            IconImage.sprite = def.GetItemIconOrDefault();
        }

        // 2. 渲染模块名称
        if (NameText != null)
        {
            NameText.text = def.DisplayName;
        }
    }

    // ==========================================
    // 交互：只负责将自己的数据汇报给 Controller
    // ==========================================
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (MachineGUIController.Instance != null && _moduleDef != null)
            {
                // 呼叫总控：指挥官选择了我！
                MachineGUIController.Instance.PickUpModule(_moduleDef);
            }
        }
    }
}