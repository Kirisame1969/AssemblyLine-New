using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using AssemblyLine.Core.Configuration;

namespace AssemblyLine.UI
{
    /// <summary>
    /// 白名单面板中的单项物品图标表现层。
    /// 接收点击事件并上报给面板。
    /// </summary>
    public class UIPortFilterIcon : MonoBehaviour, IPointerClickHandler
    {
        [Header("UI 绑定")]
        public Image ItemIcon;
        public GameObject HighlightBorder; // 用于表示“已在白名单中”的绿色高亮框

        private string _itemID;
        private UIPortConfigPanel _parentPanel;

        public void Init(ItemDefinition itemDef, UIPortConfigPanel parent)
        {
            _parentPanel = parent;
            _itemID = itemDef.ItemID;
            
            if (ItemIcon != null && itemDef.Icon != null)
            {
                ItemIcon.sprite = itemDef.Icon;
            }
            SetHighlight(false);
        }

        public void SetHighlight(bool isWhitelisted)
        {
            if (HighlightBorder != null)
            {
                HighlightBorder.SetActive(isWhitelisted);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_parentPanel != null)
            {
                _parentPanel.ToggleItemRule(_itemID);
            }
        }
    }
}