using UnityEngine;
using System.Collections.Generic;

namespace AssemblyLine.Core.Configuration
{
    [CreateAssetMenu(fileName = "NewItemCatalog", menuName = "Factory/Catalogs/Item Catalog")]
    public class ItemCatalog : ScriptableObject
    {
        [Tooltip("游戏中所有的物品定义图纸")]
        public List<ItemDefinition> AllItems = new List<ItemDefinition>();
    }
}