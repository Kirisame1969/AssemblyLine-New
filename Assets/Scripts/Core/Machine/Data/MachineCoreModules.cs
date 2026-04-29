using System.Collections.Generic;

// 存放具备主动加工、流转和交易算力的“核心”模块数据。
// MachineCoreData 负责维护该机箱的所有加工队列容器；
// 其衍生子类（如进出口核心）则作为标识，供控制层（Controller）在遍历时执行特殊的分支逻辑（如扣款、加钱）。

namespace AssemblyLine.Data.Machine
{
    /// <summary>
    /// 机器核心模块的数据实体。
    /// 负责维护和调度该机器的所有加工任务队列（多流水线支持）。
    /// </summary>
    public class MachineCoreData : MachineModuleData
    {
        /// <summary>
        /// 当前激活的加工任务队列集合。
        /// 每个队列独立处理输入缓冲、输出缓冲和加工进度。
        /// </summary>
        public List<ProcessingQueue> ActiveQueues = new List<ProcessingQueue>();

        /// <summary>
        /// 构造函数。初始化时默认分配一条基础流水线。
        /// </summary>
        public MachineCoreData()
        {
            ActiveQueues.Add(new ProcessingQueue());
        }

        /// <summary>
        /// 同步流水线数量。
        /// 根据当前机箱的全局属性（Buff），动态扩容或截断加工队列。
        /// </summary>
        /// <param name="targetQueueCount">目标队列总数</param>
        public void SyncQueues(int targetQueueCount)
        {
            // 扩容逻辑：补齐差额
            while (ActiveQueues.Count < targetQueueCount)
            {
                ActiveQueues.Add(new ProcessingQueue());
            }
            
            // 缩容逻辑：截断多余的队列
            // 提示：当前采用直接截断，后续可在此处追加将余料退回大世界的逻辑
            if (ActiveQueues.Count > targetQueueCount)
            {
                ActiveQueues.RemoveRange(targetQueueCount, ActiveQueues.Count - targetQueueCount);
            }
        }
    }

    /// <summary>
    /// 交付仓库（市场出口）的数据实体。
    /// 继承自 MachineCoreData，无独立属性。
    /// 在 SimulationController 结算时，其存在即代表执行“吞噬物品并增加资金”的逻辑。
    /// </summary>
    public class ExporterCoreData : MachineCoreData
    {
        // 依托类型多态性作为判定标识，无需追加额外属性。
    }

    /// <summary>
    /// 采购终端（市场入口）的数据实体。
    /// 记录需要从全局资金中兑换的基础原材料类型及进货频率。
    /// </summary>
    public class ImporterCoreData : MachineCoreData
    {
        /// <summary>
        /// 当前绑定的采购目标物品。
        /// </summary>
        public ItemDefinition TargetItem; 
        
        /// <summary>
        /// 单次进货所需的基础时间（秒）。受机箱全局速度倍率影响。
        /// </summary>
        public float ImportTime = 1.0f; 
    }

    /// <summary>
    /// 仓储核心的数据实体。
    /// 赋予机箱大容量静态存储的能力。不参与配方加工。
    /// </summary>
    public class WarehouseCoreData : MachineCoreData
    {
        /// <summary>
        /// 仓库的深度库存数据。
        /// </summary>
        public InventoryData Storage;

        // 玩家在 UI 上设定的全局市场供货优先级 (默认 0)
        public int MarketPriority = 0; 

        // 该仓库落成时的 Tick 时间戳。用于兜底排序，保证确定的先入先出
        public ulong BuildTick = 0;

        /// <summary>
        /// 仓储核心的构造。默认初始化 0 槽位，实际容量将在装配后由 RecalculateStats 动态计算。
        /// 默认拥有一条 ActiveQueue，用作 I/O 缓冲。
        /// </summary>
        public WarehouseCoreData() : base()
        {
            Storage = new InventoryData(0);
        }
    }

}