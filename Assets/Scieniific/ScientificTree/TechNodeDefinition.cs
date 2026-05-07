using UnityEngine;
using System.Collections.Generic;

// 若你的物品定义在特定命名空间，请取消注释并修改
// using AssemblyLine.Core.Configuration; 

[CreateAssetMenu(fileName = "NewTechNode", menuName = "Factory/Tech Tree/Tech Node")]
public class TechNodeDefinition : ScriptableObject
{
    [Header("基础信息")]
    public string NodeID;
    public string DisplayName;
    [TextArea]
    public string Description;
    public Sprite Icon;

    [Header("拓扑坐标 (Hex)")]
    [Tooltip("六边形轴向坐标 (Q, R)。Q为横轴向右，R为斜下轴。用于纯数学换算界面坐标")]
    public Vector2Int AxialCoords;

    [Header("解锁条件")]
    [Tooltip("需要花费的金钱")]
    public long UnlockCost;

    // ==========================================
    // 【隐性需求预留】：未来特殊物品解锁扩展
    // 目前使用 [HideInInspector] 隐藏，绝对不干涉当前的纯金钱解锁逻辑。
    // 预留在数据结构中，避免未来添加时破坏旧存档结构的二进制对齐。
    // ==========================================
    /*
    [System.Serializable]
    public struct ItemRequirement
    {
        public ItemDefinition Item; 
        public int Amount;
    }
    [HideInInspector]
    public List<ItemRequirement> HiddenRequiredItems = new List<ItemRequirement>();
    */

    [Header("科技依赖 (前置节点)")]
    [Tooltip("必须先解锁以下所有科技，才能解锁本科技")]
    public List<TechNodeDefinition> Prerequisites = new List<TechNodeDefinition>();
}