using System.Collections.Generic;
using UnityEngine;
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

        private WarehouseCoreData _targetWarehouse;
        private List<UIInventorySlot> _uiSlots = new List<UIInventorySlot>();

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
                slotScript.InitSlot(_uiSlots.Count);
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
    }
}