using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using AssemblyLine.Data.Machine;

namespace AssemblyLine.UI
{
    /// <summary>
    /// 仓库库存的单个 UI 槽位表现层。
    /// 仅负责接收数据并更新贴图与文本，无任何逻辑运算。
    /// </summary>
    public class UIInventorySlot : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [Header("UI 组件绑定")]
        public Image ItemIcon;           // 物品图标
        public TextMeshProUGUI CountText;// 数量文本

        // 内部缓存的数据索引，供后续拖拽系统使用
        public int SlotIndex { get; private set; }
        private UIWarehousePanel _parentPanel; // 向上通讯的引用
        public void InitSlot(int index, UIWarehousePanel parent)
        {
            SlotIndex = index;
            _parentPanel = parent;
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

        // ==========================================
        // 探针 1：最基础的鼠标按下测试
        // ==========================================
        public void OnPointerDown(PointerEventData eventData)
        {
            Debug.Log($"[UI 探针] 成功点按了槽位 {SlotIndex}！射线没有被遮挡。");
        }

        // ==========================================
        // 探针 2：拖拽事件链测试
        // ==========================================
        // ==========================================
        // 拖拽事件捕获 (View -> Controller)
        // ==========================================
        public void OnBeginDrag(PointerEventData eventData)
        {
            Debug.Log($"[UI 探针] 槽位 {SlotIndex} 尝试开始拖拽！图标是否激活: {ItemIcon.gameObject.activeSelf}");
            
            if (_parentPanel == null)
            {
                Debug.LogError($"[UI 致命错误] 槽位 {SlotIndex} 的 _parentPanel 为空！请检查 UIWarehousePanel.cs 的 SyncGridCapacity 方法中是否漏传了 this 参数！");
                return;
            }

            if (ItemIcon.gameObject.activeSelf)
            {
                _parentPanel.OnSlotBeginDrag(SlotIndex, ItemIcon.sprite);
            }
        }


        

        public void OnDrag(PointerEventData eventData)
        {
            if (_parentPanel != null) _parentPanel.OnSlotDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            Debug.Log($"[UI 探针] 槽位 {SlotIndex} 拖拽结束！");
            if (_parentPanel != null) _parentPanel.OnSlotEndDrag();
        }

        public void OnDrop(PointerEventData eventData)
        {
            Debug.Log($"[UI 探针] 有物品在槽位 {SlotIndex} 上松开（Drop）！");
            if (_parentPanel != null) _parentPanel.OnSlotDrop(SlotIndex);
        }
    }
}