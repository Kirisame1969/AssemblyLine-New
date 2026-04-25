using System;
using UnityEngine;
using System.Collections.Generic;
using AssemblyLine.Data.Machine;

// 定义时间流速枚举 (利用整型作为乘数倍率)
public enum TimeSpeed { Paused = 0, Normal = 1, Fast = 2, SuperFast = 5 }

public class SimulationController : MonoBehaviour
{
    public static SimulationController Instance { get; private set; }   // 单例模式
    public ulong CurrentTick { get; private set; } = 0;                 // 游戏的总tick数
    public int CurrentCycle { get; private set; } = 1;                  // 当前的游戏周期（天数），从第1天开始

    [Header("时间与流速设置")]
    public float TickRate = 0.05f;                                      // 逻辑上永远是每 0.05 秒一 Tick
    public TimeSpeed CurrentSpeed = TimeSpeed.Normal;                   // 游戏时间流速倍速
    private float _accumulatedTime = 0f;                                // 时间蓄水池，用来帮助计算周期，见update
    
    [Header("周期逻辑配置")]
    [Tooltip("每个周期包含多少个 Tick。默认 600 Tick = 现实 30 秒")]
    // 假设 20 Ticks = 1秒
    // 实际游戏中你可以设为 12000 (现实时间 10 分钟)
    public ulong TicksPerCycle = 600; 

    // 【新增】：周期事件委托，方便未来其他系统（如怪物袭击、按天扣税）订阅
    public event Action<int> OnNewCycleStarted;

    // 活跃物品名单。系统只关心这里的物品。
    public List<ItemData> ActiveItems = new List<ItemData>(); 

    // 【新增】：当物品由于某种原因（被吞噬、掉落、销毁）从逻辑中移除时触发
    // 供表现层监听并清理对应的视觉实体
    public event Action<ItemData> OnItemLogicRemoved;
    

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Update()
    {
        // 【核心修改】：真实流逝的时间 乘以 时间倍率
        // 如果 Paused (0)，累加时间永远不增加，Tick 就停止跳动，实现完美暂停
        float multiplier = (int)CurrentSpeed;
        _accumulatedTime += Time.deltaTime * multiplier;

        while (_accumulatedTime >= TickRate)
        {
            PerformTick();
            _accumulatedTime -= TickRate;
        }

        // 无论是否暂停，画面渲染插值照常进行，确保视觉平滑
        // UpdateItemVisuals();
    }

    private void PerformTick()//update中调用
    {
        CurrentTick++;

        // 1. 处理周期逻辑
        CheckCycleProgress();

        // 2. 处理物品移动
        MoveItems();

        // 3. 【关键新增】：统一泵血！让所有的机器运转起来
        // 因为 while 循环已经处理了加速，所以每次 Tick 机器都只往前走固定的 TickRate 时间
        if (MachineManager.Instance != null)
        {
            MachineManager.Instance.TickMachines(TickRate);
        }
    }

    private void CheckCycleProgress()
    {
        // 当当前的 Tick 数量正好是一个周期的整数倍时，触发新周期
        if (CurrentTick > 0 && CurrentTick % TicksPerCycle == 0)
        {
            CurrentCycle++;
            Debug.Log($"【系统通知】进入第 {CurrentCycle} 周期 (Tick: {CurrentTick})");
            
            // 触发事件通知所有订阅者
            OnNewCycleStarted?.Invoke(CurrentCycle);
        }
    }

