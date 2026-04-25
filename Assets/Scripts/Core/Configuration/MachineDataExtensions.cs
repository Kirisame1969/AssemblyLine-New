using System;
using System.Collections.Generic;
using UnityEngine;
using AssemblyLine.Data.Machine;
// ==========================================
// 1. 机器当前属性面板 (Data层契约)
// 用于汇总当前机箱内所有模块提供的 Buff
// ==========================================
public class MachineStats
{
    public float SpeedMultiplier = 1.0f; // 加工速度倍率 (默认 1.0)
    public int MaxProcessingQueues = 1;  // 最大并行加工队列数 (默认 1)

    // 重置为基础属性（在每次重新计算前调用）
    public void Reset()
    {
        SpeedMultiplier = 1.0f;
        MaxProcessingQueues = 1;
    }
}

// ==========================================
// 2. 模块特效定义 (Config层使用)
// ==========================================
public enum EffectType
{
    AddSpeedMultiplier,   // 增加速度倍率 (如 0.5 代表加速 50%)
    AddProcessingQueue,   // 增加并行处理队列数 (如 1 代表多一条流水线)
    // 未来可扩展：ReduceEnergyCost, IncreaseStorage...
}

[Serializable]
public struct ModuleEffect
{
    [Tooltip("特效的类型")]
    public EffectType Type;
    [Tooltip("特效的数值")]
    public float Value;
}

// ==========================================
// 3. 异形模块形状与旋转引擎 (纯数学逻辑)
// ==========================================
[Serializable]
public class ModuleShapeData
{
    [Tooltip("模块在 0度 旋转下占用的所有局部坐标（务必包含一个 0,0 作为拖拽锚点）")]
    public List<Vector2Int> BaseCells = new List<Vector2Int> { Vector2Int.zero };

    // 【核心引擎】：根据当前的旋转枚举，计算出实际占用的物理坐标
    public List<Vector2Int> GetRotatedCells(ModuleRotation rotation)
    {
        List<Vector2Int> rotatedCells = new List<Vector2Int>();

        foreach (Vector2Int cell in BaseCells)
        {
            Vector2Int newPos = cell;
            // 二维旋转矩阵计算
            switch (rotation)
            {
                case ModuleRotation.R0:
                    newPos = new Vector2Int(cell.x, cell.y);
                    break;
                case ModuleRotation.R90:
                    // 顺时针旋转 90度: (x,y) -> (y, -x)
                    newPos = new Vector2Int(cell.y, -cell.x);
                    break;
                case ModuleRotation.R180:
                    // 顺时针旋转 180度: (x,y) -> (-x, -y)
                    newPos = new Vector2Int(-cell.x, -cell.y);
                    break;
                case ModuleRotation.R270:
                    // 顺时针旋转 270度: (x,y) -> (-y, x)
                    newPos = new Vector2Int(-cell.y, cell.x);
                    break;
            }
            rotatedCells.Add(newPos);
        }
        return rotatedCells;
    }
}