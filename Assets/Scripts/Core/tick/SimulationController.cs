using UnityEngine;
using System.Collections.Generic;

public class SimulationController : MonoBehaviour
{
    public static SimulationController Instance { get; private set; }

    [Header("Simulation Settings")]
    public float TickRate = 0.05f; 
    public ulong CurrentTick { get; private set; } = 0;

    // 活跃物品名单。系统只关心这里的物品。
    public List<ItemData> ActiveItems = new List<ItemData>(); 

    // 注册新物品的方法
    public void RegisterItem(ItemData newItem)
    {
        if (!ActiveItems.Contains(newItem))
        {
            ActiveItems.Add(newItem);
        }
    }

    private float _accumulatedTime = 0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Update()
    {
        _accumulatedTime += Time.deltaTime;
        while (_accumulatedTime >= TickRate)
        {
            PerformTick();
            _accumulatedTime -= TickRate;
        }

        // 逻辑更新后，每帧平滑更新画面表现
        UpdateItemVisuals();
    }

    private void PerformTick()
    {
        CurrentTick++;

        //处理物品移动
        MoveItems();
    }

    private void MoveItems()
    {
        // 现在我们只需要遍历“活跃名单”里的物品
        for (int i = 0; i < ActiveItems.Count; i++)
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

                if (nextCell != null && nextCell.Belt != null && nextCell.Item == null)
                {
                    // 跨格转移
                    nextCell.Item = item;
                    cell.Item = null;
                    
                    // 【关键新增】：更新物品的 GPS 定位！
                    item.CurrentCell = nextCell; 
                    
                    item.Progress -= 1.0f;
                }
                else
                {
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

    // --- 预留给表现层的更新方法 ---
    private void UpdateItemVisuals()
    {
        // 稍后通过 InteractionController 来刷新屏幕上的小方块位置
        if (InteractionController.Instance != null)
        {
            InteractionController.Instance.RenderItems();
        }
    }
}