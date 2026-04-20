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

        // ==========================================
        // 【第六重锁】：经济终端 I/O 防呆锁
        // ==========================================
        if (module is OutputPortData && shell.MainCore is ExporterCoreData)
        {
            Debug.LogWarning("[放置失败] 交付仓库（市场黑洞）不需要也不能安装向外吐东西的【输出匣】！");
            return false;
        }
        if (module is ExporterCoreData && shell.OutputPorts.Count > 0)
        {
            Debug.LogWarning("[放置失败] 机箱内已存在【输出匣】，无法将其改装为交付仓库！");
            return false;
        }
        if (module is InputPortData && shell.MainCore is ImporterCoreData)
        {
            Debug.LogWarning("[放置失败] 市场采购终端（源泉）不需要也不能安装吃东西的【输入匣】！");
            return false;
        }
        if (module is ImporterCoreData && shell.InputPorts.Count > 0)
        {
            Debug.LogWarning("[放置失败] 机箱内已存在【输入匣】，无法将其改装为市场采购终端！");
            return false;
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
    // 【新增】：提供给 UI 层的标准配方配置接口
    // 严格遵循 MVC，仅修改纯数据，不涉及任何表现层组件
    // ==========================================
    public void SetRecipe(MachineShellData shell, int queueIndex, RecipeDefinition recipe)
    {
        if (shell == null || shell.MainCore == null) 
        {
            Debug.LogWarning("[数据层] 无法配置配方：机箱为空或尚未安装核心模块！");
            return;
        }
        
        // 防越界校验
        if (queueIndex >= 0 && queueIndex < shell.MainCore.ActiveQueues.Count)
        {
            shell.MainCore.ActiveQueues[queueIndex].CurrentRecipe = recipe;
            Debug.Log($"[数据层] 成功为机箱 {shell.ShellID} 的流水线 [{queueIndex}] 挂载配方: {(recipe != null ? recipe.DisplayName : "空")}");
        }
    }
    // ==========================================
    // 全局机器数据缓存
    // ==========================================
    // 记录世界上所有的机箱，方便我们在 Update 里遍历它们让它们工作
    public List<MachineShellData> AllActiveShells = new List<MachineShellData>();

    // ==========================================
    // 供外部传送带调用的“喂食”接口 (多线程版)
    // ==========================================
    /*
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
    }*/
    public bool TryIngestItem(GridCell targetCell, ItemData item)
    {
        // 1. 确认该格子确实是一个输入匣
        if (targetCell.OccupyingModule is InputPortData inputPort)
        {
            MachineShellData shell = inputPort.ParentShell;
            
            // 2. 确认机箱装了核心，并且有活跃的加工队列
            if (shell.MainCore != null && shell.MainCore.ActiveQueues.Count > 0)
            {
                // 3. 寻找一个没满的队列（目前默认塞给第一个队列）
                ProcessingQueue targetQueue = shell.MainCore.ActiveQueues[0];
                
                if (targetQueue.InputBuffer.Count < targetQueue.MaxBufferSize)
                {
                    // 【核心操作】：在纯数据层，将物品实体移交进机器的胃里！
                    targetQueue.InputBuffer.Add(item);
                    
                    // 打印日志：确认物品到底有没有进肚子
                    Debug.Log($"[机器进食] {shell.ShellID} 吞入了 {item.Definition.name}, 当前肚子里的数量: {targetQueue.InputBuffer.Count}");
                    return true;
                }
            }
        }
        return false;
    }

    // ==========================================
    // 核心加工与输出循环 (多线程+增益版 + 严苛空间校验)
    // ==========================================
    public void TickMachines(float tickDelta)
    {
        // 【性能优化】：在极高频的 Tick 驱动中，使用 for 循环避免 foreach 产生的隐性迭代器装箱开销
        for (int i = 0; i < AllActiveShells.Count; i++)
        {
            MachineShellData shell = AllActiveShells[i];
            if (shell.MainCore == null) continue; // 没装核心的空壳子不工作
            MachineCoreData core = shell.MainCore;

            // ------------------------------------
            // 阶段 1：核心处理引擎 (Process 分流)
            // ------------------------------------
            
            // 【新增分支 A】：如果是交付仓库，执行瞬间吞噬变现逻辑
            if (core is ExporterCoreData exporter)
            {
                long tickEarnings = 0;
                // 扁平化遍历仓库的接收队列
                for (int j = 0; j < exporter.ActiveQueues.Count; j++)
                {
                    ProcessingQueue queue = exporter.ActiveQueues[j];
                    if (queue.InputBuffer.Count > 0)
                    {
                        // 累加本次 Tick 吃掉的所有物品的价值
                        for (int k = 0; k < queue.InputBuffer.Count; k++)
                        {
                            if (queue.InputBuffer[k].Definition != null)
                            {
                                tickEarnings += queue.InputBuffer[k].Definition.BasePrice;
                            }
                        }
                        // 【纯数据层原子操作】：瞬间清空肚子里所有的实体，释放内存！
                        queue.InputBuffer.Clear(); 
                    }
                }

                // 统一结算当前 Tick 的收益，呼叫全局经济系统
                if (tickEarnings > 0 && EconomyManager.Instance != null)
                {
                    EconomyManager.Instance.AddFunds(tickEarnings);
                    // 探针：确认系统收到钱了
                    Debug.Log($"[经济系统] 交付仓库成功处理订单，入账: +${tickEarnings}！当前总资金: {EconomyManager.Instance.EconomyData.Funds}");
                    // 【表现层挂钩】：在机箱的中心点生成绿色加钱跳字！
                    if (InteractionController.Instance != null)
                    {
                        Vector2Int centerPos = new Vector2Int(shell.Bounds.xMin + shell.Bounds.width / 2, shell.Bounds.yMin + shell.Bounds.height / 2);
                        InteractionController.Instance.SpawnFloatingText($"+ ${tickEarnings}", centerPos, Color.green);
                    }
                }
                
                // 仓库处理完毕，直接跳过下方的常规配方运算！
                //continue; 
            }
            
            else if (core is ImporterCoreData importer)
            { 
                // 采购终端只需要用第 0 条队列来做进货缓冲
                if (importer.TargetItem != null && importer.ActiveQueues.Count > 0)
                {
                    ProcessingQueue queue = importer.ActiveQueues[0];
                    
                    // 确保输出区没满才继续买
                    if (queue.OutputBuffer.Count < queue.MaxBufferSize)
                    {
                        queue.ProcessingProgress += tickDelta * shell.CurrentStats.SpeedMultiplier;
                        // 进度满了，尝试向经济中心发起扣款请求
                        if (queue.ProcessingProgress >= importer.ImportTime)
                        {
                            if (EconomyManager.Instance != null && EconomyManager.Instance.HasEnoughFunds(importer.TargetItem.BasePrice))
                            {
                                // 扣款 -> 生成实体 -> 重置进度
                                EconomyManager.Instance.ConsumeFunds(importer.TargetItem.BasePrice);
                                queue.OutputBuffer.Add(new ItemData(importer.TargetItem));
                                queue.ProcessingProgress = 0f;
                                // 【表现层挂钩】：在机箱的中心点生成红色扣钱跳字！
                                if (InteractionController.Instance != null)
                                {
                                    Vector2Int centerPos = new Vector2Int(shell.Bounds.xMin + shell.Bounds.width / 2, shell.Bounds.yMin + shell.Bounds.height / 2);
                                    InteractionController.Instance.SpawnFloatingText($"- ${importer.TargetItem.BasePrice}", centerPos, new Color(1f, 0.3f, 0.3f));
                                }
                            }
                            else
                            {
                                // 没钱了，进度停滞等待
                                queue.ProcessingProgress = importer.ImportTime;
                            }
                        }
                    }
                }
                //绝对不能写 continue，它需要走到阶段 2 把买到的东西吐出来！
            }
            // 【常规分支 C】：普通的加工机器，执行原有的配方驱动逻辑
            else 
            {
                for (int j = 0; j < core.ActiveQueues.Count; j++)
                {
                    ProcessingQueue queue = core.ActiveQueues[j];

                    if (queue.CurrentRecipe != null)
                    {
                        // ... 你原本写好的 HasEnoughInputs 和进度条推进逻辑保持不变 ...
                        if (HasEnoughInputs(queue, queue.CurrentRecipe) && queue.OutputBuffer.Count < queue.MaxBufferSize)
                        {
                            queue.ProcessingProgress += tickDelta * shell.CurrentStats.SpeedMultiplier;
                            if (queue.ProcessingProgress >= queue.CurrentRecipe.ProcessingTime) 
                            {
                                ConsumeInputs(queue, queue.CurrentRecipe);
                                ProduceOutputs(queue, queue.CurrentRecipe);
                                queue.ProcessingProgress = 0f; 
                            }
                        }
                        else
                        {
                            queue.ProcessingProgress = 0f; 
                        }
                    }
                }
            }

            // ------------------------------------
            // 阶段 2：吐出逻辑 (支持多输出匣) (Push)
            // ------------------------------------
            if (shell.OutputPorts.Count > 0)
            {
                for (int p = 0; p < shell.OutputPorts.Count; p++)
                {
                    OutputPortData port = shell.OutputPorts[p];

                    // 在所有的队列中，寻找第一个有产物可以吐出的队列
                    ProcessingQueue readyQueue = null;
                    for (int q = 0; q < core.ActiveQueues.Count; q++)
                    {
                        if (core.ActiveQueues[q].OutputBuffer.Count > 0)
                        {
                            readyQueue = core.ActiveQueues[q];
                            break; // 找到就立刻跳出，优先服务该队列
                        }
                    }

                    // 如果有产物可以吐
                    if (readyQueue != null)
                    {
                        // 【核心架构替换点】：使用我们第一步制定的空间映射协议
                        // 替代原有简单的坐标相加。这道防线会自动拦截“朝向对内”、“深埋机箱”等非法端口！
                        if (TryGetExternalPosition(shell, port, out Vector2Int forwardPos))
                        {
                            GridCell forwardCell = GridManager.Instance.GetGridCell(forwardPos);

                            // 严苛握手：不仅要有传送带、没有物品，且该格子上绝对不能被另一个机壳或模块物理占领！
                            if (forwardCell != null && forwardCell.Belt != null && forwardCell.Item == null && forwardCell.OccupyingModule == null)
                            {
                                // 1. 数据层剥离
                                ItemData outItem = readyQueue.OutputBuffer[0];
                                readyQueue.OutputBuffer.RemoveAt(0);
                                
                                // 赋予大世界数据流坐标
                                forwardCell.Item = outItem;
                                outItem.CurrentCell = forwardCell;
                                outItem.Progress = 0f; 

                                // 2. 物理层注册与视觉层渲染
                                SimulationController.Instance.RegisterItem(outItem);
                                
                                if (InteractionController.Instance != null)
                                {
                                    InteractionController.Instance.SpawnItemVisual(outItem, forwardPos);
                                }

                                // 跳出内部逻辑，这个物理端口本 Tick 已经完成了一次吞吐任务
                            }
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

    // ==========================================
    // 附加至 MachineManager.cs 的 I/O 坐标映射服务
    // ==========================================

    /// <summary>
    /// 尝试获取【输入端口】对外的绝对大世界交互坐标
    /// </summary>
    /// <param name="shell">所属机器外壳数据</param>
    /// <param name="port">输入端口数据</param>
    /// <param name="externalPos">输出：外部传送带所在的大世界坐标</param>
    /// <returns>如果是贴边的合法向外端口返回true，否则返回false</returns>
    public bool TryGetExternalPosition(MachineShellData shell, InputPortData port, out Vector2Int externalPos)
    {
        return CalculateExternalPos(shell, port.LocalBottomLeft, port.FacingDir, out externalPos);
    }

    /// <summary>
    /// 尝试获取【输出端口】对外的绝对大世界交互坐标
    /// </summary>
    public bool TryGetExternalPosition(MachineShellData shell, OutputPortData port, out Vector2Int externalPos)
    {
        return CalculateExternalPos(shell, port.LocalBottomLeft, port.FacingDir, out externalPos);
    }

    /// <summary>
    /// 核心计算逻辑：局部坐标转大世界目标交互坐标，并进行严苛的合法性校验
    /// </summary>
    private bool CalculateExternalPos(MachineShellData shell, Vector2Int localAnchor, Direction facingDir, out Vector2Int externalPos)
    {
        externalPos = Vector2Int.zero;
        if (shell == null) return false;

        // 1. 获取机器在大世界中的左下角原点
        Vector2Int shellGlobalOrigin = shell.Bounds.position;

        // 2. 计算端口模块本身的大世界绝对坐标
        Vector2Int portGlobalPos = shellGlobalOrigin + localAnchor;

        // 3. 根据朝向获取方向向量偏移
        Vector2Int dirOffset = GetDirectionOffset(facingDir);

        // 4. 计算预期的交互坐标（大世界坐标系）
        externalPos = portGlobalPos + dirOffset;

        // 5. 绝对防线：利用 RectInt 原生方法检查目标坐标是否在机箱内部！
        // 如果 Bounds.Contains 返回 true，说明该端口尝试吸取/吐出物品到机壳物理占地内
        // 这属于非法摆放（深埋或向内），在数据层彻底掐断其吞吐能力
        if (shell.Bounds.Contains(externalPos))
        {
            return false; 
        }

        return true;
    }

    /// <summary>
    /// 纯数学辅助：方向枚举转向量（基于你 BeltData.cs 中的顺时针枚举）
    /// Up=0, Right=1, Down=2, Left=3
    /// </summary>
    private Vector2Int GetDirectionOffset(Direction dir)
    {
        switch (dir)
        {
            case Direction.Up:    return new Vector2Int(0, 1);
            case Direction.Right: return new Vector2Int(1, 0);
            case Direction.Down:  return new Vector2Int(0, -1);
            case Direction.Left:  return new Vector2Int(-1, 0);
            default:              return Vector2Int.zero;
        }
    }
    


}