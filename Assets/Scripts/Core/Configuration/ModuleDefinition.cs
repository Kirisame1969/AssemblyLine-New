using UnityEngine;
using System.Collections.Generic;

// 定义模块的底层业务类型，方便系统知道该实例化哪个 C# 类
public enum ModuleLogicalType
{
    NormalRect, // 普通矩形模块
    Core,       // 机器核心
    InputPort,  // 输入匣
    OutputPort  // 输出匣
}

[CreateAssetMenu(fileName = "NewModuleDef", menuName = "Factory/Module Definition")]
public class ModuleDefinition : ScriptableObject
{
    [Header("基础信息")]
    public string ModuleID;             // 唯一ID，如 "module_core_basic"
    public string DisplayName;          // UI上显示的名称，如 "初级处理核心"
    public Sprite Icon;                 // UI侧边栏和机器面板里显示的精美贴图

    [Header("物理属性")]
    public Vector2Int Size = new Vector2Int(1, 1); // 模块的逻辑宽高尺寸
    public ModuleLogicalType Type = ModuleLogicalType.NormalRect; // 模块类型

    // ==========================================
    // 【架构亮点：工厂方法】
    // View层只认识这个图纸(ScriptableObject)。当玩家从UI拖拽它到机器里松手时，
    // 调用这个方法，就能安全地生成纯 C# 的底层逻辑数据！
    // ==========================================
    public MachineModuleData CreateRuntimeInstance()
    {
        switch (Type)
        {
            case ModuleLogicalType.Core:
                return new MachineCoreData(); // 实例化核心
            case ModuleLogicalType.InputPort:
                return new InputPortData();   // 实例化输入匣
            case ModuleLogicalType.OutputPort:
                return new OutputPortData();  // 实例化输出匣
            case ModuleLogicalType.NormalRect:
            default:
                return new RectModuleData(Size.x, Size.y); // 实例化普通多格模块
        }
    }
}