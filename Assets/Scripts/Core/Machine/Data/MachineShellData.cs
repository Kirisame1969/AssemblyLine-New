using System;
using System.Collections.Generic;
using UnityEngine;

// 作为机器实体的根数据节点。
// 负责存储机箱的物理边界、死区/隔断墙限制、汇总内部安装的所有模块实例，并维护关键模块（核心、输入输出端口）的路由索引。

/*
(注意：在 ModuleEffect 的枚举类型 EffectType 中，建议你以后追加一个 AddStorageCapacity，
并在现有的 RecalculateStats 的 foreach 遍历中对其进行累加 CurrentStats.ExtraStorageCapacity += value。
这里为了集中精力，我们先聚焦核心逻辑。)
*/

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
        public int ExtraStorageCapacity = 0; // 【新增】扩展模块提供的额外容量

        /// <summary>
        /// 重置为无任何增益的基础状态。
        /// </summary>
        public void Reset()
        {
            SpeedMultiplier = 1.0f;
            MaxProcessingQueues = 1;
            ExtraStorageCapacity = 0;
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


            // ... [以上为增益计算逻辑] ...

            // 【新增】：仓储核心动态容量推算机制
            if (MainCore is WarehouseCoreData warehouseCore)
            {
                // 1. 计算总面积与占用
                int totalArea = Bounds.width * Bounds.height;
                int occupiedCells = 0;
                
                foreach (var module in Modules)
                {
                    // 使用已有的 GetOccupiedLocalCells 方法获取占据格子数
                    occupiedCells += module.GetOccupiedLocalCells().Count;
                }

                // 2. 纯网格数学计算空闲格子数 (完全兼容你预期的“不对齐”现象)
                int freeCells = totalArea - DeadCells.Count - occupiedCells;
                freeCells = Math.Max(0, freeCells); // 使用 System.Math.Max

                // 3. 推算总容量
                int baseStoragePerCell = 1; // 兜底默认值
                if (warehouseCore.Definition is WarehouseCoreDefinition whDef)
                {
                    baseStoragePerCell = whDef.BaseStoragePerFreeCell;
                }

                int targetCapacity = (freeCells * baseStoragePerCell) + CurrentStats.ExtraStorageCapacity;

                // 4. 执行库存容量重构与溢出接管
                List<InventorySlot> spilled = warehouseCore.Storage.Resize(targetCapacity);
                
                // 5. 溢出物临时销毁处理
                if (spilled.Count > 0)
                {
                    string log = $"[数据层] 仓库 {ShellID} 缩容至 {targetCapacity}。因空间不足直接销毁了以下溢出物：\n";
                    foreach (var drop in spilled)
                    {
                        log += $"- {drop.ItemType.DisplayName} x{drop.Count}\n";
                    }
                    Debug.LogWarning(log);
                    // 未来若有掉落物系统，可在此处请求 SimulationController 抛出实体
                }

                Debug.Log($"[数据层] {ShellID} 仓储容量重算完毕。空闲格子:{freeCells}, 目标容量:{targetCapacity}");
            }        
        }
    }
}