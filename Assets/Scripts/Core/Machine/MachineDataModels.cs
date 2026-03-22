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
        // 这样即使输入 (0,1)和(0,0)，也会被存为 A=(0,0), B=(0,1)，方便判断相等。
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
    // 【新增】：用来存放生成在地上的那 12 个灰色底板 GameObject
    public List<GameObject> FloorVisuals = new List<GameObject>();

    // 【节点锁】：绝对死区。记录机箱内部完全无法放置任何东西的“坏死/承重柱”局部坐标
    public HashSet<Vector2Int> DeadCells = new HashSet<Vector2Int>(); 

    // 【边缘锁】：便当盒隔断。记录机箱内部阻挡模块跨越的墙壁
    public HashSet<InternalWall> PartitionWalls = new HashSet<InternalWall>();

    // 内部已放置的模块列表
    public List<MachineModuleData> Modules = new List<MachineModuleData>();

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

// 5.1 核心主板 (固定 1x1)
public class MachineCoreData : MachineModuleData
{
    // 预留的输入输出清单
    // public List<ItemData> InputInventory = new List<ItemData>();
    // public List<ItemData> OutputInventory = new List<ItemData>();

    public override List<Vector2Int> GetOccupiedLocalCells()
    {
        return new List<Vector2Int> { LocalBottomLeft };
    }

    public override List<InternalWall> GetRequiredInternalConnections()
    {
        // 1x1 内部没有缝隙，不需要连通性
        return new List<InternalWall>();
    }
}

// 5.2 标准矩形模块 (支持任意宽高的矩形，支持旋转)
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