using System;
using System.Collections.Generic;
using UnityEngine; // 仅使用基础数据结构 Vector2Int, RectInt, ColorUtility(如果需要)

namespace AssemblyLine.Data.SaveData
{
    // ==========================================
    // 基础物流与网格 DTO
    // ==========================================
    
    [Serializable]
    public class ItemSaveData
    {
        public string ItemID; 
        public float Progress;
    }

    [Serializable]
    public class BeltSaveData
    {
        public int Dir; 
        public string ParentStripID; 
    }

    [Serializable]
    public class GridCellSaveData
    {
        public Vector2Int GridPosition;
        public BeltSaveData Belt;
        public ItemSaveData Item;
        public bool[] CutEdges;
    }

    [Serializable]
    public class StripSaveData
    {
        public string StripID;
        public float MoveSpeed;
        public List<Vector2Int> Cells;
        public string StripColorHex; 
    }

    // ==========================================
    // 仓储与加工队列 DTO
    // ==========================================

    [Serializable]
    public class InventorySlotSaveData
    {
        public string ItemID;
        public int Count;
    }

    [Serializable]
    public class InventorySaveData
    {
        public InventorySlotSaveData[] Slots;
        public int MaxStackPerSlot;
    }

    [Serializable]
    public class ProcessingQueueSaveData
    {
        public string RecipeID; 
        public List<ItemSaveData> InputBuffer;
        public List<ItemSaveData> OutputBuffer;
        public float ProcessingProgress;
        public int MaxBufferSize;
    }

    [Serializable]
    public class PortRuleConfigSaveData
    {
        public List<string> Whitelist; 
        public int MaxThroughputPerTick;
        public int MarketPriority;
    }

    // ==========================================
    // 机器与模块核心 DTO (扁平化设计)
    // ==========================================

    [Serializable]
    public class ModuleSaveData
    {
        public string ModuleType; // 记录真实的 C# 类名（如 "WarehouseCoreData"）
        public string ModuleDefinitionID; 
        public Vector2Int LocalBottomLeft;
        public int Rotation; 

        // --- 多态子类字段 ---
        public List<ProcessingQueueSaveData> ActiveQueues;
        public InventorySaveData Storage;
        public int MarketPriority;
        public ulong BuildTick;
        public string TargetItemID;
        public float ImportTime;
        public PortRuleConfigSaveData Rules;
        
    }

    [Serializable]
    public class MachineShellSaveData
    {
        public string ShellID;
        public string ProfileID; 
        // 【核心修复】：将 RectInt 扁平化为 4 个基础整形，杜绝引擎类的循环引用
        public int BoundsX;
        public int BoundsY;
        public int BoundsWidth;
        public int BoundsHeight;
        public List<ModuleSaveData> Modules;
    }

    // ==========================================
    // 存档根节点 DTO
    // ==========================================
    [Serializable]
    public class GameSnapshotData
    {
        public string Version;
        public long PlayerFunds; // 玩家资金
        public List<StripSaveData> Strips;
        public List<MachineShellSaveData> Machines;
        public List<GridCellSaveData> ActiveGridCells; 
    }
}