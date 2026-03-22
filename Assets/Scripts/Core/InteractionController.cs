
//---------------------------------------



using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class InteractionController : MonoBehaviour
{
    // 单例模式
    public static InteractionController Instance { get; private set; }

#region 核心状态数据
    [Header("UI建造状态")]
    public BuildableType CurrentBuildType = BuildableType.None; // 唯一的核心状态机

    [Header("机器拼装预览状态")]
    private MachineModuleData _previewModule = null; 
    private List<GameObject> _previewVisuals = new List<GameObject>();

    [Header("上帝之手(物品抓取)状态")]
    private ItemData _cursorItem = null;          // 鼠标当前抓着的物品数据
    private GameObject _cursorItemVisual = null;  // 鼠标当前抓着的物品视觉表现
#endregion

#region 视觉表现与字典引用
    [Header("视觉预制体")]
    public GameObject BeltPrefab;
    public GameObject ItemPrefab;

    private Dictionary<Vector2Int, GameObject> _spawnedBelts = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<ItemData, GameObject> _spawnedItems = new Dictionary<ItemData, GameObject>();
#endregion

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

#region 总控循环 (Update)
    private void Update()
    {
        // 1. 全局拦截：防 UI 穿透
        if (EventSystem.current.IsPointerOverGameObject() && Input.GetMouseButtonDown(0)) return;

        // 2. 全局拦截：右键优先级 (取消选中 > 删除实体)
        if (Input.GetMouseButtonDown(1))
        {
            if (CurrentBuildType != BuildableType.None)
            {
                ResetBuildState(); // 手里有图纸，右键就是撕图纸
                return;
            }
            else
            {
                HandleDeletion(); // 手里空空，右键就是拆迁队
            }
        }

        // 3. 全局热键：时间控制
        if (Input.GetKeyDown(KeyCode.Space)) SetTimeSpeed(SimulationController.Instance.CurrentSpeed == TimeSpeed.Paused ? TimeSpeed.Normal : TimeSpeed.Paused);
        if (Input.GetKeyDown(KeyCode.Alpha1)) SetTimeSpeed(TimeSpeed.Normal);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SetTimeSpeed(TimeSpeed.Fast);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SetTimeSpeed(TimeSpeed.SuperFast);

        // 4. 状态分发器 (核心重构区)
        switch (CurrentBuildType)
        {
            case BuildableType.None:
                // 空手状态：只负责抓取物品和基础工具栏
                HandleGrabAndDrop_Update(); 

                // 独立工具
                if (Input.GetKeyDown(KeyCode.R)) HandleRotate();
                if (Input.GetKeyDown(KeyCode.T)) HandleSplit();
                if (Input.GetKeyDown(KeyCode.I)) HandleSpawnItem();
                if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus)) HandleSpeedControl(0.5f);
                if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) HandleSpeedControl(-0.5f);
                break;

            case BuildableType.ConveyorBelt:
                // 传送带专属建造逻辑
                HandleBeltPlacement();
                break;

            case BuildableType.MachineShell_Test:
                // 机箱放置逻辑
                HandleShellPlacement();
                break;

            case BuildableType.Module_Core_1x1:
            case BuildableType.Module_Rect_2x1:
            case BuildableType.Module_Rect_2x2:
                // 模块拼装逻辑
                UpdateModulePreviewAndPlacement();
                if (Input.GetKeyDown(KeyCode.R) && _previewModule != null)
                {
                    _previewModule.Rotation = (ModuleRotation)(((int)_previewModule.Rotation + 1) % 4);
                }
                break;
        }
    }
    #endregion

