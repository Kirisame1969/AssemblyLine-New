using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AssemblyLine.Data.Machine;

namespace AssemblyLine.UI
{
    /// <summary>
    /// 仓库库存的单个 UI 槽位表现层。
    /// 仅负责接收数据并更新贴图与文本，无任何逻辑运算。
    /// </summary>
    public class UIInventorySlot : MonoBehaviour
    {
        [Header("UI 组件绑定")]
        public Image ItemIcon;           // 物品图标
        public TextMeshProUGUI CountText;// 数量文本

        // 内部缓存的数据索引，供后续拖拽系统使用
        public int SlotIndex { get; private set; }

        public void InitSlot(int index)
        {
            SlotIndex = index;
            ClearVisuals();
        }

        /// <summary>
        /// 刷新该 UI 槽位的视觉表现。
        /// </summary>
        /// <param name="slotData">底层对应槽位的只读引用</param>
        public void RefreshVisuals(InventorySlot slotData)
        {
            if (slotData == null || slotData.IsEmpty)
            {
                ClearVisuals();
                return;
            }

            // 显示图标
            if (slotData.ItemType != null && slotData.ItemType.Icon != null)
            {
                ItemIcon.sprite = slotData.ItemType.Icon;
                ItemIcon.color = Color.white; // 恢复不透明
                ItemIcon.gameObject.SetActive(true);
            }

            // 显示数量
            CountText.text = slotData.Count.ToString();
            CountText.gameObject.SetActive(true);
        }

        private void ClearVisuals()
        {
            ItemIcon.sprite = null;
            ItemIcon.color = Color.clear; // 完全透明
            ItemIcon.gameObject.SetActive(false);
            
            CountText.text = "";
            CountText.gameObject.SetActive(false);
        }
    }
}