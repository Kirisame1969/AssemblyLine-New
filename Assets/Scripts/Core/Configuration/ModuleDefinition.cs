using UnityEngine;
using System.Collections.Generic;
using AssemblyLine.Data.Machine;

public enum ModuleLogicalType
{
    NormalRect, // 普通异形/矩形模块
    Core,       // 机器核心 (处理配方)
    InputPort,  // 输入匣
    OutputPort,  // 输出匣
    ExporterCore,  // 交付仓库核心 (黑洞变现)
    ImporterCore,   // 市场采购终端
    WarehouseCore // 【新增】仓储核心
}

[CreateAssetMenu(fileName = "NewModuleDef", menuName = "Factory/Module Definition")]
public class ModuleDefinition : ScriptableObject
{
    public string ModuleID;
    public string DisplayName;
    
                  

    [Header("物理与逻辑属性")]//(Data层使用)
    public ModuleLogicalType Type = ModuleLogicalType.NormalRect; 
    
    [Tooltip("定义该模块的形状和初始坐标集")]//（例如L型等）
    public ModuleShapeData Shape;

    [Tooltip("该模块提供的所有增益特效")]
    public List<ModuleEffect> Effects = new List<ModuleEffect>();
    
    [Header("视觉表现")]
    [Tooltip("大世界与网格中实际放置时的俯视物理贴图")]
    public Sprite Icon;   
    [Tooltip("UI 左侧列表专用的图标。如果不填，将默认使用上面的 Icon")]
    public Sprite ItemIcon;

    
    //[Header("模块属性")]
    //public ModuleType Type;
    //public ModuleShapeData Shape;

    /// <summary>
    /// 获取侧边栏专用的 UI 图标。为了防止旧数据没填报空指针，这里做了回退保护。
    /// </summary>
    public Sprite GetItemIconOrDefault()
    {
        return ItemIcon != null ? ItemIcon : Icon;
    }


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
            case ModuleLogicalType.ExporterCore://  仓库核心
                newInstance = new ExporterCoreData(); 
                break;
            case ModuleLogicalType.ImporterCore://  购买核心
                newInstance = new ImporterCoreData();
                break;
            case ModuleLogicalType.WarehouseCore: // 【新增分支】仓储核心
                newInstance = new WarehouseCoreData(); 
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