using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 静态配置层：科技节点定义
/// 纯数据驱动，记录科技的依赖拓扑、解锁消耗与解锁奖励。
/// </summary>
[CreateAssetMenu(fileName = "TechNode_", menuName = "Factory/Tech Tree/Tech Node")]
public class TechNodeDefinition : ScriptableObject
{
    [Header("Node Identity (身份标识)")]
    [Tooltip("全局唯一标识符，用于持久化存档与逻辑查阅")]
    public string techID;

    [Tooltip("表现层显示的科技名称")]
    public string techName;

    [TextArea(2, 4)]
    [Tooltip("表现层显示的科技描述")]
    public string description;

    [Tooltip("UI 科技树节点图标")]
    public Sprite techIcon;

    [Header("Topology Dependencies (拓扑依赖)")]
    [Tooltip("必须先解锁该列表中的所有科技，才能研究本科技")]
    public List<TechNodeDefinition> prerequisites = new List<TechNodeDefinition>();

    [Header("Costs (解锁消耗)")]
    [Tooltip("研发该科技需要消耗的金钱")]
    public long unlockCostMoney;

    [Tooltip("研发该科技需要消耗的天赋点")]
    public int unlockCostTalentPoints;

    [Header("Rewards (解锁内容)")]
    [Tooltip("解锁后可供放置的机壳蓝图")]
    public List<MachineShellProfile> unlockedMachineShells = new List<MachineShellProfile>();

    [Tooltip("解锁后可供调用的配方")]
    public List<RecipeDefinition> unlockedRecipes = new List<RecipeDefinition>();

    [Tooltip("解锁后可安装在机壳内的内部模块")]
    public List<ModuleDefinition> unlockedModules = new List<ModuleDefinition>();
}