#region UI 交互与状态清理
    public void OnBuildButtonClicked(int typeIndex)
    {
        ResetBuildState();
        CurrentBuildType = (BuildableType)typeIndex;

        switch (CurrentBuildType)
        {
            case BuildableType.ConveyorBelt: Debug.Log("UI：传送带模式"); break;
            case BuildableType.MachineShell_Test: Debug.Log("UI：机器外壳模式"); break;
            case BuildableType.Module_Core_1x1: SelectModule(new MachineCoreData()); break;
            case BuildableType.Module_Rect_2x1: SelectModule(new RectModuleData(2, 1)); break;
            case BuildableType.Module_Rect_2x2: SelectModule(new RectModuleData(2, 2)); break;
            case BuildableType.None: Debug.Log("UI：空手模式"); break;
        }
    }

    private void SelectModule(MachineModuleData moduleData)
    {
        _previewModule = moduleData;
    }

    private void ResetBuildState()
    {
        if (_cursorItemVisual != null)
        {
            Destroy(_cursorItemVisual);
            _cursorItemVisual = null;
            _cursorItem = null; 
        }

        foreach (var visual in _previewVisuals) Destroy(visual);
        _previewVisuals.Clear();
        _previewModule = null;

        CurrentBuildType = BuildableType.None; 
    }
    #endregion

#region 具体建造与拆除逻辑 (被搬家的代码)
    
    // 【重构】：原来的 HandleLeftClick 变成了专属的传送带放置
    private void HandleBeltPlacement()
    {
        if (Input.GetMouseButtonDown(0)) // 监听左键
        {
            Vector2Int gridPos = GetMouseGridPosition();
            GridCell cell = GridManager.Instance.GetGridCell(gridPos);

            if (cell != null && cell.Belt == null && cell.ShellRegion == null) // 防止建在机箱里
            {
                cell.Belt = new BeltData { Dir = Direction.Up };
                Vector2 spawnPos = GridManager.Instance.GridToWorldPosition(gridPos);
                GameObject newBelt = Instantiate(BeltPrefab, spawnPos, Quaternion.identity);
                _spawnedBelts.Add(gridPos, newBelt);
                StripManager.Instance.OnBeltModified(gridPos);
            }
        }
    }

    // 【升级】：原来的 HandleRightClick 升级为了通用拆除
    private void HandleDeletion()
    {
        Vector2Int gridPos = GetMouseGridPosition();
        GridCell cell = GridManager.Instance.GetGridCell(gridPos);

        if (cell == null) return;

        // 1. 尝试拆除传送带
        if (cell.Belt != null)
        {
            cell.Belt = null;
            if (_spawnedBelts.TryGetValue(gridPos, out GameObject beltVisual))
            {
                Destroy(beltVisual);
                _spawnedBelts.Remove(gridPos);
            }
            StripManager.Instance.OnBeltModified(gridPos);
            Debug.Log("已拆除传送带");
            return; // 删了一次就停手
        }

        // 2. 尝试拆除机器外壳 (未来可扩展拆除内部模块)
        if (cell.ShellRegion != null)
        {
            MachineShellData shell = cell.ShellRegion;
            // 清理这片区域占用的所有网格属性
            for (int x = 0; x < shell.Bounds.width; x++)
            {
                for (int y = 0; y < shell.Bounds.height; y++)
                {
                    Vector2Int pos = new Vector2Int(shell.Bounds.xMin + x, shell.Bounds.yMin + y);
                    GridCell targetCell = GridManager.Instance.GetGridCell(pos);
                    if (targetCell != null)
                    {
                        targetCell.ShellRegion = null;
                        targetCell.OccupyingModule = null;
                    }
                }
            }
            // (注：由于我们机箱的地板现在是动态生成的Quad且未集中保存，目前画面上的灰底板暂不会消失。
            // 未来我们会建立一个 MachineVisualManager 统一处理视觉销毁)
            //foreach (var v in shell.FloorVisuals) Destroy(v);
            //这里可能是专门用来拆除机器外壳的？
            // 【新增】：销毁画面上的视觉底板
            foreach (GameObject visualQuad in shell.FloorVisuals)
            {
                Destroy(visualQuad);
            }
            shell.FloorVisuals.Clear();
            Debug.Log($"已拆除机器外壳: {shell.ShellID}");
        }
    }

    // 【整合】：纯粹的抓取与放下循环
    private void HandleGrabAndDrop_Update()
    {
        // 跟随逻辑
        if (_cursorItemVisual != null)
        {
            Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            _cursorItemVisual.transform.position = mouseWorldPos;
        }

        // 点击交互
        if (Input.GetMouseButtonDown(0))
        {
            Vector2Int gridPos = GetMouseGridPosition();
            GridCell cell = GridManager.Instance.GetGridCell(gridPos);
            if (cell == null) return;

            if (_cursorItem == null)
            {
                // 空手抓取
                if (cell.Item != null)
                {
                    _cursorItem = cell.Item;
                    cell.Item = null;
                    SimulationController.Instance.ActiveItems.Remove(_cursorItem);

                    if (_spawnedItems.TryGetValue(_cursorItem, out GameObject visualObj))
                    {
                        Destroy(visualObj);
                        _spawnedItems.Remove(_cursorItem);
                    }

                    _cursorItemVisual = Instantiate(ItemPrefab);
                }
            }
            else
            {
                // 放下物品
                if (cell.Belt != null && cell.Item == null)
                {
                    cell.Item = _cursorItem;
                    _cursorItem.CurrentCell = cell;
                    _cursorItem.Progress = 0.5f;

                    SimulationController.Instance.RegisterItem(_cursorItem);

                    Vector2 spawnPos = GridManager.Instance.GridToWorldPosition(gridPos);
                    GameObject newVisual = Instantiate(ItemPrefab, spawnPos, Quaternion.identity);
                    _spawnedItems.Add(_cursorItem, newVisual);

                    Destroy(_cursorItemVisual);
                    _cursorItem = null;
                    _cursorItemVisual = null;
                }
            }
        }
    }
    #endregion

