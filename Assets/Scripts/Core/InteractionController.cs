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

    /*
    26.4.4 彻底删除原先用于大世界模块预览的 _previewModule 和红绿方块 _previewVisuals 
    [Header("机器拼装预览状态")]
    private MachineModuleData _previewModule = null; 
    private List<GameObject> _previewVisuals = new List<GameObject>();
    */
    // 26.4.4 新增专门用于“机器外壳 (Shell)”在大世界放置时的红绿预览列表。
    [Header("大世界机壳预览状态")]
    private List<GameObject> _shellPreviewVisuals = new List<GameObject>();

    [Header("上帝之手(物品抓取)状态")]
    private ItemData _cursorItem = null;          // 鼠标当前抓着的物品数据
    private GameObject _cursorItemVisual = null;  // 鼠标当前抓着的物品视觉表现
#endregion

#region 视觉表现与字典引用
    [Header("视觉预制体")]
    public GameObject BeltPrefab;
    public GameObject ItemPrefab;

    /*
    // 26.4.4 此两份配置将移动到UI管理器
    public Sprite CoreSprite;         // 核心贴图
    public Sprite DefaultModuleSprite;// 普通模块贴图
    */
    public Sprite InputPortSprite;    // 输入匣贴图
    public Sprite OutputPortSprite;   // 输出匣贴图


    private Dictionary<Vector2Int, GameObject> _spawnedBelts = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<ItemData, GameObject> _spawnedItems = new Dictionary<ItemData, GameObject>();

    [Header("配置测试")]
    public ItemDefinition TestSpawnItem; // 测试按 I 键生成的物品 (拖入铁矿)
    public RecipeDefinition TestRecipe;  // 测试用的核心配方 (拖入熔炼配方)
    public MachineShellProfile TestShellProfile;    //测试机器外壳


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
        //if (EventSystem.current.IsPointerOverGameObject() && Input.GetMouseButtonDown(0)) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            // 只要鼠标在 UI 上，就直接 return，彻底切断向下执行的大世界交互逻辑！
            // 这样无论你怎么点击面板，背后的传送带和机器都不会有任何反应。
            return; 
        }
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

            /*
            case BuildableType.Module_Core_1x1:
            case BuildableType.Module_Rect_2x1:
            case BuildableType.Module_Rect_2x2:
            case BuildableType.Module_InputPort:
            case BuildableType.Module_OutputPort:
                // 模块拼装逻辑
                UpdateModulePreviewAndPlacement();
                if (Input.GetKeyDown(KeyCode.R) && _previewModule != null)
                {
                    _previewModule.Rotation = (ModuleRotation)(((int)_previewModule.Rotation + 1) % 4);
                }
                break;
              */
        }
    }
    #endregion

#region UI 交互与状态清理
    public void OnBuildButtonClicked(int typeIndex)
    {
        ResetBuildState();
        CurrentBuildType = (BuildableType)typeIndex;
        /* 26.4.4 删除了选中模块时的图纸实例化逻辑
        switch (CurrentBuildType)
        {
            case BuildableType.ConveyorBelt: 
                Debug.Log("UI：传送带模式"); 
                break;
            case BuildableType.MachineShell_Test: 
                Debug.Log("UI：机器外壳模式"); 
                break;
           case BuildableType.Module_Core_1x1:
                Debug.Log("UI选中：1x1 机器核心");
                MachineCoreData newCore = new MachineCoreData();
                newCore.CurrentRecipe = TestRecipe; // 【关键】：把配方图纸塞进核心的脑袋里！
                SelectModule(newCore);
                break;
            case BuildableType.Module_Rect_2x1: 
                SelectModule(new RectModuleData(2, 1)); 
                Debug.Log("UI：1x2模块");
                break;
            case BuildableType.Module_Rect_2x2: 
                SelectModule(new RectModuleData(2, 2)); 
                Debug.Log("UI：2x2模块");
                break;
            case BuildableType.Module_InputPort:
                Debug.Log("UI：输入匣");
                SelectModule(new InputPortData());
                break;
            case BuildableType.Module_OutputPort:
                Debug.Log("UI：输出匣");
                SelectModule(new OutputPortData());
                break;
            case BuildableType.None: 
                Debug.Log("UI：空手模式"); 
                break;
        }
        */
    }
    /*
    // 26.4.4 作废代码
    private void SelectModule(MachineModuleData moduleData)
    {
        _previewModule = moduleData;
    }
    */
    private void ResetBuildState()
    {
        if (_cursorItemVisual != null)
        {
            Destroy(_cursorItemVisual);
            _cursorItemVisual = null;
            _cursorItem = null; 
        }
        /*
        26.4.4 模块删除后在世界中清除相关部分的逻辑也不再需要
        foreach (var visual in _previewVisuals) Destroy(visual);
        _previewVisuals.Clear();
        _previewModule = null;
        */
        //26.4.4 清理大世界机壳的预览方块
        foreach (var visual in _shellPreviewVisuals) Destroy(visual);
        _shellPreviewVisuals.Clear();

        CurrentBuildType = BuildableType.None;
    }
    #endregion

