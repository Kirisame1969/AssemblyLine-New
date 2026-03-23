using System;
using System.Collections.Generic;
using UnityEngine;

// 1. 模块的旋转状态
public enum ModuleRotation { R0, R90, R180, R270 }

// 2. 【核心机制】：定义两个相邻格子之间的“物理隔断墙” (无向边)
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

// --- 3. 机箱/外壳数据 (容器) ---
public class MachineShellData
{
    public string ShellID;
    public RectInt Bounds; // 世界坐标下的包围盒 (x,y为左下角)
    
    // 用来存放生成在地上的那 12 个灰色底板 GameObject
    public List<GameObject> FloorVisuals = new List<GameObject>();

    // 【节点锁】：绝对死区。记录机箱内部完全无法放置任何东西的“坏死/承重柱”局部坐标
    public HashSet<Vector2Int> DeadCells = new HashSet<Vector2Int>(); 

    // 【边缘锁】：便当盒隔断。记录机箱内部阻挡模块跨越的墙壁
    public HashSet<InternalWall> PartitionWalls = new HashSet<InternalWall>();

    // 内部已放置的模块全量列表
    public List<MachineModuleData> Modules = new List<MachineModuleData>();

    // ==========================================
    // 【新增】：局域网路由索引表 (用于瞬间物流)
    // ==========================================
    public MachineCoreData MainCore;
    public List<InputPortData> InputPorts = new List<InputPortData>();
    public List<OutputPortData> OutputPorts = new List<OutputPortData>();

    // 【新增】：用来存放机器内部所有模块的贴图 GameObject
    public List<GameObject> ModuleVisuals = new List<GameObject>();

    public MachineShellData(RectInt bounds)
    {
        ShellID = Guid.NewGuid().ToString().Substring(0, 5);
        Bounds = bounds;
    }
}

// --- 4. 异形模块基类 ---
public abstract class MachineModuleData
{
    public MachineShellData ParentShell;
    
    // 模块在机箱内部的局部坐标（始终代表旋转后，模块包围盒的左下角）
    public Vector2Int LocalBottomLeft; 
    public ModuleRotation Rotation = ModuleRotation.R0;

    // 必须实现：获取该模块在当前旋转下，占据的所有【局部坐标】
    public abstract List<Vector2Int> GetOccupiedLocalCells();

    // 必须实现：获取该模块作为一个整体，内部必须保持连通的【接缝】
    public abstract List<InternalWall> GetRequiredInternalConnections();
}

// --- 5. 具体的模块实现示例 ---

// 5.1 标准矩形模块 (支持任意宽高的矩形，支持旋转)
public class RectModuleData : MachineModuleData
{
    private int _baseWidth;
    private int _baseHeight;

    public RectModuleData(int width, int height)
    {
        _baseWidth = width;
        _baseHeight = height;
    }

    public override List<Vector2Int> GetOccupiedLocalCells()
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        // 旋转 90 或 270 度时，宽高互换
        int currentW = (Rotation == ModuleRotation.R90 || Rotation == ModuleRotation.R270) ? _baseHeight : _baseWidth;
        int currentH = (Rotation == ModuleRotation.R90 || Rotation == ModuleRotation.R270) ? _baseWidth : _baseHeight;

        for (int x = 0; x < currentW; x++)
        {
            for (int y = 0; y < currentH; y++)
            {
                cells.Add(new Vector2Int(LocalBottomLeft.x + x, LocalBottomLeft.y + y));
            }
        }
        return cells;
    }

    public override List<InternalWall> GetRequiredInternalConnections()
    {
        List<InternalWall> connections = new List<InternalWall>();
        List<Vector2Int> cells = GetOccupiedLocalCells();

        // 遍历所有占用的格子，如果是相邻的（曼哈顿距离为1），则说明它们之间有一条必须连通的缝隙
        for (int i = 0; i < cells.Count; i++)
        {
            for (int j = i + 1; j < cells.Count; j++)
            {
                int dx = Mathf.Abs(cells[i].x - cells[j].x);
                int dy = Mathf.Abs(cells[i].y - cells[j].y);
                
                if (dx + dy == 1)
                {
                    connections.Add(new InternalWall(cells[i], cells[j]));
                }
            }
        }
        return connections;
    }
}

// 5.2 升级版机器核心 (Machine Core)
public class MachineCoreData : MachineModuleData
{
    // 【新增】：核心当前正在执行的配方图纸
    public RecipeDefinition CurrentRecipe;
    // 【双重库存】：用来存放外侧吃进来的原料，以及加工完毕等待吐出的产物
    public List<ItemData> InputBuffer = new List<ItemData>();
    public List<ItemData> OutputBuffer = new List<ItemData>();
    
    public int MaxBufferSize = 10; 
    public float ProcessingProgress = 0f;

    public override List<Vector2Int> GetOccupiedLocalCells() => new List<Vector2Int> { LocalBottomLeft }; 
    public override List<InternalWall> GetRequiredInternalConnections() => new List<InternalWall>();
}

// 5.3 输入匣 (Input Port)
public class InputPortData : MachineModuleData
{
    // 将模块的四种旋转状态，映射为具体的开口方向
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

    public override List<Vector2Int> GetOccupiedLocalCells() => new List<Vector2Int> { LocalBottomLeft };
    public override List<InternalWall> GetRequiredInternalConnections() => new List<InternalWall>();
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

    // 【白名单路由配置】：未来在 UI 上配置它只允许输出什么物品
    public List<string> AllowedItems = new List<string>(); 

    public override List<Vector2Int> GetOccupiedLocalCells() => new List<Vector2Int> { LocalBottomLeft };
    public override List<InternalWall> GetRequiredInternalConnections() => new List<InternalWall>();
}