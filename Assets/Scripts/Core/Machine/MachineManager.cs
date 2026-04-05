using System.Collections.Generic;
using UnityEngine;

public class MachineManager : MonoBehaviour
{
    public static MachineManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // ==========================================
    // 核心引擎：校验模块是否允许被放置在机箱的指定位置
    // ==========================================
    public bool CanPlaceModule(MachineShellData shell, MachineModuleData module)
    {
        // 获取模块在当前旋转状态下，需要占据的局部坐标
        List<Vector2Int> localCells = module.GetOccupiedLocalCells();

        // 【第一重锁 & 第二重锁 & 第三重锁】：遍历所有占用的格子
        foreach (Vector2Int localPos in localCells)
        {
            // 1. 越界锁：模块是否超出了机箱的物理尺寸？
            if (localPos.x < 0 || localPos.x >= shell.Bounds.width ||
                localPos.y < 0 || localPos.y >= shell.Bounds.height)
            {
                Debug.LogWarning($"[放置失败] 模块超出了机箱边界: {localPos}");
                return false;
            }

            // 2. 死区锁：该格子是否为机箱的坏死区/承重柱？
            if (shell.DeadCells.Contains(localPos))
            {
                Debug.LogWarning($"[放置失败] 触碰机箱绝对死区: {localPos}");
                return false;
            }

            // 3. 占用锁：该格子是否已经被其他模块占用了？
            // 将局部坐标转换为大世界网格坐标
            Vector2Int worldPos = new Vector2Int(shell.Bounds.xMin + localPos.x, shell.Bounds.yMin + localPos.y);
            GridCell cell = GridManager.Instance.GetGridCell(worldPos);
            
            if (cell == null || cell.OccupyingModule != null)
            {
                Debug.LogWarning($"[放置失败] 世界网格位置已被占用: {worldPos}");
                return false;
            }
        }
        // ==========================================
        // 【第四重锁】：便当盒隔断锁
        // ==========================================
        // 获取模块作为一个整体，内部必须连通的接缝
        List<InternalWall> requiredConnections = module.GetRequiredInternalConnections();
        foreach (InternalWall conn in requiredConnections)
        {
            // 检查机箱的隔板字典中，是否存在这堵墙
            if (shell.PartitionWalls.Contains(conn))
            {
                Debug.LogWarning($"[放置失败] 模块内部连通性被机箱隔断墙切断! 阻挡位置: {conn.CellA} <-> {conn.CellB}");
                return false;
            }
        }

        // ==========================================
        // 【第五重锁】：I/O 匣子的边缘与朝向锁
        // ==========================================
        if (module is InputPortData || module is OutputPortData)
        {
            Vector2Int pos = module.LocalBottomLeft; // I/O 匣子是 1x1，只看这一个坐标
            Direction facing = (module is InputPortData input) ? input.FacingDir : ((OutputPortData)module).FacingDir;

            int maxX = shell.Bounds.width - 1;
            int maxY = shell.Bounds.height - 1;

            bool isValidEdgeAndFacing = false;

            // 检查：是否在顶部边缘，且朝上？
            if (pos.y == maxY && facing == Direction.Up) isValidEdgeAndFacing = true;
            // 检查：是否在底部边缘，且朝下？
            else if (pos.y == 0 && facing == Direction.Down) isValidEdgeAndFacing = true;
            // 检查：是否在右侧边缘，且朝右？
            else if (pos.x == maxX && facing == Direction.Right) isValidEdgeAndFacing = true;
            // 检查：是否在左侧边缘，且朝左？
            else if (pos.x == 0 && facing == Direction.Left) isValidEdgeAndFacing = true;

            if (!isValidEdgeAndFacing)
            {
                // 如果既不贴边，开口也不朝外，直接亮红灯！
                Debug.LogWarning($"[放置失败] I/O 匣子必须放在机箱边缘，且开口必须朝向外部！当前坐标:{pos}, 朝向:{facing}");
                return false;
            }
        }

        return true; // 恭喜，全部校验通过！
    }