    private void MoveItems()
    {
        // 【关键修改】：因为物品一旦被吃掉就会从列表中移除，所以必须采用“倒序遍历”，防止索引越界！
        for (int i = ActiveItems.Count - 1; i >= 0; i--)
        {
            ItemData item = ActiveItems[i];
            GridCell cell = item.CurrentCell;

            // 如果物品处于悬空状态，暂停它的移动
            if (cell == null || cell.Belt == null || cell.Belt.ParentStrip == null) continue;

            float speed = cell.Belt.ParentStrip.MoveSpeed;
            item.Progress += speed * TickRate;

            if (item.Progress >= 1.0f)
            {
                Vector2Int nextPos = GetNextPosition(cell.GridPosition, cell.Belt.Dir);
                GridCell nextCell = GridManager.Instance.GetGridCell(nextPos);

                if (nextCell != null)
                {
                    /*
                    // ====================================================
                    // 🩺 诊断探针开始：打印前方的物理真实情况
                    // ====================================================
                    if (nextCell.OccupyingModule != null)
                    {
                        Debug.Log($"[诊断] 物品即将撞上: {nextCell.OccupyingModule.GetType().Name}");
                       if (nextCell.OccupyingModule is InputPortData inputPort)
                        {
                            MachineShellData shell = inputPort.ParentShell;
                            bool hasCore = shell.MainCore != null;
                            
                            // 【关键修复】：遍历所有队列，只要有一个没满，就不算 full
                            bool isFull = true;
                            if (hasCore)
                            {
                                foreach (var q in shell.MainCore.ActiveQueues)
                                {
                                    if (q.InputBuffer.Count < q.MaxBufferSize)
                                    {
                                        isFull = false;
                                        break;
                                    }
                                }
                            }
                            Debug.Log($"[诊断] 确认是输入匣！当前机箱是否有核心: {hasCore}。 核心是否已满: {isFull}");
                        }
                    }
                    // ====================================================
                    */


                    // ====================================================
                    // 【机器吞噬拦截】：前方有机器模块，且是输入匣！
                    // ====================================================
                    if (nextCell.OccupyingModule is InputPortData)
                    {
                        // 尝试呼叫中心把物品塞进去
                        bool ingested = MachineManager.Instance.TryIngestItem(nextCell, item);
                        if (ingested)
                        {
                            // 1. 数据清空：把它从传送带的格子上摘下来
                            cell.Item = null;
                            
                            // 2. 物理除名：从活跃移动名单中剔除（因为是倒序遍历，这里 RemoveAt 是绝对安全的）
                            ActiveItems.RemoveAt(i);

                            // 仅通知事件，不直接指挥表现层
                            OnItemLogicRemoved?.Invoke(item);
                            
                            continue; // 成功喂食，立刻跳过当前物品的处理，去处理下一个物品！
                        }
                        else
                        {
                            // 如果机器没装核心，或者核心库存满了，物品就死死卡在履带尽头排队
                            item.Progress = 1.0f;
                            continue;
                        }
                    }
                    // ====================================================

                    // 【原本的正常跨格逻辑】：前方是普通传送带
                    if (nextCell.Belt != null && nextCell.Item == null)
                    {
                        // 跨格转移
                        nextCell.Item = item;
                        cell.Item = null;
                        
                        // 更新物品的 GPS 定位！
                        item.CurrentCell = nextCell; 
                        
                        item.Progress -= 1.0f;
                    }
                    else
                    {
                        // 前方有传送带但是堵车了
                        item.Progress = 1.0f; 
                    }
                }
                else
                {
                    // 前方是地图边缘或者虚空
                    item.Progress = 1.0f; 
                }
            }
        }
    }

    // 辅助计算下一个坐标
    private Vector2Int GetNextPosition(Vector2Int currentPos, Direction dir)
    {
        switch (dir)
        {
            case Direction.Up: return new Vector2Int(currentPos.x, currentPos.y + 1);
            case Direction.Right: return new Vector2Int(currentPos.x + 1, currentPos.y);
            case Direction.Down: return new Vector2Int(currentPos.x, currentPos.y - 1);
            case Direction.Left: return new Vector2Int(currentPos.x - 1, currentPos.y);
            default: return currentPos;
        }
    }

    /*
    // --- 预留给表现层的更新方法 ---
    private void UpdateItemVisuals()
    {
        // 稍后通过 InteractionController 来刷新屏幕上的小方块位置
        if (InteractionController.Instance != null)
        {
            InteractionController.Instance.RenderItems();
        }
    }
    */
    // 注册新物品的方法
    public void RegisterItem(ItemData newItem)
    {
        if (!ActiveItems.Contains(newItem))
        {
            ActiveItems.Add(newItem);
        }
    }
    
}