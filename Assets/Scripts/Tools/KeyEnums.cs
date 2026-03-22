// 定义玩家当前鼠标准备放置的物品类型
public enum BuildableType
{
    None,               // 空手/取消选择
    ConveyorBelt,       // 传送带
    MachineShell_Test,  // 测试用机箱 (比如 3x3 或 3x4)
    Module_Core_1x1,    // 1x1 机器核心
    Module_Rect_2x1,    // 2x1 加速器模块
    Module_Rect_2x2,    // 2x2 大型模块
    Module_InputPort,   // 输入匣
    Module_OutputPort   // 输出匣
}