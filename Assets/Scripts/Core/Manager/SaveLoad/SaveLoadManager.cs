using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using AssemblyLine.Data;
using AssemblyLine.Data.SaveData;
using AssemblyLine.Data.Machine;

namespace AssemblyLine.Core.Manager.SaveLoad
{
    /// <summary>
    /// 控制层：存读档总线。
    /// 负责聚合全图数据生成快照，或解析快照并执行严谨的“清空->重绑定”双轨复原逻辑。
    /// 绝对不包含任何表现层 (View/UI) 代码。
    /// </summary>
    public class SaveLoadManager : MonoBehaviour
    {
        public static SaveLoadManager Instance { get; private set; }

        // 定义存档存放路径：操作系统持久化目录
        private string SaveDirectory => Path.Combine(Application.persistentDataPath, "Saves");

        // 全局广播事件：当读档完成，底层数据已全部就位时触发。供表现层 (UI/大世界) 监听以进行画面重绘
        public event Action OnSimulationDataRestored;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            if (!Directory.Exists(SaveDirectory))
            {
                Directory.CreateDirectory(SaveDirectory);
            }
        }

        // ==========================================
        // 核心功能：保存游戏
        // ==========================================
        public void SaveGame(string slotName = "AutoSave")
        {
            Debug.Log($"[SaveLoadManager] 开始保存快照至槽位: {slotName}...");
            
            GameSnapshotData snapshot = new GameSnapshotData
            {
                Version = "1.0.0",
                PlayerFunds = EconomyManager.Instance.EconomyData.Funds,
                Strips = new List<StripSaveData>(),
                Machines = new List<MachineShellSaveData>(),
                ActiveGridCells = new List<GridCellSaveData>()
            };

            // 1. 抓取所有机器切片
            foreach (var shell in MachineManager.Instance.AllActiveShells)
            {
                snapshot.Machines.Add(shell.ToSaveData());
            }

            // 2. 抓取所有条带切片
            if (StripManager.Instance != null)
            {
                foreach (var strip in StripManager.Instance.ActiveStrips)
                {
                    snapshot.Strips.Add(strip.ToSaveData());
                }
            }

            // 3. 稀疏矩阵抓取：仅保存有状态的网格（有物品、有履带、或被切断的）
            GridCell[,] allCells = GridManager.Instance.GetAllCells();
            int width = allCells.GetLength(0);
            int height = allCells.GetLength(1);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    GridCell cell = allCells[x, y];
                    bool hasCutEdge = cell.CutEdges[0] || cell.CutEdges[1] || cell.CutEdges[2] || cell.CutEdges[3];
                    
                    if (cell.Belt != null || cell.Item != null || hasCutEdge)
                    {
                        var cellSave = new GridCellSaveData
                        {
                            GridPosition = cell.GridPosition,
                            CutEdges = (bool[])cell.CutEdges.Clone(),
                            Item = cell.Item?.ToSaveData(),
                            Belt = cell.Belt != null ? new BeltSaveData { Dir = (int)cell.Belt.Dir, ParentStripID = cell.Belt.ParentStrip?.StripID } : null
                        };
                        snapshot.ActiveGridCells.Add(cellSave);
                    }
                }
            }

            // 执行文件写入 (生产环境中此步可放入 Task.Run 异步防卡死)
            string filePath = Path.Combine(SaveDirectory, $"{slotName}.json");
            // 【核心修复】：追加 JsonSerializerSettings，强制忽略任何未知的循环引用
            JsonSerializerSettings settings = new JsonSerializerSettings { 
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore 
            };
            string json = JsonConvert.SerializeObject(snapshot, Formatting.None, settings);
            File.WriteAllText(filePath, json);

            Debug.Log($"[SaveLoadManager] 存档成功！体积: {json.Length / 1024} KB。路径: {filePath}");
        }

        // ==========================================
        // 核心功能：加载游戏 (双轨重建机制)
        // ==========================================
        public void LoadGame(string slotName = "AutoSave")
        {
            string filePath = Path.Combine(SaveDirectory, $"{slotName}.json");
            if (!File.Exists(filePath))
            {
                Debug.LogError($"[SaveLoadManager] 存档文件不存在: {filePath}");
                return;
            }

            string json = File.ReadAllText(filePath);
            GameSnapshotData snapshot = JsonConvert.DeserializeObject<GameSnapshotData>(json);

            // ----------------------------------------------------
            // 准备期：热重置当前世界，防止数据堆叠
            // ----------------------------------------------------
            GridManager.Instance.ClearAll();
            MachineManager.Instance.ClearAll();
            if (StripManager.Instance != null) StripManager.Instance.ClearAll();

            // ----------------------------------------------------
            // 恢复经济数据
            // ----------------------------------------------------
            EconomyManager.Instance.EconomyData.CurrentCycleExpenses = 0;
            EconomyManager.Instance.EconomyData.CurrentCycleRevenue = 0;
            
            // 使用新接口：直接注入目标金额并确保事件必定被广播到 UI 层
            EconomyManager.Instance.SetFunds(snapshot.PlayerFunds);

            // ----------------------------------------------------
            // Phase 1：实例化条带 (Strip)
            // ----------------------------------------------------
            Dictionary<string, StripData> stripLookup = new Dictionary<string, StripData>();
            foreach (var stripSave in snapshot.Strips)
            {
                StripData strip = StripData.CreateFromSaveData(stripSave);
                if (StripManager.Instance != null) StripManager.Instance.ActiveStrips.Add(strip);
                stripLookup[strip.StripID] = strip;
            }

            // ----------------------------------------------------
            // Phase 1 & 2 混合：恢复网格与大世界物品
            // ----------------------------------------------------
            foreach (var cellSave in snapshot.ActiveGridCells)
            {
                GridCell realCell = GridManager.Instance.GetGridCell(cellSave.GridPosition);
                if (realCell == null) continue;

                if (cellSave.CutEdges != null && realCell.CutEdges != null)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        realCell.CutEdges[i] = cellSave.CutEdges[i];
                    }
                }

                // 还原传送带与条带的内存指针绑定
                if (cellSave.Belt != null)
                {
                    realCell.Belt = new BeltData { Dir = (Direction)cellSave.Belt.Dir };
                    if (!string.IsNullOrEmpty(cellSave.Belt.ParentStripID) && stripLookup.TryGetValue(cellSave.Belt.ParentStripID, out var parentStrip))
                    {
                        realCell.Belt.ParentStrip = parentStrip;
                    }
                }

                // 还原大世界上的掉落物/传送带物品
                if (cellSave.Item != null && !string.IsNullOrEmpty(cellSave.Item.ItemID))
                {
                    ItemDefinition itemDef = ConfigManager.Instance.GetItem(cellSave.Item.ItemID);
                    if (itemDef != null)
                    {
                        ItemData item = new ItemData(itemDef) { Progress = cellSave.Item.Progress, CurrentCell = realCell };
                        realCell.Item = item;
                        // 注意：如果有专门的 SimulationController.RegisterItem(item) 这里应该调用，以确保物品纳入 Tick 计算
                    }
                }
            }

            // ----------------------------------------------------
            // Phase 1 & 2 混合：还原机箱、模块及其空间映射
            // ----------------------------------------------------
            foreach (var shellSave in snapshot.Machines)
            {
                MachineShellProfile profile = ConfigManager.Instance.GetProfile(shellSave.ProfileID);
                // 【核心修复】：从 4 个整形重新组装出 Unity 的 RectInt
                RectInt restoredBounds = new RectInt(shellSave.BoundsX, shellSave.BoundsY, shellSave.BoundsWidth, shellSave.BoundsHeight);
                MachineShellData shell = new MachineShellData(profile, restoredBounds);
                shell.ShellID = shellSave.ShellID; // 强制覆写为存档 ID

                foreach (var modSave in shellSave.Modules)
                {
                    ModuleDefinition def = ConfigManager.Instance.GetModule(modSave.ModuleDefinitionID);
                    
                    // 【反射工厂】：根据之前存入的真实类名，动态实例化正确的子类
                    Type moduleType = Type.GetType("AssemblyLine.Data.Machine." + modSave.ModuleType);
                    if (moduleType == null) { Debug.LogError($"无法解析模块类型: {modSave.ModuleType}"); continue; }
                    
                    MachineModuleData module = (MachineModuleData)Activator.CreateInstance(moduleType);
                    module.Definition = def;
                    module.LocalBottomLeft = modSave.LocalBottomLeft;
                    module.Rotation = (ModuleRotation)modSave.Rotation;

                    // == 注入特化数据 ==
                    if (module is WarehouseCoreData warehouse && modSave.Storage != null)
                    {
                        warehouse.MarketPriority = modSave.MarketPriority;
                        warehouse.BuildTick = modSave.BuildTick;
                        warehouse.Storage.RestoreFromSaveData(modSave.Storage, ConfigManager.Instance.GetItem);
                    }
                    else if (module is ImporterCoreData importer)
                    {
                        importer.TargetItem = ConfigManager.Instance.GetItem(modSave.TargetItemID);
                        importer.ImportTime = modSave.ImportTime;
                    }
                    else if (module is MachineCoreData core && modSave.ActiveQueues != null)
                    {
                        core.ActiveQueues.Clear(); // 清空构造函数自带的默认队列
                        foreach (var qSave in modSave.ActiveQueues)
                        {
                            ProcessingQueue q = new ProcessingQueue
                            {
                                CurrentRecipe = ConfigManager.Instance.GetRecipe(qSave.RecipeID),
                                ProcessingProgress = qSave.ProcessingProgress,
                                MaxBufferSize = qSave.MaxBufferSize
                            };
                            // 恢复输入输出缓存的物品实例
                            if (qSave.InputBuffer != null) { foreach(var itm in qSave.InputBuffer) { var d = ConfigManager.Instance.GetItem(itm.ItemID); if(d!=null) q.InputBuffer.Add(new ItemData(d) { Progress = itm.Progress }); } }
                            if (qSave.OutputBuffer != null) { foreach(var itm in qSave.OutputBuffer) { var d = ConfigManager.Instance.GetItem(itm.ItemID); if(d!=null) q.OutputBuffer.Add(new ItemData(d) { Progress = itm.Progress }); } }
                            core.ActiveQueues.Add(q);
                        }
                    }

                    // == 重建父子联系与路由 ==
                    module.ParentShell = shell;
                    shell.Modules.Add(module);
                    
                    if (module is MachineCoreData c) shell.MainCore = c;
                    else if (module is InputPortData input) { input.Rules = RestoreRules(modSave.Rules); shell.InputPorts.Add(input); }
                    else if (module is OutputPortData output) { output.Rules = RestoreRules(modSave.Rules); shell.OutputPorts.Add(output); }

                    // == 重建与 GridCell 的底层空间映射 (物理指针回归) ==
                    foreach (Vector2Int localPos in module.GetOccupiedLocalCells())
                    {
                        Vector2Int worldPos = new Vector2Int(shell.Bounds.xMin + localPos.x, shell.Bounds.yMin + localPos.y);
                        GridCell cell = GridManager.Instance.GetGridCell(worldPos);
                        if (cell != null) cell.OccupyingModule = module;
                    }
                }

                // 一键恢复死区、隔断墙及当前机箱运行效率
                shell.RecalculateStats();
                MachineManager.Instance.AllActiveShells.Add(shell);
            }

            Debug.Log("[SaveLoadManager] 读档核心数据重构完毕！广播重绘事件...");
            
            // 数据重构完毕，通知全系统的 View 层重新映射视觉表现！
            OnSimulationDataRestored?.Invoke();
        }

        // ==========================================
        // 辅助反序列化工具
        // ==========================================
        private PortRuleConfig RestoreRules(PortRuleConfigSaveData save)
        {
            var rules = new PortRuleConfig();
            if (save != null)
            {
                rules.MaxThroughputPerTick = save.MaxThroughputPerTick;
                rules.MarketPriority = save.MarketPriority;
                if (save.Whitelist != null) rules.Whitelist = new HashSet<string>(save.Whitelist);
            }
            return rules;
        }
    }
}