using UnityEngine;
using System.Collections.Generic;

public class InteractionController : MonoBehaviour
{
    //单例模式
    public static InteractionController Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    

    [Header("视觉预制体")]
    [Tooltip("传送带预制体")]
    public GameObject BeltPrefab;
    [Tooltip("物品预制体")]
    public GameObject ItemPrefab; // 【新增】物品的视觉预制体 (比如一个黄色小圆圈)

    // 记录表现层的 GameObject，键是网格坐标，值是对应的预制体实例
    private Dictionary<Vector2Int, GameObject> _spawnedBelts = new Dictionary<Vector2Int, GameObject>();
    
    // 记录画面上的物品 GameObject。键是纯数据 ItemData，值是 Unity 的 GameObject
    private Dictionary<ItemData, GameObject> _spawnedItems = new Dictionary<ItemData, GameObject>();


    private void Update()
    {
        // 左键：放置
        if (Input.GetMouseButtonDown(0))
        {
            HandleLeftClick();
        }
        // 右键：删除
        if (Input.GetMouseButtonDown(1))
        {
            HandleRightClick();
        }
        // R键：旋转鼠标悬停处的传送带
        if (Input.GetKeyDown(KeyCode.R))
        {
            HandleRotate();
        }
        // T键：拆分/缝合工具
        if (Input.GetKeyDown(KeyCode.T))
        {
            HandleSplit();
        }

        // + / - 键：条带速度控制 (可以用小键盘或主键盘的加减号)
        if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
        {
            HandleSpeedControl(0.5f); // 每次增加 0.5 速度
        }
        if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
        {
            HandleSpeedControl(-0.5f); // 每次减少 0.5 速度
        }

        // I键：在当前悬停的传送带上生成一个物品
        if (Input.GetKeyDown(KeyCode.I))
        {
            HandleSpawnItem();
        }

        // 【新增】：时间流速控制
        if (Input.GetKeyDown(KeyCode.Space))
        {
            // 空格键切换 暂停 / 正常
            if (SimulationController.Instance.CurrentSpeed == TimeSpeed.Paused)
                SetTimeSpeed(TimeSpeed.Normal);
            else
                SetTimeSpeed(TimeSpeed.Paused);
        }
        if (Input.GetKeyDown(KeyCode.Alpha1)) SetTimeSpeed(TimeSpeed.Normal); // 数字键 1：正常
        if (Input.GetKeyDown(KeyCode.Alpha2)) SetTimeSpeed(TimeSpeed.Fast);   // 数字键 2：2倍速
        if (Input.GetKeyDown(KeyCode.Alpha3)) SetTimeSpeed(TimeSpeed.SuperFast); // 数字键 3：5倍速

    }

    // --- 放置逻辑 ---
    private void HandleLeftClick()
    {
        Vector2Int gridPos = GetMouseGridPosition();
        GridCell cell = GridManager.Instance.GetGridCell(gridPos);

        if (cell != null && cell.Belt == null)
        {
            // 1. 数据层更新
            cell.Belt = new BeltData { Dir = Direction.Up };

            // 2. 表现层更新
            Vector2 spawnPos = GridManager.Instance.GridToWorldPosition(gridPos);
            GameObject newBelt = Instantiate(BeltPrefab, spawnPos, Quaternion.identity);
            
            // 3. 将新生成的视觉对象存入字典
            _spawnedBelts.Add(gridPos, newBelt);

            // 未来预留：
            StripManager.Instance.OnBeltModified(gridPos);
        }
    }

    // --- 删除逻辑 ---
    private void HandleRightClick()
    {
        Vector2Int gridPos = GetMouseGridPosition();
        GridCell cell = GridManager.Instance.GetGridCell(gridPos);

        if (cell != null && cell.Belt != null)
        {
            // 1. 数据层更新：清空传送带数据
            cell.Belt = null;

            // 2. 表现层更新：销毁对应的 GameObject，并从字典中移除
            if (_spawnedBelts.TryGetValue(gridPos, out GameObject beltVisual))
            {
                Destroy(beltVisual);
                _spawnedBelts.Remove(gridPos);
            }

            // 未来预留：
            StripManager.Instance.OnBeltModified(gridPos);
        }
    }

    // --- 旋转逻辑 ---
    private void HandleRotate()
    {
        Vector2Int gridPos = GetMouseGridPosition();
        GridCell cell = GridManager.Instance.GetGridCell(gridPos);

        if (cell != null && cell.Belt != null)
        {
            // 1. 数据层更新：方向顺时针旋转 90 度
            // 利用枚举转换为 int，+1 后取模 4，实现 Up->Right->Down->Left->Up 循环
            int currentDirInt = (int)cell.Belt.Dir;
            cell.Belt.Dir = (Direction)((currentDirInt + 1) % 4);

            // 2. 表现层更新：修改对应 GameObject 的旋转角度
            if (_spawnedBelts.TryGetValue(gridPos, out GameObject beltVisual))
            {
                UpdateVisualRotation(beltVisual, cell.Belt.Dir);
            }

            // 未来预留：
            StripManager.Instance.OnBeltModified(gridPos);
        }
    }

