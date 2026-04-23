using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 静态配置层：科技树拓扑总表
/// 作为唯一数据入口，供 TechManager 和 TechTreeGUIController 抓取所有可用节点。
/// </summary>
[CreateAssetMenu(fileName = "TechTreeTopology", menuName = "Factory/Tech Tree/Topology")]
public class TechTreeTopology : ScriptableObject
{
    [Header("All Tech Nodes")]
    [Tooltip("注册游戏内所有的科技节点配置")]
    public List<TechNodeDefinition> allNodes = new List<TechNodeDefinition>();
}