#region 旧工具方法 (旋转/拆分/调速)
    // 保持原样，没有任何改动
    private void HandleRotate()
    {
        Vector2Int gridPos = GetMouseGridPosition();
        GridCell cell = GridManager.Instance.GetGridCell(gridPos);
        if (cell != null && cell.Belt != null)
        {
            int currentDirInt = (int)cell.Belt.Dir;
            cell.Belt.Dir = (Direction)((currentDirInt + 1) % 4);
            if (_spawnedBelts.TryGetValue(gridPos, out GameObject beltVisual))
                UpdateVisualRotation(beltVisual, cell.Belt.Dir);
            StripManager.Instance.OnBeltModified(gridPos);
        }
    }

    private void HandleSplit()
    {
        Vector2Int gridPos = GetMouseGridPosition();
        GridCell cell = GridManager.Instance.GetGridCell(gridPos);
        if (cell != null && cell.Belt != null)
        {
            Vector2Int forwardPos = gridPos;
            switch (cell.Belt.Dir)
            {
                case Direction.Up: forwardPos.y += 1; break;
                case Direction.Right: forwardPos.x += 1; break;
                case Direction.Down: forwardPos.y -= 1; break;
                case Direction.Left: forwardPos.x -= 1; break;
            }
            GridManager.Instance.ToggleCutEdge(gridPos, forwardPos);
            StripManager.Instance.OnBeltModified(gridPos);
            StripManager.Instance.OnBeltModified(forwardPos); 
        }
    }

    private void HandleSpeedControl(float speedChange)
    {
        Vector2Int gridPos = GetMouseGridPosition();
        GridCell cell = GridManager.Instance.GetGridCell(gridPos);
        if (cell != null && cell.Belt != null && cell.Belt.ParentStrip != null)
        {
            StripData strip = cell.Belt.ParentStrip;
            strip.MoveSpeed = Mathf.Max(0.1f, strip.MoveSpeed + speedChange);
        }
    }

    private void SetTimeSpeed(TimeSpeed newSpeed)
    {
        SimulationController.Instance.CurrentSpeed = newSpeed;
    }

    private void HandleSpawnItem()
    {
        Vector2Int gridPos = GetMouseGridPosition();
        GridCell cell = GridManager.Instance.GetGridCell(gridPos);
        if (cell != null && cell.Belt != null && cell.Item == null)
        {
            ItemData newItem = new ItemData();
            cell.Item = newItem;
            newItem.CurrentCell = cell; 
            SimulationController.Instance.RegisterItem(newItem);
            Vector2 spawnPos = GridManager.Instance.GridToWorldPosition(gridPos);
            GameObject itemObj = Instantiate(ItemPrefab, spawnPos, Quaternion.identity);
            _spawnedItems.Add(newItem, itemObj);
        }
    }
    #endregion

