using UnityEngine;

// 这个标签让你可以在 Unity 里右键 -> Create -> Factory -> Item Definition
[CreateAssetMenu(fileName = "NewItem", menuName = "Factory/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    public string ItemID;         // 唯一代码，如 "iron_ore"
    public string DisplayName;    // 游戏内显示的名称，如 "铁矿石"
    public Sprite Icon;           // 【关键】物品在传送带上真正长什么样
    
    // 未来你还可以加：最大堆叠数、物品描述、价值等...
}