using UnityEngine;
using System.Collections.Generic;

public enum ModuleLogicalType
{
    NormalRect, // 普通异形/矩形模块
    Core,       // 机器核心 (处理配方)
    InputPort,  // 输入匣
    OutputPort  // 输出匣
}

[CreateAssetMenu(fileName = "NewModuleDef", menuName = "Factory/Module Definition")]
public class ModuleDefinition : ScriptableObject
{
    [Header("基础视觉信息")]//(View层使用)
    public string ModuleID;             
    public string DisplayName;          
    public Sprite Icon;                 

    [Header("物理与逻辑属性")]//(Data层使用)
    public ModuleLogicalType Type = ModuleLogicalType.NormalRect; 
    
    [Tooltip("定义该模块的形状和初始坐标集")]//（例如L型等）
    public ModuleShapeData Shape;

    [Tooltip("该模块提供的所有增益特效")]
    public List<ModuleEffect> Effects = new List<ModuleEffect>();

    // ==========================================
    // 【工厂方法】：将图纸实例化为运行时的 C# 数据对象
    // ==========================================
    public MachineModuleData CreateRuntimeInstance()
    {
        MachineModuleData newInstance;

        switch (Type)
        {
            case ModuleLogicalType.Core:
                newInstance = new MachineCoreData(); 
                break;
            case ModuleLogicalType.InputPort:
                newInstance = new InputPortData();   
                break;
            case ModuleLogicalType.OutputPort:
                newInstance = new OutputPortData();  
                break;
            case ModuleLogicalType.NormalRect:
            default:
                newInstance = new RectModuleData(); // 改为了无参数构造，稍后在数据模型里更新
                break;
        }
        
        // 【关键】：让实例化的模块记住自己的图纸！方便后续读取形状和Buff
        newInstance.Definition = this; 
        return newInstance;
    }
}