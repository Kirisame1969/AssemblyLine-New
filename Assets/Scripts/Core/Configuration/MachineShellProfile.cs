using UnityEngine;
using System.Collections.Generic;
using AssemblyLine.Data.Machine;

// 定义单个格子在 UI 面板中的视觉配置
[System.Serializable]
public struct CellVisualConfig
{
    [Tooltip("逻辑坐标 (如 0,0)")]
    public Vector2Int LogicalPos;
    
    [Tooltip("UI中心相对偏移量")]
    public Vector2 VisualOffset;

    [Tooltip("四面是否与相邻的可用网格连通（用于淡化内部边框，形成大房间感）")]
    public bool ConnectTop;
    public bool ConnectBottom;
    public bool ConnectLeft;
    public bool ConnectRight;
}

[CreateAssetMenu(fileName = "NewShellProfile", menuName = "Factory/Machine Shell Profile")]
public class MachineShellProfile : ScriptableObject
{
    [Header("基础信息")]
    public string ProfileID;        
    public string DisplayName;      

    [Header("物理约束 (Data层核心碰撞使用)")]
    [Tooltip("机壳内逻辑网格的最大宽度 (用于遍历)")]
    public int LogicWidth = 3;      
    [Tooltip("机壳内逻辑网格的最大高度 (用于遍历)")]
    public int LogicHeight = 4;     
    
    [Tooltip("绝对死区局部坐标（不可放置任何东西）")]
    public List<Vector2Int> DeadCells = new List<Vector2Int>();
    
    [Tooltip("便当盒隔断墙：定义阻挡模块跨越的内部墙壁")]
    public List<InternalWall> PartitionWalls = new List<InternalWall>();

    [Header("GUI视觉映射 (View层UI面板使用)")]
    [Tooltip("为每一个有效的逻辑格子配置 UI 表现参数")]
    public List<CellVisualConfig> VisualConfigs = new List<CellVisualConfig>();
}