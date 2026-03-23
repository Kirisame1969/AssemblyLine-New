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

        // ==========================================
        // 【关键修复：局域网路由注册】
        // 必须让机箱明确知道谁是核心，谁是嘴巴！
        // ==========================================
        if (module is MachineCoreData core)
        {
            shell.MainCore = core;
            Debug.Log($"[路由接入] 主核心已成功连接到机箱 {shell.ShellID} 的主板！");
        }
        else if (module is InputPortData inputPort)
        {
            shell.InputPorts.Add(inputPort);
            Debug.Log($"[路由接入] 输入匣已成功连接！");
        }
        else if (module is OutputPortData outputPort)
        {
            shell.OutputPorts.Add(outputPort);
            Debug.Log($"[路由接入] 输出匣已成功连接！");
        }


        // 2. 将数据写入大世界网格
        List<Vector2Int> localCells = module.GetOccupiedLocalCells();
        foreach (Vector2Int localPos in localCells)
        {
            Vector2Int worldPos = new Vector2Int(shell.Bounds.xMin + localPos.x, shell.Bounds.yMin + localPos.y);
            GridCell cell = GridManager.Instance.GetGridCell(worldPos);
            
            if (cell != null)
            {
                cell.OccupyingModule = module;
            }
        }

        Debug.Log($"[系统提示] 成功在机箱 {shell.ShellID} 放置了模块！当前模块数: {shell.Modules.Count}");
    }

    // ==========================================
    // 全局机器数据缓存
    // ==========================================
    // 记录世界上所有的机箱，方便我们在 Update 里遍历它们让它们工作
    public List<MachineShellData> AllActiveShells = new List<MachineShellData>();

    // ==========================================
    // 供外部传送带调用的“喂食”接口
    // ==========================================
    public bool TryIngestItem(GridCell targetCell, ItemData item)
    {
        // 检查这个格子上是不是正好插着一个输入匣
        if (targetCell.OccupyingModule is InputPortData inputPort)
        {
            MachineShellData shell = inputPort.ParentShell;
            // 检查这个机箱里有没有装核心，核心满没满
            if (shell != null && shell.MainCore != null)
            {
                if (shell.MainCore.InputBuffer.Count < shell.MainCore.MaxBufferSize)
                {
                    // 一口吞下！放入核心缓存
                    shell.MainCore.InputBuffer.Add(item);
                    return true; 
                }
            }
        }
        return false; // 吞噬失败（没孔、没核心、或者肚子满了）
    }

    // ==========================================
    // 核心加工与输出循环 (Tick)
    // ==========================================
    public void TickMachines(float tickDelta)
    {
        

        foreach (MachineShellData shell in AllActiveShells)
        {
            if (shell.MainCore == null) continue; // 没装核心的空壳子不工作

            MachineCoreData core = shell.MainCore;

            // ------------------------------------
            // 阶段 1：真正的配方加工引擎
            // ------------------------------------
            if (core.CurrentRecipe != null)
            {
                // 1. 校验：肚子里的原料够不够？产物区还有没有空位？
                if (HasEnoughInputs(core, core.CurrentRecipe) && core.OutputBuffer.Count < core.MaxBufferSize)
                {
                    // 【新增】：照妖镜！把读到的目标时间打印出来
                    //  Debug.Log($"[加工监视] 开始运转！当前进度: {core.ProcessingProgress}, 从图纸读到的目标耗时: {core.CurrentRecipe.ProcessingTime}");
                    
                    // 【关键】：进度条每次增加的不是 1，而是 0.05 秒！
                    core.ProcessingProgress += tickDelta;
                    
                    Debug.Log($"[时间侦探] 机器收到 Tick! 增加的秒数: {tickDelta}, 当前总进度: {core.ProcessingProgress}, 当前帧率倍速: {(int)SimulationController.Instance.CurrentSpeed}");
                    
                    // 2. 进度条满了，也就是度过了配方规定的耗时
                    if (core.ProcessingProgress >= core.CurrentRecipe.ProcessingTime) 
                    {
                        // 扣除配方所需求的原料
                        ConsumeInputs(core, core.CurrentRecipe);
                        
                        // 凭空生成配方所规定的产物
                        ProduceOutputs(core, core.CurrentRecipe);
                        
                        core.ProcessingProgress = 0f; // 进度归零，准备下一轮
                        Debug.Log($"[{shell.ShellID}] 按照配方【{core.CurrentRecipe.DisplayName}】加工完成！");
                    }
                }
                else
                {
                    // 原料不足或产物堆积，进度必须暂停或清零
                    core.ProcessingProgress = 0f; 
                }
            }

            // ------------------------------------
            // 阶段 2：吐出逻辑 (产物区有货，且有输出匣)
            // ------------------------------------
            if (core.OutputBuffer.Count > 0 && shell.OutputPorts.Count > 0)
            {
                // 遍历机箱所有的输出口
                foreach (OutputPortData port in shell.OutputPorts)
                {
                    // 计算输出匣在大世界中的位置
                    Vector2Int portWorldPos = new Vector2Int(shell.Bounds.xMin + port.LocalBottomLeft.x, shell.Bounds.yMin + port.LocalBottomLeft.y);
                    // 获取输出匣正前方对准的那个格子
                    Vector2Int forwardPos = GetForwardPosition(portWorldPos, port.FacingDir);
                    GridCell forwardCell = GridManager.Instance.GetGridCell(forwardPos);

                    // 如果前方有传送带，并且当前那个格子上没有被其他物品堵住
                    if (forwardCell != null && forwardCell.Belt != null && forwardCell.Item == null)
                    {
                        // 1. 数据层：把物品从核心转移到外部网格
                        ItemData outItem = core.OutputBuffer[0];
                        core.OutputBuffer.RemoveAt(0);
                        
                        forwardCell.Item = outItem;
                        outItem.CurrentCell = forwardCell;
                        outItem.Progress = 0f; // 刚上履带，进度为0

                        // 2. 物理层：把它重新注册给传送带系统，让它开始移动
                        SimulationController.Instance.RegisterItem(outItem);
                        
                        // 3. 表现层：通知 UI 控制器在画面上生成它的 视觉贴图
                        InteractionController.Instance.SpawnItemVisual(outItem, forwardPos);

                        // 每次 Update 每个核心只吐 1 个，防止在同一个口子瞬间重叠
                        break; 
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
    // 配方引擎辅助方法
    // ==========================================

    // 检查原料是否满足配方需求
    private bool HasEnoughInputs(MachineCoreData core, RecipeDefinition recipe)
    {
        foreach (var req in recipe.Inputs)
        {
            int count = 0;
            // 遍历核心肚子里所有的物品，数一数对应类型的有几个
            foreach (var item in core.InputBuffer)
            {
                if (item.Definition == req.Item) count++;
            }
            if (count < req.Amount) return false; // 只要有一种原料数量不够，直接返回 false
        }
        return true;
    }

    // 严格按照配方扣除原料
    private void ConsumeInputs(MachineCoreData core, RecipeDefinition recipe)
    {
        foreach (var req in recipe.Inputs)
        {
            int removedCount = 0;
            // 倒序遍历安全删除
            for (int i = core.InputBuffer.Count - 1; i >= 0; i--)
            {
                if (core.InputBuffer[i].Definition == req.Item)
                {
                    core.InputBuffer.RemoveAt(i);
                    removedCount++;
                    if (removedCount == req.Amount) break; // 扣够了就停止
                }
            }
        }
    }

    // 严格按照配方生成产物
    private void ProduceOutputs(MachineCoreData core, RecipeDefinition recipe)
    {
        foreach (var outDef in recipe.Outputs)
        {
            for (int i = 0; i < outDef.Amount; i++)
            {
                core.OutputBuffer.Add(new ItemData(outDef.Item));
            }
        }
    }

}