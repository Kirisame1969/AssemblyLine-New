using System.Collections.Generic;

// 存放不具备核心调度能力的普通模块数据实体。
// 包含负责跨界物流吞吐的输入/输出匣子（I/O Ports），以及仅提供物理占位或静态增益的常规模块（RectModuleData）。
// 将继承自基类的旋转枚举（Rotation）转换为具体的物理朝向（Direction），供控制层在验证大世界连通性时使用。

namespace AssemblyLine.Data.Machine
{
    /// <summary>
    /// 输入匣（Input Port）的数据实体。
    /// 负责接收大世界传送带的物品并将其送入机器核心的加工队列。
    /// </summary>
    public class InputPortData : MachineModuleData
    {
        /// <summary>
        /// 获取输入匣的物理吸入朝向。
        /// 通过映射模块当前的旋转状态（Rotation）得出绝对方向。
        /// </summary>
        public Direction FacingDir
        {
            get
            {
                switch (Rotation)
                {
                    case ModuleRotation.R0: return Direction.Up;
                    case ModuleRotation.R90: return Direction.Right;
                    case ModuleRotation.R180: return Direction.Down;
                    case ModuleRotation.R270: return Direction.Left;
                    default: return Direction.Up;
                }
            }
        }
    }

    /// <summary>
    /// 输出匣（Output Port）的数据实体。
    /// 负责将机器核心加工完毕的产物吐出到大世界的传送带上。
    /// </summary>
    public class OutputPortData : MachineModuleData
    {
        /// <summary>
        /// 获取输出匣的物理吐出朝向。
        /// </summary>
        public Direction FacingDir
        {
            get
            {
                switch (Rotation)
                {
                    case ModuleRotation.R0: return Direction.Up;
                    case ModuleRotation.R90: return Direction.Right;
                    case ModuleRotation.R180: return Direction.Down;
                    case ModuleRotation.R270: return Direction.Left;
                    default: return Direction.Up;
                }
            }
        }

        /// <summary>
        /// 允许输出的物品白名单（过滤器）。
        /// 若列表为空，则默认允许输出所有产物。
        /// </summary>
        public List<string> AllowedItems = new List<string>(); 
    }

    /// <summary>
    /// 标准占位模块（或基础异形模块）的数据实体。
    /// 仅具有空间尺寸、隔断和全局增益属性，无主动交互逻辑。
    /// </summary>
    public class RectModuleData : MachineModuleData
    {
        // 依托基类 MachineModuleData 的 Definition 进行空间计算，内部无需增加额外字段。
    }
}