#region 具体建造与拆除逻辑
    
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
            // 销毁画面上的视觉底板
            foreach (GameObject visualQuad in shell.FloorVisuals)
            {
                Destroy(visualQuad);
            }
            shell.FloorVisuals.Clear();

            // 将机箱内部所有的模块贴图一并销毁,这里保留,用于销毁I/O匣
            foreach (GameObject modVisual in shell.ModuleVisuals)
            {
                Destroy(modVisual);
            }
            shell.ModuleVisuals.Clear();

            MachineManager.Instance.AllActiveShells.Remove(shell);
            //移除机箱在机器管理器里的注册

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

                    // ==========================================
                    // 【关键修复 1】：抓取到鼠标上时，赋予真实的贴图！
                    // ==========================================
                    SpriteRenderer sr = _cursorItemVisual.GetComponent<SpriteRenderer>();
                    if (sr != null && _cursorItem.Definition != null)
                    {
                        sr.sprite = _cursorItem.Definition.Icon;
                    }
                }
                //26.4.4 新增 [如果格子上没物品，但点击了机箱领地 -> 打开GUI]
                else if (cell.ShellRegion != null)
                {
                    Debug.Log($"点击了机箱 {cell.ShellRegion.ShellID}，准备打开内部配置 GUI 面板！");
                    // 预留接口，下一步我们将在这里呼叫 UI 管理器
                    MachineGUIController.Instance.OpenPanel(cell.ShellRegion);
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

                    // ==========================================
                    // 【关键修复 2】：放回传送带时，赋予真实的贴图！
                    // ==========================================
                    SpriteRenderer sr = newVisual.GetComponent<SpriteRenderer>();
                    if (sr != null && _cursorItem.Definition != null)
                    {
                        sr.sprite = _cursorItem.Definition.Icon;
                    }

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

    // --- 物品生成逻辑 ---
    private void HandleSpawnItem()
    {
        if (TestSpawnItem == null) return; // 防呆：没挂载配置就不生成

        Vector2Int gridPos = GetMouseGridPosition();
        GridCell cell = GridManager.Instance.GetGridCell(gridPos);

        if (cell != null && cell.Belt != null && cell.Item == null)
        {
            // 【改变】：现在生成物品必须传入它是什么东西的定义
            ItemData newItem = new ItemData(TestSpawnItem); 
            cell.Item = newItem;
            newItem.CurrentCell = cell; 
            SimulationController.Instance.RegisterItem(newItem);
            
            SpawnItemVisual(newItem, gridPos); // 直接调用下面的视觉生成
        }
    }

    #endregion

#region 机器模块拼装引擎 (上一轮补充的代码)
    /* 26.4.4 作废代码
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
                // 【新增】：底层数据写完后，在画面上真正生成你的精美贴图！
                SpawnModuleVisual(targetShell, _previewModule);
                ResetBuildState(); 
            }
            else
            {
                Debug.LogWarning("放置失败：位置不合法或不在机箱内部！");
            }
        }
        
    }
    */

    /* 26.4.4 作废代码
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
    */

    // ==========================================
    // 引擎：机器外壳(Shell)的实时预览与放置逻辑
    // ==========================================
    private void HandleShellPlacement()
    {
        // 1. 【最关键的防呆拦截】：如果你没有在 Inspector 面板里拖入图纸，直接拦截！
        // 连绿色的预览框都不会显示，并且控制台会告诉你原因，彻底杜绝报错！
        if (TestShellProfile == null)
        {
            Debug.LogWarning("【严重警告】你忘记在 InteractionController 面板的 TestShellProfile 槽位拖入图纸了！");
            return; 
        }

        // 2. 彻底抛弃硬编码，直接从你配置的图纸读取尺寸
        int shellWidth = TestShellProfile.LogicWidth;
        int shellHeight = TestShellProfile.LogicHeight;

        Vector2Int worldOrigin = GetMouseGridPosition();
        bool canPlaceShell = true;

        // 3. 探查大世界网格：确保这片区域是完全空旷的
        for (int x = 0; x < shellWidth; x++)
        {
            for (int y = 0; y < shellHeight; y++)
            {
                Vector2Int checkPos = new Vector2Int(worldOrigin.x + x, worldOrigin.y + y);
                GridCell cell = GridManager.Instance.GetGridCell(checkPos);

                if (cell == null || cell.ShellRegion != null || cell.Belt != null)
                {
                    // ====== 照妖镜代码开始 ======
                    bool isOutOfBounds = (cell == null);
                    bool hasMachine = (cell != null && cell.ShellRegion != null);
                    bool hasBelt = (cell != null && cell.Belt != null);

                    Debug.Log($"❌ 阻挡点分析 -> 鼠标点算出的网格坐标: {checkPos} | 是越界(null)吗: {isOutOfBounds} | 有机器: {hasMachine} | 有传送带: {hasBelt}");
                    // ====== 照妖镜代码结束 ======

                    canPlaceShell = false;
                    break;
                }
            }
        }

        // 4. 绘制机箱的红绿灯预览
        UpdateShellPreviewVisuals(worldOrigin, shellWidth, shellHeight, canPlaceShell);

        // 5. 执行放置 (左键点击)
        if (Input.GetMouseButtonDown(0))
        {
            if (canPlaceShell)
            {
                RectInt bounds = new RectInt(worldOrigin.x, worldOrigin.y, shellWidth, shellHeight);
                
                // 实例化真实数据（传入你配置好的图纸）
                MachineShellData newShell = new MachineShellData(TestShellProfile, bounds);

                // 安全读取图纸中的物理限制配置
                newShell.DeadCells = TestShellProfile.DeadCells != null 
                    ? new HashSet<Vector2Int>(TestShellProfile.DeadCells) 
                    : new HashSet<Vector2Int>();

                newShell.PartitionWalls = TestShellProfile.PartitionWalls != null 
                    ? new HashSet<InternalWall>(TestShellProfile.PartitionWalls) 
                    : new HashSet<InternalWall>();

                // ====================================================
                // 【终极防呆补丁】：强制初始化内部容器，防止底层漏写导致 NRE
                // ====================================================
                if (newShell.FloorVisuals == null) newShell.FloorVisuals = new List<GameObject>();
                if (newShell.Modules == null) newShell.Modules = new List<MachineModuleData>();
                if (newShell.ModuleVisuals == null) newShell.ModuleVisuals = new List<GameObject>();
                if (newShell.InputPorts == null) newShell.InputPorts = new List<InputPortData>();
                if (newShell.OutputPorts == null) newShell.OutputPorts = new List<OutputPortData>();
                
                if (MachineManager.Instance != null && MachineManager.Instance.AllActiveShells == null)
                {
                    MachineManager.Instance.AllActiveShells = new List<MachineShellData>();
                }
                // ====================================================

                // 正式将机壳注册到大世界网格中
                PlaceShellInWorld(newShell);
                
                ResetBuildState();
                Debug.Log($"✅ 成功按照图纸【{TestShellProfile.DisplayName}】放置了机壳！位置: {worldOrigin}");
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
        // 26.4.4 将原先已被删除的 _previewVisuals 全面替换为 _shellPreviewVisuals
        int requiredCount = width * height;
        if (_shellPreviewVisuals.Count != requiredCount)
        {
            foreach (var v in _shellPreviewVisuals) Destroy(v);
            _shellPreviewVisuals.Clear();

            for (int i = 0; i < requiredCount; i++)
            {
                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(quad.GetComponent<Collider>());
                Renderer r = quad.GetComponent<Renderer>();
                r.material = new Material(Shader.Find("Sprites/Default"));
                _shellPreviewVisuals.Add(quad);
            }
        }

        // 排列这 12 个方块，组成 3x4 的形状跟随鼠标
        int index = 0;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // 用 GridManager 算出精准的世界坐标
                Vector2Int currentGridPos = new Vector2Int(origin.x + x, origin.y + y);
                Vector2 exactPos = GridManager.Instance.GridToWorldPosition(currentGridPos);
                
                _shellPreviewVisuals[index].transform.position = new Vector3(exactPos.x, exactPos.y, 0);
                _shellPreviewVisuals[index].GetComponent<Renderer>().material.color = previewColor;
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

        // ==========================================
        // 2. 【修复】：把注册代码移到循环外面！整个机箱只注册 1 次！
        // 顺便加上 Contains 检查，防止以后任何误操作导致的重复注册
        // ==========================================
        if (!MachineManager.Instance.AllActiveShells.Contains(shell))
        {
            MachineManager.Instance.AllActiveShells.Add(shell);
            Debug.Log($"[系统] 新机箱已注册！当前世界上共有 {MachineManager.Instance.AllActiveShells.Count} 台机器在运转。");
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
    

    // ==========================================
    // 视觉落地：在机箱内生成真实的模块贴图实体    
    // ==========================================
    // 26.4.4 改名并降级视觉生成。不再大张旗鼓地渲染核心贴图，只覆盖 I/O 匣子开口
    // 拟改名: SpawnPortOverlayVisual
    // 供未来的 UI 面板拼装成功后调用

    public void SpawnPortOverlayVisual(MachineShellData shell, MachineModuleData module)
    {
        // 只有输入匣和输出匣需要在大世界上“留痕”
        if (!(module is InputPortData) && !(module is OutputPortData)) return;

        foreach (Vector2Int localPos in module.GetOccupiedLocalCells())
        {
            Vector2Int worldGridPos = new Vector2Int(shell.Bounds.xMin + localPos.x, shell.Bounds.yMin + localPos.y);
            Vector2 exactPos = GridManager.Instance.GridToWorldPosition(worldGridPos);

            GameObject modVisual = new GameObject($"PortOverlay_{module.GetType().Name}");
            modVisual.transform.position = new Vector3(exactPos.x, exactPos.y, -0.1f); 
            SpriteRenderer sr = modVisual.AddComponent<SpriteRenderer>();

            if (module is InputPortData inputPort)
            {
                sr.sprite = InputPortSprite;
                UpdateVisualRotation(modVisual, inputPort.FacingDir); 
            }
            else if (module is OutputPortData outputPort)
            {
                sr.sprite = OutputPortSprite;
                UpdateVisualRotation(modVisual, outputPort.FacingDir);
            }

            shell.ModuleVisuals.Add(modVisual);
        }
    }

    // ==========================================
    // 【Phase 4 新增】：刷新大世界机箱端口的视觉表现
    // 供 UI 面板放置/拆除模块后调用
    // ==========================================
    public void RefreshPortOverlayVisuals(MachineShellData shell)
    {
        if (shell == null) return;

        // 1. 清理该机箱旧的大世界模块视觉贴图
        foreach (var visual in shell.ModuleVisuals)
        {
            if (visual != null) Destroy(visual);
        }
        shell.ModuleVisuals.Clear();

        // 2. 遍历当前机箱底层真实的数据，重新生成 I/O 匣子的印记
        foreach (var module in shell.Modules)
        {
            SpawnPortOverlayVisual(shell, module);
        }
    }

    // ==========================================
    // 供外部物流系统调用的物品视觉接口
    // ==========================================
    public void SpawnItemVisual(ItemData item, Vector2Int gridPos)
    {
        Vector2 spawnPos = GridManager.Instance.GridToWorldPosition(gridPos);
        GameObject itemObj = Instantiate(ItemPrefab, spawnPos, Quaternion.identity);
        
        // 【关键体验升级】：将预制体上的精灵替换为你图纸里的专属图标！
        SpriteRenderer sr = itemObj.GetComponent<SpriteRenderer>();
        if (sr != null && item.Definition != null)
        {
            sr.sprite = item.Definition.Icon;
        }

        _spawnedItems.Add(item, itemObj);
    }

    public void DestroyItemVisual(ItemData item)
    {
        if (_spawnedItems.TryGetValue(item, out GameObject visualObj))
        {
            Destroy(visualObj);
            _spawnedItems.Remove(item);
        }
    }

#endregion

}