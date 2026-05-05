using UnityEngine;
using System.Collections.Generic;

namespace AssemblyLine.Core.Configuration
{
    [CreateAssetMenu(fileName = "NewRecipeCatalog", menuName = "Factory/Catalogs/Recipe Catalog")]
    public class RecipeCatalog : ScriptableObject
    {
        [Tooltip("游戏中所有的配方定义图纸")]
        public List<RecipeDefinition> AllRecipes = new List<RecipeDefinition>();
    }
}