    // ==========================================
    // 动作执行：正式将模块写入数据底座
    // ==========================================
    public void PlaceModule(MachineShellData shell, MachineModuleData module)
    {
        if (!CanPlaceModule(shell, module)) return;

        // 1. 建立归属关系
        module.ParentShell = shell;
        shell.Modules.Add(module);

        // 2. 局域网路由注册
        if (module is MachineCoreData core) shell.MainCore = core;
        else if (module is InputPortData inputPort) shell.InputPorts.Add(inputPort);
        else if (module is OutputPortData outputPort) shell.OutputPorts.Add(outputPort);

        // 3. 将数据写入大世界网格
        List<Vector2Int> localCells = module.GetOccupiedLocalCells();
        foreach (Vector2Int localPos in localCells)
        {
            Vector2Int worldPos = new Vector2Int(shell.Bounds.xMin + localPos.x, shell.Bounds.yMin + localPos.y);
            GridCell cell = GridManager.Instance.GetGridCell(worldPos);
            if (cell != null) cell.OccupyingModule = module;
        }

        // ==========================================
        // 【关键】：放置完毕，触发全机箱的增益重算！
        // ==========================================
        shell.RecalculateStats();

        Debug.Log($"[系统提示] 成功放置模块！当前模块数: {shell.Modules.Count}");
    }

    // ==========================================
    // 【新增】：模块拆卸逻辑 (为了之后的 UI 右键拆卸准备)
    // ==========================================
    public void RemoveModule(MachineShellData shell, MachineModuleData module)
    {
        if (!shell.Modules.Contains(module)) return;

        // 1. 断开归属关系
        shell.Modules.Remove(module);
        module.ParentShell = null;

        // 2. 解除局域网路由
        if (module is MachineCoreData) shell.MainCore = null;
        else if (module is InputPortData inputPort) shell.InputPorts.Remove(inputPort);
        else if (module is OutputPortData outputPort) shell.OutputPorts.Remove(outputPort);

        // 3. 清理大世界网格上的占位
        List<Vector2Int> localCells = module.GetOccupiedLocalCells();
        foreach (Vector2Int localPos in localCells)
        {
            Vector2Int worldPos = new Vector2Int(shell.Bounds.xMin + localPos.x, shell.Bounds.yMin + localPos.y);
            GridCell cell = GridManager.Instance.GetGridCell(worldPos);
            
            // 确保没有误删别的模块的占位
            if (cell != null && cell.OccupyingModule == module) 
            {
                cell.OccupyingModule = null;
            }
        }

        // ==========================================
        // 【关键】：拆卸完毕，触发全机箱的增益重算！（Buff失效）
        // ==========================================
        shell.RecalculateStats();

        Debug.Log($"[系统提示] 成功拆卸模块！当前模块数: {shell.Modules.Count}");
    }
    // ==========================================
    // 全局机器数据缓存
    // ==========================================
    // 记录世界上所有的机箱，方便我们在 Update 里遍历它们让它们工作
    public List<MachineShellData> AllActiveShells = new List<MachineShellData>();

    // ==========================================
    // 供外部传送带调用的“喂食”接口 (多线程版)
    // ==========================================
    public bool TryIngestItem(GridCell targetCell, ItemData item)
    {
        if (targetCell.OccupyingModule is InputPortData inputPort)
        {
            MachineShellData shell = inputPort.ParentShell;
            if (shell != null && shell.MainCore != null)
            {
                // 【修复】：遍历机器所有的处理队列，寻找第一个有空位的肚子
                foreach (ProcessingQueue queue in shell.MainCore.ActiveQueues)
                {
                    if (queue.InputBuffer.Count < queue.MaxBufferSize)
                    {
                        queue.InputBuffer.Add(item);
                        return true; // 成功吞下！
                    }
                }
            }
        }
        return false; // 吞噬失败（没孔、没核心、或者所有队列肚子都满了）
    }

