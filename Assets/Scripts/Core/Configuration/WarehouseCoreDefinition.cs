using UnityEngine;

// 保持与 ModuleDefinition 一致的命名空间（如果有的话），或者放在全局
[CreateAssetMenu(fileName = "NewWarehouseCore", menuName = "Factory/Warehouse Core Definition")]
public class WarehouseCoreDefinition : ModuleDefinition
{
    [Header("仓储核心特殊属性")]
    [Tooltip("机箱内每 1 个空闲格子，能提供多少个物品槽位")]
    public int BaseStoragePerFreeCell = 2;

    private void OnValidate()
    {
        Type = ModuleLogicalType.WarehouseCore;
    }
}