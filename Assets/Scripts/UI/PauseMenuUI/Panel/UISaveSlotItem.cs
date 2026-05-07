using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UISaveSlotItem : MonoBehaviour
{
    [Header("UI 组件引用")]
    public TextMeshProUGUI TxtSaveName;
    public TextMeshProUGUI TxtSaveDate;
    public Button ClickButton;

    public void Setup(string slotName, string dateString, bool isNewSave, System.Action<string, bool> onClickAction)
    {
        // 极简且稳健的文本替换逻辑
        if (TxtSaveName != null) 
        {
            TxtSaveName.text = isNewSave ? "+ 新建存档" : slotName;
            if (isNewSave) TxtSaveName.alignment = TextAlignmentOptions.Center; // 可选：让新建文字居中
        }
        
        if (TxtSaveDate != null) 
        {
            TxtSaveDate.text = isNewSave ? "" : dateString;
        }

        if (ClickButton != null)
        {
            // 每次生成时清除旧事件，防止重复触发
            ClickButton.onClick.RemoveAllListeners();
            ClickButton.onClick.AddListener(() => onClickAction?.Invoke(slotName, isNewSave));
        }
    }
}