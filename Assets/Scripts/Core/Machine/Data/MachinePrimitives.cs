using System;
using System.Collections.Generic;
using UnityEngine; // 仅用于 Vector2Int 数学结构

// 定义构建工厂机器系统不可或缺的基础“砖块”-------纯数值结构体、枚举和数据包容器。
// 它们不包含任何主动的业务逻辑，仅负责静态数据的存储与物理/几何形态的数学表达。

namespace AssemblyLine.Data.Machine
{
    /// <summary>
    /// 【数据容器】加工任务队列
    /// 用于存放单条流水线的当前配方、输入/输出缓冲区以及加工进度。
    /// 剥离了 UI 进度条或物理实体的概念，仅作为内存状态机。
    /// </summary>
    [Serializable]
    public class ProcessingQueue
    {
        // 当前队列正在执行的加工配方图纸
        public RecipeDefinition CurrentRecipe;
        
        // 已进入机器但尚未加工的原材料缓冲池
        // 输入缓存区
        public List<ItemData> InputBuffer = new List<ItemData>();
        
        // 已加工完成但尚未被传送带或输出匣抽走的产物缓冲池
        // 输出缓存区
        public List<ItemData> OutputBuffer = new List<ItemData>();
        
        // 当前加工进度 (0f 到 Recipe.ProcessingTime)
        public float ProcessingProgress = 0f;
        
        // 缓冲池的容量上限
        public int MaxBufferSize = 10; 
    }

    /// <summary>
    /// 【枚举】模块的旋转状态
    /// 相间顺时针 90 度。用于驱动旋转算法，也决定异形模块最终占据的空间坐标。
    /// </summary>
    public enum ModuleRotation 
    { 
        R0,   // 0度 (默认朝向)
        R90,  // 顺时针90度
        R180, // 顺时针180度
        R270  // 顺时针270度
    }

    /// <summary>
    /// 【核心几何结构】内部物理隔断墙
    /// 这是一个无向边结构，用于描述机箱内部两个相邻网格之间是否存在物理阻挡。
    /// 决定异形模块（如占用 2x1 空间的模块）能否被成功放置。
    /// </summary>
    [Serializable]
    public struct InternalWall : IEquatable<InternalWall>
    {
        // 墙壁两侧的两个相邻单元格的局部坐标
        public Vector2Int CellA;
        public Vector2Int CellB;
        /// <summary>
        /// 构造一堵隔断墙。
        /// 包含了防呆逻辑，确保 (A,B) 和 (B,A) 被视为同一堵墙。
        /// </summary>
        public InternalWall(Vector2Int a, Vector2Int b)
        {
            // 强制规范化：始终保证 X 坐标较小的在前面；若 X 相同，则 Y 坐标较小的在前面。
            // 这确保了无论从哪边传入坐标，生成的 HashCode 都是一致的。
            if (a.x < b.x || (a.x == b.x && a.y < b.y))
            {
                CellA = a; 
                CellB = b;
            }
            else
            {
                CellA = b; 
                CellB = a;
            }
        }

        // 实现 IEquatable 接口，避免装箱拆箱，提升在 HashSet/Dictionary 中的查找性能
        public bool Equals(InternalWall other)
        {
            return CellA == other.CellA && CellB == other.CellB;
        }

        // 联合生成哈希值，确保 HashSet<InternalWall> 的极速查找
        public override int GetHashCode()
        {
            return HashCode.Combine(CellA, CellB);
        }
    }
}