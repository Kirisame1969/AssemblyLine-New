using System;
using System.Collections.Generic;
using UnityEngine;

// 作为机器实体的根数据节点。
// 负责存储机箱的物理边界、死区/隔断墙限制、汇总内部安装的所有模块实例，并维护关键模块（核心、输入输出端口）的路由索引。
namespace AssemblyLine.Data.Machine
{
    /// <summary>
    /// 机器的动态属性数据包。
    /// 记录机器在当前模块组合下的运行倍率与加工队列数量。
    /// </summary>
    [Serializable]
    public class MachineStats
    {
        public float SpeedMultiplier = 1.0f;
        public int MaxProcessingQueues = 1;

        /// <summary>
        /// 重置为无任何增益的基础状态。
        /// </summary>
        public void Reset()
        {
            SpeedMultiplier = 1.0f;
            MaxProcessingQueues = 1;
        }
    }

    /// <summary>
    /// 机器外壳（机箱）的数据容器。
    /// 维护机器内部的网格状态、已安装模块列表及路由索引。不包含表现层组件的引用。
    /// </summary>
    public class MachineShellData
    {
        public string ShellID;
        
        /// <summary>
        /// 机箱在大世界中的包围盒坐标与尺寸。
        /// </summary>
        public RectInt Bounds;
        
        /// <summary>
        /// 指向该机箱对应的静态配置数据。
        /// </summary>
        public MachineShellProfile Profile; 

        /// <summary>
        /// 当前机箱汇总计算后的动态属性。
        /// </summary>
        public MachineStats CurrentStats = new MachineStats();

        /// <summary>
        /// 机箱内部的死区坐标集合，不允许放置任何模块。
        /// </summary>
        public HashSet<Vector2Int> DeadCells = new HashSet<Vector2Int>(); 

        /// <summary>
        /// 机箱内部的物理隔断墙集合，限制多格模块的跨越。
        /// </summary>
        public HashSet<InternalWall> PartitionWalls = new HashSet<InternalWall>();

        /// <summary>
        /// 内部已放置的所有模块的全量列表。
        /// </summary>
        public List<MachineModuleData> Modules = new List<MachineModuleData>();

        // ==========================================
        // 局域网路由索引表
        // 缓存关键模块的引用，供外部（如 Controller）快速访问，避免遍历 Modules 列表。
        // ==========================================
        public MachineCoreData MainCore;
        public List<InputPortData> InputPorts = new List<InputPortData>();
        public List<OutputPortData> OutputPorts = new List<OutputPortData>();

        /// <summary>
        /// 初始化机箱数据实体。
        /// </summary>
        /// <param name="profile">机箱的静态配置</param>
        /// <param name="bounds">大世界中的包围盒</param>
        public MachineShellData(MachineShellProfile profile, RectInt bounds)
        {
            Profile = profile;
            ShellID = Guid.NewGuid().ToString().Substring(0, 5); // 生成简易唯一标识
            Bounds = bounds;
        }

        /// <summary>
        /// 重新计算机箱当前的增益属性。
        /// 在模块发生安装或拆卸时由管理层调用。
        /// </summary>
        public void RecalculateStats()
        {
            CurrentStats.Reset(); 

            // 遍历并叠加所有模块提供的增益效果
            foreach (var module in Modules)
            {
                if (module.Definition != null && module.Definition.Effects != null)
                {
                    foreach (var effect in module.Definition.Effects)
                    {
                        if (effect.Type == EffectType.AddSpeedMultiplier)
                        {
                            CurrentStats.SpeedMultiplier += effect.Value;
                        }
                        else if (effect.Type == EffectType.AddProcessingQueue)
                        {
                            CurrentStats.MaxProcessingQueues += (int)effect.Value;
                        }
                    }
                }
            }

            // 限制最低运行速度，避免负数或零导致逻辑异常
            CurrentStats.SpeedMultiplier = Mathf.Max(0.1f, CurrentStats.SpeedMultiplier);

            // 同步队列数量限制至核心模块
            if (MainCore != null)
            {
                MainCore.SyncQueues(CurrentStats.MaxProcessingQueues);
            }

            Debug.Log($"[数据层] {ShellID} 属性已刷新 -> 速度: {CurrentStats.SpeedMultiplier}x, 队列数: {CurrentStats.MaxProcessingQueues}");
        }
    }
}