    // ==========================================
    // 核心加工与输出循环 (多线程+增益版)
    // ==========================================
    public void TickMachines(float tickDelta)
    {
        foreach (MachineShellData shell in AllActiveShells)
        {
            if (shell.MainCore == null) continue; // 没装核心的空壳子不工作
            MachineCoreData core = shell.MainCore;

            // ------------------------------------
            // 阶段 1：多队列并发加工引擎
            // ------------------------------------
            foreach (ProcessingQueue queue in core.ActiveQueues)
            {
                if (queue.CurrentRecipe != null)
                {
                    // 校验：当前这条流水线的原料够不够？产物区还有没有空位？
                    if (HasEnoughInputs(queue, queue.CurrentRecipe) && queue.OutputBuffer.Count < queue.MaxBufferSize)
                    {
                        // 【核心架构亮点】：无缝接入 Buff 系统！
                        // 进度 = 基础时间流逝(tickDelta) * 机器当前的加速倍率
                        queue.ProcessingProgress += tickDelta * shell.CurrentStats.SpeedMultiplier;

                        // 进度条满了，完成加工
                        if (queue.ProcessingProgress >= queue.CurrentRecipe.ProcessingTime) 
                        {
                            ConsumeInputs(queue, queue.CurrentRecipe);
                            ProduceOutputs(queue, queue.CurrentRecipe);
                            queue.ProcessingProgress = 0f; // 进度归零
                            Debug.Log($"[{shell.ShellID}] 某条流水线已完成【{queue.CurrentRecipe.DisplayName}】加工！");
                        }
                    }
                    else
                    {
                        // 原料不足或产物堆积，该队列进度暂停
                        queue.ProcessingProgress = 0f; 
                    }
                }
            }

            // ------------------------------------
            // 阶段 2：吐出逻辑 (支持多输出匣)
            // ------------------------------------
            if (shell.OutputPorts.Count > 0)
            {
                foreach (OutputPortData port in shell.OutputPorts)
                {
                    // 在所有的队列中，寻找第一个有产物可以吐出的队列
                    ProcessingQueue readyQueue = null;
                    foreach (ProcessingQueue queue in core.ActiveQueues)
                    {
                        if (queue.OutputBuffer.Count > 0)
                        {
                            readyQueue = queue;
                            break;
                        }
                    }

                    // 如果有产物可以吐
                    if (readyQueue != null)
                    {
                        // 计算输出匣在大世界中的前方坐标
                        Vector2Int portWorldPos = new Vector2Int(shell.Bounds.xMin + port.LocalBottomLeft.x, shell.Bounds.yMin + port.LocalBottomLeft.y);
                        Vector2Int forwardPos = GetForwardPosition(portWorldPos, port.FacingDir);
                        GridCell forwardCell = GridManager.Instance.GetGridCell(forwardPos);

                        if (forwardCell != null && forwardCell.Belt != null && forwardCell.Item == null)
                        {
                            // 1. 数据层转移
                            ItemData outItem = readyQueue.OutputBuffer[0];
                            readyQueue.OutputBuffer.RemoveAt(0);
                            
                            forwardCell.Item = outItem;
                            outItem.CurrentCell = forwardCell;
                            outItem.Progress = 0f; 

                            // 2. 物理层注册与视觉层渲染
                            SimulationController.Instance.RegisterItem(outItem);
                            InteractionController.Instance.SpawnItemVisual(outItem, forwardPos);

                            break; // 这个输出口吐出物品后跳出，每次 Update 每个口只吐 1 个
                        }
                    }
                }
            }
        }
    }

    // 辅助工具：根据方向获取前方一格的坐标
    private Vector2Int GetForwardPosition(Vector2Int pos, Direction dir)
    {
        switch (dir)
        {
            case Direction.Up: return new Vector2Int(pos.x, pos.y + 1);
            case Direction.Right: return new Vector2Int(pos.x + 1, pos.y);
            case Direction.Down: return new Vector2Int(pos.x, pos.y - 1);
            case Direction.Left: return new Vector2Int(pos.x - 1, pos.y);
            default: return pos;
        }
    }

    // ==========================================
    // 配方引擎辅助方法 (已修复：参数改为独立的 ProcessingQueue)
    // ==========================================

    private bool HasEnoughInputs(ProcessingQueue queue, RecipeDefinition recipe)
    {
        foreach (var req in recipe.Inputs)
        {
            int count = 0;
            foreach (var item in queue.InputBuffer)
            {
                if (item.Definition == req.Item) count++;
            }
            if (count < req.Amount) return false; 
        }
        return true;
    }

    private void ConsumeInputs(ProcessingQueue queue, RecipeDefinition recipe)
    {
        foreach (var req in recipe.Inputs)
        {
            int removedCount = 0;
            for (int i = queue.InputBuffer.Count - 1; i >= 0; i--)
            {
                if (queue.InputBuffer[i].Definition == req.Item)
                {
                    queue.InputBuffer.RemoveAt(i);
                    removedCount++;
                    if (removedCount == req.Amount) break; 
                }
            }
        }
    }

    private void ProduceOutputs(ProcessingQueue queue, RecipeDefinition recipe)
    {
        foreach (var outDef in recipe.Outputs)
        {
            for (int i = 0; i < outDef.Amount; i++)
            {
                queue.OutputBuffer.Add(new ItemData(outDef.Item));
            }
        }
    }

}