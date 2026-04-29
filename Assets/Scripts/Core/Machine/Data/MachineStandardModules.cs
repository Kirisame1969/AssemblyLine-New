using System.Collections.Generic;

// 存放不具备核心调度能力的普通模块数据实体。
// 包含负责跨界物流吞吐的输入/输出匣子（I/O Ports），以及仅提供物理占位或静态增益的常规模块（RectModuleData）。
// 将继承自基类的旋转枚举（Rotation）转换为具体的物理朝向（Direction），供控制层在验证大世界连通性时使用。

namespace AssemblyLine.Data.Machine
{
    /// <summary>
    /// 端口吞吐规则配置。
    /// 用于控制输入/输出匣的行为参数，支持后续由 UI 面板动态修改。
    /// </summary>
    [System.Serializable]
    public class PortRuleConfig
    {
        /// <summary>
        /// 允许吞吐的物品白名单集合（图纸名或 ID）。为空则代表接受任何物品。
        /// </summary>
        public HashSet<string> Whitelist = new HashSet<string>();

        /// <summary>
        /// 每 Tick 允许的最大吞吐量。用于实现高级端口的速率限制。
        /// 默认为 1。
        /// </summary>
        public int MaxThroughputPerTick = 1;

        /// <summary>
        /// 多仓库协同交割时的供货优先级（针对输出端口）。数字越大优先级越高。
        /// </summary>
        public int MarketPriority = 0;

        /// <summary>
        /// 检查指定物品是否被允许通过该端口。
        /// </summary>
        public bool IsAllowed(ItemDefinition item)
        {
            //空值校验
            if (item == null) return false;
            //列表为空视作无过滤规则，全部放行
            if (Whitelist.Count == 0) return true;
            //校验是否在白名单中
            return Whitelist.Contains(item.ItemID); // ItemDefinition.ItemID 作为唯一标识
        }
    }




    /// <summary>
    /// 输入匣（Input Port）的数据实体。
    /// 负责接收大世界传送带的物品并将其送入机器核心的加工队列。
    /// </summary>
    public class InputPortData : MachineModuleData
    {
        public PortRuleConfig Rules = new PortRuleConfig();
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
        public PortRuleConfig Rules = new PortRuleConfig();
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