#region 机器模块拼装引擎 (上一轮补充的代码)
    // ==========================================
    // 引擎：机器模块的实时预览与放置逻辑
    // ==========================================
    private void UpdateModulePreviewAndPlacement()
    {
        if (_previewModule == null) return;

        // 1. 获取鼠标在世界空间的网格坐标
        // 【修复】：统一使用标准网格定位，废弃 ScreenToWorldPoint 后的四舍五入
        Vector2Int worldPos = GetMouseGridPosition();

        // 2. 探查鼠标下方的大世界网格，看看有没有“机箱”存在
        GridCell hoverCell = GridManager.Instance.GetGridCell(worldPos);
        MachineShellData targetShell = hoverCell?.ShellRegion;

        bool canPlace = false;

        // 3. 坐标系转换与四重锁校验
        if (targetShell != null)
        {
            // 如果下方有机箱：计算出局部坐标并赋给模块
            Vector2Int localPos = new Vector2Int(worldPos.x - targetShell.Bounds.xMin, worldPos.y - targetShell.Bounds.yMin);
            _previewModule.LocalBottomLeft = localPos;
            
            canPlace = MachineManager.Instance.CanPlaceModule(targetShell, _previewModule);
        }
        else
        {
            // 【Bug修复】：如果下方是空地，将模块的局部原点重置为 (0,0)
            // 这样在外部画图时，坐标才不会叠加之前的残留量
            _previewModule.LocalBottomLeft = Vector2Int.zero; 
            
            canPlace = false;
        }

        // 4. 渲染红绿灯虚影
        UpdatePreviewVisuals(targetShell, canPlace, worldPos);

        // 5. 执行放置 (左键点击)
        if (Input.GetMouseButtonDown(0))
        {
            if (canPlace && targetShell != null)
            {
                // 正式写入数据
                MachineManager.Instance.PlaceModule(targetShell, _previewModule);
                
                // 【注意】：放置成功后清空双手。
                // 如果你想实现像异星工厂一样“点一次放一个，可以连续放置”，
                // 你需要在这里不调用 ResetBuildState，而是重新 new 一个相同类型的模块赋值给 _previewModule。
                // 目前我们先采用最稳妥的“放完就清空”模式：
                ResetBuildState(); 
            }
            else
            {
                Debug.LogWarning("❌ 放置失败：位置不合法或不在机箱内部！");
            }
        }
        
    }

    // ==========================================
    // 渲染：动态更新悬浮的红绿方块
    // ==========================================
    private void UpdatePreviewVisuals(MachineShellData targetShell, bool canPlace, Vector2Int fallbackWorldPos)
    {
        // 颜色定义：绿灯(允许) / 红灯(禁止)
        Color previewColor = canPlace ? new Color(0, 1, 0, 0.5f) : new Color(1, 0, 0, 0.5f);

        // 获取模块当前旋转状态下需要占用的所有坐标
        List<Vector2Int> occupiedCells = _previewModule.GetOccupiedLocalCells();

        // 如果现有的视觉块数量对不上（比如刚按了旋转键，或者刚抓起模块），就重新生成
        if (_previewVisuals.Count != occupiedCells.Count)
        {
            // 清理旧的
            foreach (var v in _previewVisuals) Destroy(v);
            _previewVisuals.Clear();

            // 生成新的 (这里为了演示继续用 Quad，实际开发建议用你自己的 Prefab)
            for (int i = 0; i < occupiedCells.Count; i++)
            {
                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(quad.GetComponent<Collider>()); // 关掉碰撞体防止挡鼠标
                
                Renderer r = quad.GetComponent<Renderer>();
                r.material = new Material(Shader.Find("Sprites/Default")); // 使用支持半透明的默认精灵材质
                
                _previewVisuals.Add(quad);
            }
        }

        // 实时更新每一个方块的位置和颜色
        for (int i = 0; i < occupiedCells.Count; i++)
        {
            Vector2Int localCell = occupiedCells[i];
            Vector2 worldCellPos;

            if (targetShell != null)
            {
                // 如果有机箱，基于机箱原点计算世界坐标
                worldCellPos = new Vector2(targetShell.Bounds.xMin + localCell.x, targetShell.Bounds.yMin + localCell.y);
            }
            else
            {
                // 如果在野外，就直接以鼠标当前位置为基准画红块
                worldCellPos = new Vector2(fallbackWorldPos.x + localCell.x, fallbackWorldPos.y + localCell.y);
            }

            // worldCellPos 目前是个 Vector2（之前写错了类型），把它转回标准的 Grid 坐标再输出
            Vector2Int currentGridPos = new Vector2Int(Mathf.RoundToInt(worldCellPos.x), Mathf.RoundToInt(worldCellPos.y));
            Vector2 exactPos = GridManager.Instance.GridToWorldPosition(currentGridPos);

            _previewVisuals[i].transform.position = new Vector3(exactPos.x, exactPos.y, 0);
            _previewVisuals[i].GetComponent<Renderer>().material.color = previewColor;
        }
    }

    // ==========================================
    // 引擎：机器外壳(Shell)的实时预览与放置逻辑
    // ==========================================
    private void HandleShellPlacement()
    {
        // 假设我们的测试机箱是 3x4 大小
        int shellWidth = 3;
        int shellHeight = 4;

        // 1. 获取鼠标在世界空间的网格坐标 (作为机箱的左下角原点)
        // 【修复】：统一使用你的网格获取方法，废弃 Mathf.RoundToInt
        Vector2Int worldOrigin = GetMouseGridPosition();
        
        bool canPlaceShell = true;

        // 2. 探查大世界网格：确保这 3x4 的区域是完全空旷的
        for (int x = 0; x < shellWidth; x++)
        {
            for (int y = 0; y < shellHeight; y++)
            {
                Vector2Int checkPos = new Vector2Int(worldOrigin.x + x, worldOrigin.y + y);
                GridCell cell = GridManager.Instance.GetGridCell(checkPos);

                // 如果该位置超出了世界地图，或者已经有了别的机箱/传送带，则不允许放置
                if (cell == null || cell.ShellRegion != null || cell.Belt != null)
                {
                    canPlaceShell = false;
                    break;
                }
            }
        }

        // 3. 绘制机箱的红绿灯预览 (复用 _previewVisuals)
        UpdateShellPreviewVisuals(worldOrigin, shellWidth, shellHeight, canPlaceShell);

        // 4. 执行放置 (左键点击)
        if (Input.GetMouseButtonDown(0))
        {
            if (canPlaceShell)
            {
                // 创建真实的机壳数据
                RectInt bounds = new RectInt(worldOrigin.x, worldOrigin.y, shellWidth, shellHeight);
                MachineShellData newShell = new MachineShellData(bounds);

                // 【硬编码加入我们之前设计的测试障碍】
                // 加入横向隔断墙
                newShell.PartitionWalls.Add(new InternalWall(new Vector2Int(0, 1), new Vector2Int(0, 2)));
                newShell.PartitionWalls.Add(new InternalWall(new Vector2Int(1, 1), new Vector2Int(1, 2)));
                // 加入右上角的绝对死区
                newShell.DeadCells.Add(new Vector2Int(2, 3));

                // 正式将机壳注册到大世界网格中，并生成实体的底板视觉效果
                PlaceShellInWorld(newShell);
                
                // 放置完毕，清空双手
                ResetBuildState();
                Debug.Log($"✅ 成功放置了测试机箱！位置: {worldOrigin}");
            }
            else
            {
                Debug.LogWarning("❌ 这里放不下机箱！有东西挡住了。");
            }
        }
    }

    // ==========================================
    // 渲染：更新机壳悬浮预览 (类似于模块预览，但它是 3x4 的一大块)
    // ==========================================
    private void UpdateShellPreviewVisuals(Vector2Int origin, int width, int height, bool canPlace)
    {
        Color previewColor = canPlace ? new Color(0, 1, 0, 0.3f) : new Color(1, 0, 0, 0.3f);

        // 如果视觉块数量不对 (3x4 = 12格)，重新生成
        int requiredCount = width * height;
        if (_previewVisuals.Count != requiredCount)
        {
            foreach (var v in _previewVisuals) Destroy(v);
            _previewVisuals.Clear();

            for (int i = 0; i < requiredCount; i++)
            {
                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(quad.GetComponent<Collider>());
                Renderer r = quad.GetComponent<Renderer>();
                r.material = new Material(Shader.Find("Sprites/Default"));
                _previewVisuals.Add(quad);
            }
        }

        // 排列这 12 个方块，组成 3x4 的形状跟随鼠标
        int index = 0;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // 【修复】：用 GridManager 算出精准的世界坐标
                Vector2Int currentGridPos = new Vector2Int(origin.x + x, origin.y + y);
                Vector2 exactPos = GridManager.Instance.GridToWorldPosition(currentGridPos);
                
                _previewVisuals[index].transform.position = new Vector3(exactPos.x, exactPos.y, 0);
                _previewVisuals[index].GetComponent<Renderer>().material.color = previewColor;
                index++;
            }
        }
    }

    // ==========================================
    // 落地：真正将机壳写入世界，并生成永久的底板图案
    // ==========================================
    private void PlaceShellInWorld(MachineShellData shell)
    {
        for (int x = 0; x < shell.Bounds.width; x++)
        {
            for (int y = 0; y < shell.Bounds.height; y++)
            {
                Vector2Int worldPos = new Vector2Int(shell.Bounds.xMin + x, shell.Bounds.yMin + y);
                Vector2Int localPos = new Vector2Int(x, y);

                // 1. 写入数据：标记该网格属于这个机壳
                GridCell cell = GridManager.Instance.GetGridCell(worldPos);
                if (cell != null)
                {
                    cell.ShellRegion = shell;
                }

                // 2. 生成永久的视觉底板 (这里用简单的颜色方块代替未来的贴图)
                // 【修复】：将网格坐标转换为真实的世界坐标轴，再赋值给生成物
                Vector2 exactWorldPos = GridManager.Instance.GridToWorldPosition(worldPos);

                GameObject floorQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);

                // 【新增】：把生成的肉体存进数据里！
                shell.FloorVisuals.Add(floorQuad);
                
                // 使用转换后的 exactWorldPos，而不是直接用 worldPos.x
                floorQuad.transform.position = new Vector3(exactWorldPos.x, exactWorldPos.y, 0); 
                Destroy(floorQuad.GetComponent<Collider>()); // 去掉碰撞体
                
                Renderer r = floorQuad.GetComponent<Renderer>();
                r.material = new Material(Shader.Find("Sprites/Default"));
                
                // 给死区标记黑色，可用区域标记深灰色
                if (shell.DeadCells.Contains(localPos))
                {
                    r.material.color = new Color(0.1f, 0.1f, 0.1f, 1f); // 黑色死区
                }
                else
                {
                    r.material.color = new Color(0.4f, 0.4f, 0.4f, 0.6f); // 灰色可用底板
                }
                
                // （注意：在实际项目中，你应该把这些生成的视觉对象放在一个统一的父节点下管理，
                // 或者保存在 Shell 数据结构里，方便未来拆除机箱时一起销毁。）
            }
        }
    }
    #endregion

#region 辅助与渲染方法
    // 保持原样
    private Vector2Int GetMouseGridPosition()
    {
        Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        return GridManager.Instance.WorldToGridPosition(mouseWorldPos);
    }

    private void UpdateVisualRotation(GameObject visualObj, Direction dir)
    {
        float angle = 0f;
        switch (dir)
        {
            case Direction.Up: angle = 0f; break;
            case Direction.Right: angle = -90f; break; 
            case Direction.Down: angle = 180f; break;
            case Direction.Left: angle = 90f; break;
        }
        visualObj.transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    public void UpdateBeltColorVisuals()
    {
        foreach (var kvp in _spawnedBelts)
        {
            Vector2Int pos = kvp.Key;
            GridCell cell = GridManager.Instance.GetGridCell(pos);
            if (cell != null && cell.Belt != null && cell.Belt.ParentStrip != null)
            {
                SpriteRenderer sr = kvp.Value.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = cell.Belt.ParentStrip.StripColor;
            }
        }
    }

    public void RenderItems()
    {
        foreach (var kvp in _spawnedItems)
        {
            ItemData itemData = kvp.Key;
            GameObject itemObj = kvp.Value;
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
    #endregion
}