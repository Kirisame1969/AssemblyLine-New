using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UIModuleEntry : MonoBehaviour, IPointerClickHandler
{
    public Image IconImage; // 显示模块的图标
    
    private ModuleDefinition _moduleDef;

    // 初始化方法
    public void Init(ModuleDefinition def)
    {
        _moduleDef = def;
        if (IconImage != null && def != null)
        {
            IconImage.sprite = def.Icon;
        }
    }

    // 当玩家左键点击侧边栏的这个按钮时
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            // 呼叫总控：我被拿起来了！
            if (MachineGUIController.Instance != null && _moduleDef != null)
            {
                MachineGUIController.Instance.PickUpModule(_moduleDef);
            }
        }
    }
}