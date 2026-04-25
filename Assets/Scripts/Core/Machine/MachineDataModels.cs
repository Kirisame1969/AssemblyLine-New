/*
using System;
using System.Collections.Generic;
using UnityEngine;
// 26.4.5
// 【新增】：多配方加工任务队列的独立容器
[System.Serializable]
public class ProcessingQueue
{
    public RecipeDefinition CurrentRecipe;
    public List<ItemData> InputBuffer = new List<ItemData>();
    public List<ItemData> OutputBuffer = new List<ItemData>();
    public float ProcessingProgress = 0f;
    public int MaxBufferSize = 10; 
}

// 1. 模块的旋转状态
public enum ModuleRotation { R0, R90, R180, R270 }

// 2. 【核心机制】：定义两个相邻格子之间的“物理隔断墙” (无向边)
[System.Serializable]
public struct InternalWall : IEquatable<InternalWall>
{
    public Vector2Int CellA;
    public Vector2Int CellB;

    public InternalWall(Vector2Int a, Vector2Int b)
    {
        // 强制规范化：始终保证较小的坐标在前面。
        if (a.x < b.x || (a.x == b.x && a.y < b.y))
        {
            CellA = a; CellB = b;
        }
        else
        {
            CellA = b; CellB = a;
        }
    }

    public bool Equals(InternalWall other)
    {
        return CellA == other.CellA && CellB == other.CellB;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(CellA, CellB);
    }
}
// 26.4.5

// --- 3. 机箱/外壳数据 (修改 MachineShellData) ---
public class MachineShellData
{
    public string ShellID;
    public RectInt Bounds;
    public MachineShellProfile Profile; 

    // 【新增】：当前机器的动态属性面板
    public MachineStats CurrentStats = new MachineStats();

   // 用来存放灰色底板 GameObject
    public List<GameObject> FloorVisuals = new List<GameObject>();

    // 【节点锁】：死区。记录机箱内部完全无法放置任何东西的“坏死/承重柱”局部坐标
    public HashSet<Vector2Int> DeadCells = new HashSet<Vector2Int>(); 

    // 【边缘锁】：隔断。记录机箱内部阻挡模块跨越的墙壁
    public HashSet<InternalWall> PartitionWalls = new HashSet<InternalWall>();

    // 内部已放置的模块全量列表
    public List<MachineModuleData> Modules = new List<MachineModuleData>();

    // 局域网路由索引表 (用于瞬间物流)
    public MachineCoreData MainCore;
    public List<InputPortData> InputPorts = new List<InputPortData>();
    public List<OutputPortData> OutputPorts = new List<OutputPortData>();

    // 用来存放机器内部所有模块的贴图 GameObject
    public List<GameObject> ModuleVisuals = new List<GameObject>();


    public MachineShellData(MachineShellProfile profile, RectInt bounds)
    {
        Profile = profile;
        ShellID = Guid.NewGuid().ToString().Substring(0, 5);
        Bounds = bounds;
    }

    // ==========================================
    // 【核心逻辑】：重新计算机箱当前的所有增益 Buff
    // 每次模块发生变动（装/卸）时调用
    // ==========================================
    public void RecalculateStats()
    {
        CurrentStats.Reset(); // 先清空，重置为 1.0倍速、1个队列

        // 遍历所有已安装的模块
        foreach (var module in Modules)
        {
            if (module.Definition != null && module.Definition.Effects != null)
            {
                // 叠加该模块带来的所有特效
                foreach (var effect in module.Definition.Effects)
                {
                    if (effect.Type == EffectType.AddSpeedMultiplier)
                    {
                        CurrentStats.SpeedMultiplier += effect.Value;
                    }
                    else if (effect.Type == EffectType.AddProcessingQueue)
                    {
                        CurrentStats.MaxProcessingQueues += (int)effect.Value;
                    }
                }
            }
        }

        // 防呆：确保速度倍率不为负数，最低 0.1 倍速
        CurrentStats.SpeedMultiplier = Mathf.Max(0.1f, CurrentStats.SpeedMultiplier);

        // 如果机器里有核心，通知它同步队列数量
        if (MainCore != null)
        {
            MainCore.SyncQueues(CurrentStats.MaxProcessingQueues);
        }

        Debug.Log($"[机器增益更新] {ShellID} 属性刷新 -> 速度: {CurrentStats.SpeedMultiplier}x, 队列数: {CurrentStats.MaxProcessingQueues}");
    }
}

// --- 4. 异形模块基类 (Data 层) ---
public abstract class MachineModuleData
{
    public MachineShellData ParentShell;
    
    // 【修复报错2】：让所有模块实例在运行时都带上自己的图纸！
    public ModuleDefinition Definition; 
    
    // 模块在机箱内部的局部坐标锚点（拖拽时鼠标所在的那个格子的相对坐标）
    public Vector2Int LocalBottomLeft; 
    public ModuleRotation Rotation = ModuleRotation.R0;

    // ==========================================
    // 【架构大升级：泛化的空间计算】
    // 不再需要子类自己去算，基类直接读取图纸里的 Shape！
    // ==========================================
    public virtual List<Vector2Int> GetOccupiedLocalCells()
    {
        List<Vector2Int> result = new List<Vector2Int>();
        
        // 防呆：如果没有图纸或者图纸没配形状，就只占自己的锚点
        if (Definition == null || Definition.Shape == null || Definition.Shape.BaseCells.Count == 0)
        {
            result.Add(LocalBottomLeft);
            return result;
        }

        // 调用我们在上一步写的纯数学引擎，获取当前旋转角度下的相对坐标集
        List<Vector2Int> rotatedCells = Definition.Shape.GetRotatedCells(Rotation);

        // 将相对坐标加上当前的锚点位置，算出实际在机箱内占据的格子
        foreach (Vector2Int cellOffset in rotatedCells)
        {
            result.Add(new Vector2Int(LocalBottomLeft.x + cellOffset.x, LocalBottomLeft.y + cellOffset.y));
        }

        return result;
    }

    // ==========================================
    // 【架构大升级：泛化的内部连通性计算】
    // 自动遍历该异形模块的所有格子，如果挨在一起，就意味着它们之间必须物理连通
    // ==========================================
    public virtual List<InternalWall> GetRequiredInternalConnections()
    {
        List<InternalWall> connections = new List<InternalWall>();
        List<Vector2Int> cells = GetOccupiedLocalCells();

        for (int i = 0; i < cells.Count; i++)
        {
            for (int j = i + 1; j < cells.Count; j++)
            {
                // 计算曼哈顿距离
                int dx = Mathf.Abs(cells[i].x - cells[j].x);
                int dy = Mathf.Abs(cells[i].y - cells[j].y);
                
                // 如果距离刚好是 1，说明两格紧挨着，中间不能有机壳的隔断墙
                if (dx + dy == 1)
                {
                    connections.Add(new InternalWall(cells[i], cells[j]));
                }
            }
        }
        return connections;
    }
}

// --- 5. 具体的模块实现 (现在它们变得极其干净轻量！) ---

// 5.1 标准/异形普通模块 (仅占位，无特殊逻辑)
// 【修复报错1】：去掉了带参数的构造函数，因为尺寸已经交给 Definition 管了
public class RectModuleData : MachineModuleData
{
    // 什么都不用写，全靠基类继承！
}

// 26.4.5
// 5.2 升级版机器核心 (修改 MachineCoreData)
public class MachineCoreData : MachineModuleData
{
    // 【修改】：废弃原先的单一图纸和进度条，转为支持多条流水线
    public List<ProcessingQueue> ActiveQueues = new List<ProcessingQueue>();

    // 构造时默认给予一条基础流水线
    public MachineCoreData()
    {
        ActiveQueues.Add(new ProcessingQueue());
    }

    // 动态扩容或缩容流水线
    public void SyncQueues(int targetQueueCount)
    {
        // 扩容：如果有增加队列的Buff
        while (ActiveQueues.Count < targetQueueCount)
        {
            ActiveQueues.Add(new ProcessingQueue());
        }
        
        // 缩容：如果玩家拆除了多核模块，导致上限下降
        if (ActiveQueues.Count > targetQueueCount)
        {
            // 注意：在实际商业级游戏中，缩容时需要把队列里的原料返还给玩家。
            // 这里为了保持逻辑纯粹，先做直接截断删除。
            ActiveQueues.RemoveRange(targetQueueCount, ActiveQueues.Count - targetQueueCount);
        }
    }
}

// 5.3 输入匣 (Input Port)
public class InputPortData : MachineModuleData
{
    // I/O 匣子特有逻辑：将模块自身的旋转映射为“开口的吸入方向”
    public Direction FacingDir
    {
        get
        {
            switch (Rotation)
            {
                case ModuleRotation.R0: return Direction.Up;
                case ModuleRotation.R90: return Direction.Right;
                case ModuleRotation.R180: return Direction.Down;
                case ModuleRotation.R270: return Direction.Left;
                default: return Direction.Up;
            }
        }
    }
}

// 5.4 输出匣 (Output Port)
public class OutputPortData : MachineModuleData
{
    public Direction FacingDir
    {
        get
        {
            switch (Rotation)
            {
                case ModuleRotation.R0: return Direction.Up;
                case ModuleRotation.R90: return Direction.Right;
                case ModuleRotation.R180: return Direction.Down;
                case ModuleRotation.R270: return Direction.Left;
                default: return Direction.Up;
            }
        }
    }

    public List<string> AllowedItems = new List<string>(); 
}

// ==========================================
// 【新增特殊核心】：交付仓库 (Market Exporter)
// 专门用于吞噬输入物品并转化为全局资金，无产出。
// ==========================================
public class ExporterCoreData : MachineCoreData
{
    // 继承自 MachineCoreData，天生拥有 ActiveQueues 和 InputBuffer。
    // 它不需要任何特殊属性，它的“变现”特权将在 Controller 的多态判定中体现。
}
// ==========================================
// 【新增特殊核心】：市场采购终端 (Market Importer)
// 专门用于消耗资金，无中生有地生成基础原材料。
// ==========================================
public class ImporterCoreData : MachineCoreData
{
    // 采购终端特有属性：它需要知道自己到底在买什么
    public ItemDefinition TargetItem; 
    
    // 进货所需的基础时间（默认 1 秒，受机箱多倍速影响）
    public float ImportTime = 1.0f; 
}
*/