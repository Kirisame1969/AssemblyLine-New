using UnityEngine;
using System.Collections.Generic;

// 供策划配置的“可用模块商品目录”
[CreateAssetMenu(fileName = "New Module Catalog", menuName = "Factory/Module Catalog")]
public class ModuleCatalog : ScriptableObject
{
    [Tooltip("这里放入所有你想在左侧侧边栏显示的模块图纸")]
    public List<ModuleDefinition> AvailableModules = new List<ModuleDefinition>();
}