    // --- 拆分逻辑 ---
    private void HandleSplit()
    {
        Vector2Int gridPos = GetMouseGridPosition();
        GridCell cell = GridManager.Instance.GetGridCell(gridPos);

        if (cell != null && cell.Belt != null)
        {
            // 找到它正前方的格子
            Vector2Int forwardPos = gridPos;
            switch (cell.Belt.Dir)
            {
                case Direction.Up: forwardPos.y += 1; break;
                case Direction.Right: forwardPos.x += 1; break;
                case Direction.Down: forwardPos.y -= 1; break;
                case Direction.Left: forwardPos.x -= 1; break;
            }

            // 切换它们之间的连接状态
            GridManager.Instance.ToggleCutEdge(gridPos, forwardPos);

            // 通知管理器重新计算条带！
            StripManager.Instance.OnBeltModified(gridPos);
            // 因为牵涉到两个格子，为了安全，把前方格子的状态也抛给管理器刷新
            StripManager.Instance.OnBeltModified(forwardPos); 
        }
    }

    // --- 速度控制逻辑 ---
    private void HandleSpeedControl(float speedChange)
    {
        Vector2Int gridPos = GetMouseGridPosition();
        GridCell cell = GridManager.Instance.GetGridCell(gridPos);

        if (cell != null && cell.Belt != null && cell.Belt.ParentStrip != null)
        {
            StripData strip = cell.Belt.ParentStrip;
            
            // 调整速度，最低限制为 0.1f 防止倒流或完全卡死（除非你想做停止功能）
            strip.MoveSpeed = Mathf.Max(0.1f, strip.MoveSpeed + speedChange);
            
            Debug.Log($"【条带调速】条带 {strip.StripID} (颜色: {strip.StripColor}) 的速度已修改为: {strip.MoveSpeed}");
        }
    }

    // --- 流速控制逻辑 ---
    private void SetTimeSpeed(TimeSpeed newSpeed)
    {
        SimulationController.Instance.CurrentSpeed = newSpeed;
        Debug.Log($"游戏流速已切换为: {newSpeed}");
    }



    // --- 辅助工具方法 ---

    // 获取当前鼠标所在的网格坐标
    private Vector2Int GetMouseGridPosition()
    {
        Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        return GridManager.Instance.WorldToGridPosition(mouseWorldPos);
    }

    // 根据方向更新 2D 视觉旋转
    private void UpdateVisualRotation(GameObject visualObj, Direction dir)
    {
        float angle = 0f;
        switch (dir)
        {
            case Direction.Up: angle = 0f; break;
            case Direction.Right: angle = -90f; break; // 2D中顺时针旋转是负角度
            case Direction.Down: angle = 180f; break;
            case Direction.Left: angle = 90f; break;
        }
        visualObj.transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    // --- 视觉更新逻辑 ---
    
    // 遍历所有已生成的传送带，根据它们所属的 StripData 更新颜色
    public void UpdateBeltColorVisuals()
    {
        foreach (var kvp in _spawnedBelts)
        {
            Vector2Int pos = kvp.Key;
            GameObject beltObj = kvp.Value;

            GridCell cell = GridManager.Instance.GetGridCell(pos);
            
            // 确保格子上有传送带，并且它已经被分配到了某个条带中
            if (cell != null && cell.Belt != null && cell.Belt.ParentStrip != null)
            {
                Color stripColor = cell.Belt.ParentStrip.StripColor;
                
                // 获取传送带预制体上的 SpriteRenderer 组件并修改颜色
                SpriteRenderer sr = beltObj.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = stripColor;
                }
            }
        }
    }

    // --- 物品生成逻辑 ---
    private void HandleSpawnItem()
    {
        Vector2Int gridPos = GetMouseGridPosition();
        GridCell cell = GridManager.Instance.GetGridCell(gridPos);

        // 只有在有传送带、且当前格子没有物品时，才允许生成
        if (cell != null && cell.Belt != null && cell.Item == null)
        {
            ItemData newItem = new ItemData();
            cell.Item = newItem;

            // 【新增 1】：给新物品装上 GPS
            newItem.CurrentCell = cell; 
            
            // 【新增 2】：把它交接给模拟控制器的“活跃名单”
            SimulationController.Instance.RegisterItem(newItem);

            // 生成视觉对象
            Vector2 spawnPos = GridManager.Instance.GridToWorldPosition(gridPos);
            GameObject itemObj = Instantiate(ItemPrefab, spawnPos, Quaternion.identity);
            
            _spawnedItems.Add(newItem, itemObj);
        }
    }

    // --- 物品渲染逻辑 (由 SimulationController 每帧调用) ---
    public void RenderItems()
    {
        foreach (var kvp in _spawnedItems)
        {
            ItemData itemData = kvp.Key;
            GameObject itemObj = kvp.Value;

            // 【极致优化】：原来这里有几十行全网格搜索代码，现在只需 O(1) 的一次读取！
            GridCell currentCell = itemData.CurrentCell; 

            if (currentCell != null)
            {
                Vector2 centerPos = GridManager.Instance.GridToWorldPosition(currentCell.GridPosition);
                Vector2 offset = Vector2.zero;
                
                if (currentCell.Belt != null)
                {
                    float shift = (itemData.Progress - 0.5f) * GridManager.Instance.CellSize;
                    
                    switch (currentCell.Belt.Dir)
                    {
                        case Direction.Up: offset = new Vector2(0, shift); break;
                        case Direction.Right: offset = new Vector2(shift, 0); break;
                        case Direction.Down: offset = new Vector2(0, -shift); break;
                        case Direction.Left: offset = new Vector2(-shift, 0); break;
                    }
                }

                itemObj.transform.position = centerPos + offset;
            }
        }
    }
}