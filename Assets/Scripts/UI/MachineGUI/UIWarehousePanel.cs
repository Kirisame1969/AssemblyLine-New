using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using AssemblyLine.Data.Machine;

namespace AssemblyLine.UI
{
    /// <summary>
    /// 仓库专用的 UI 视图控制器。
    /// 负责基于底层 InventoryData 动态构建网格，并执行按需刷新。
    /// </summary>
    public class UIWarehousePanel : MonoBehaviour
    {
        [Header("UI 绑定")]
        public Transform SlotGridContainer;   // 推荐挂载 Unity 原生的 GridLayoutGroup 组件
        public GameObject UIInventorySlotPrefab; // 刚才写的格子的预制体

        [Header("拖拽系统 (需在 UI 上挂载空 Image)")]
        public RectTransform DragGhost; 
        public Image DragGhostImage;

        [Header("性能优化")]
        [Tooltip("UI 每秒刷新频率（赫兹）")]
        public float UIRefreshRate = 15f;

        private WarehouseCoreData _targetWarehouse;
        private List<UIInventorySlot> _uiSlots = new List<UIInventorySlot>();

        private float _refreshTimer = 0f;
        private int _dragSourceIndex = -1;

        /// <summary>
        /// 当玩家打开机箱面板，且机箱是仓库时调用。
        /// </summary>
        public void InitPanel(WarehouseCoreData warehouse)
        {
            _targetWarehouse = warehouse;
            if (_targetWarehouse == null) return;

            // 1. 同步生成网格 UI 对象 (生命周期映射)
            SyncGridCapacity();
            
            // 2. 初始化立刻刷新一次视觉
            RefreshAllSlots();

            // 确保拖拽残影初始处于隐藏状态
            if (DragGhost != null) DragGhost.gameObject.SetActive(false);
        }

        /// <summary>
        /// 校验底层容量，销毁或实例化 UI 槽位，确保表现层与数据层完全对齐。
        /// </summary>
        public void SyncGridCapacity()
        {
            if (_targetWarehouse == null || _targetWarehouse.Storage == null) return;

            int targetCapacity = _targetWarehouse.Storage.Slots.Length;

            // 扩容：实例化不足的槽位
            while (_uiSlots.Count < targetCapacity)
            {
                GameObject obj = Instantiate(UIInventorySlotPrefab, SlotGridContainer);
                UIInventorySlot slotScript = obj.GetComponent<UIInventorySlot>();
                slotScript.InitSlot(_uiSlots.Count, this);
                _uiSlots.Add(slotScript);
            }

            // 缩容：销毁多余的槽位 (处理玩家塞入模块挤占空间的情况)
            while (_uiSlots.Count > targetCapacity)
            {
                int lastIndex = _uiSlots.Count - 1;
                Destroy(_uiSlots[lastIndex].gameObject);
                _uiSlots.RemoveAt(lastIndex);
            }
        }

        /// <summary>
        /// 遍历刷新所有已生成的 UI 槽位。
        /// 此方法将在后续步骤中通过脏标记或低频轮询被调用。
        /// </summary>
        public void RefreshAllSlots()
        {
            if (_targetWarehouse == null || _targetWarehouse.Storage == null) return;

            for (int i = 0; i < _uiSlots.Count; i++)
            {
                // 将底层数组的只读数据投射给 UI
                _uiSlots[i].RefreshVisuals(_targetWarehouse.Storage.Slots[i]);
            }
        }

        // ==========================================
        // 拖拽残影逻辑与指令下发
        // ==========================================
        public void OnSlotBeginDrag(int slotIndex, Sprite icon)
        {
            _dragSourceIndex = slotIndex;
            if (DragGhost != null && icon != null)
            {
                DragGhost.gameObject.SetActive(true);
                DragGhostImage.sprite = icon;
                DragGhost.transform.SetAsLastSibling(); // 确保渲染在最顶层
                UpdateDragGhostPosition(Input.mousePosition);
            }
        }

        public void OnSlotDrag(PointerEventData eventData)
        {
            if (_dragSourceIndex != -1) UpdateDragGhostPosition(eventData.position);
        }

        public void OnSlotEndDrag()
        {
            _dragSourceIndex = -1;
            if (DragGhost != null) DragGhost.gameObject.SetActive(false);
        }

        public void OnSlotDrop(int dropTargetIndex)
        {
            // 验证：正在拖拽，且没有原地放下
            if (_dragSourceIndex != -1 && _dragSourceIndex != dropTargetIndex)
            {
                // 指令下发：调用底层的安全交互方法
                _targetWarehouse.Storage.InteractSlots(_dragSourceIndex, dropTargetIndex);
                
                // 为了视觉即时反馈，交互完毕立刻强刷一次 UI，忽略定时器
                RefreshAllSlots(); 
            }
        }

        private void UpdateDragGhostPosition(Vector2 screenPos)
        {
            if (DragGhost != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)transform, screenPos, null, out Vector2 localPoint))
            {
                DragGhost.anchoredPosition = localPoint;
            }
        }
        
    }
}