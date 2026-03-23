using UnityEngine;
using System.Collections.Generic;

// 辅助结构：用来表示“几个什么物品” (比如：2个铁矿)
[System.Serializable]
public class ItemAmount
{
    public ItemDefinition Item;
    public int Amount;
}

[CreateAssetMenu(fileName = "NewRecipe", menuName = "Factory/Recipe Definition")]
public class RecipeDefinition : ScriptableObject
{
    public string RecipeID;       // 配方代码，如 "smelt_iron_ingot"
    public string DisplayName;    // 显示名称，如 "熔炼铁锭"
    public float ProcessingTime = 2f; // 加工耗时 (Tick 周期)

    public List<ItemAmount> Inputs;   // 原料需求表 (例如：2个铁矿)
    public List<ItemAmount> Outputs;  // 产物输出表 (例如：1个铁块)
}