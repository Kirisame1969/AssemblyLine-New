using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using AssemblyLine.Data.Machine;

public class MachineGUIController : MonoBehaviour
{
    public static MachineGUIController Instance { get; private set; }

    [Header("UI 容器与预制体")]
    public RectTransform GridViewport;    // 网格视口遮罩
    public GameObject PanelRoot;          // 面板根节点
    public RectTransform GridContainer;   // 承载 UICell 的缩放容器
    public GameObject UICellPrefab;       // 格子预制体
    public Button CloseButton;            // 关闭按钮

    [Header("视图配置")]
    
    public float BaseCellSize = 64f;      // 基础格子的像素大小 (如 64x64)
    public Vector2 ViewportSize = new Vector2(600, 600); // UI 视口的最大可用大小（用于缩放计算）

    // 当前正在观察的数据源（纯数据模型）
    private MachineShellData _currentShell; 
    
    // UI 格子缓存，用于坐标映射
    private List<GameObject> _spawnedCells = new List<GameObject>(); 
    private Dictionary<Vector2Int, UICell> _cellDict = new Dictionary<Vector2Int, UICell>(); 

    // 【视觉管理核心】：建立数据实例与 UI 贴图物体的映射关系
    // 代替了之前被删除的 MachineShellData.ModuleVisuals
    private Dictionary<MachineModuleData, GameObject> _moduleUIDict = new Dictionary<MachineModuleData, GameObject>();

    [Header("交互状态")]
    public ModuleDefinition TestModuleDef; 
    private ModuleDefinition _selectedModuleDef; 
    private MachineModuleData _previewModuleData; // 放置前的虚影数据替身
    private Vector2Int _currentHoverPos = new Vector2Int(-1, -1);

    [Header("侧边栏与导航")]
    public ModuleCatalog CurrentCatalog; 
    public Transform SidebarContent; 
    public GameObject UIModuleEntryPrefab; 
    public RectTransform DragGhost; 
    public Image DragGhostImage; 
    public UITabButton[] TabButtons; 
    public GameObject[] TabPages;

    [Header("端口配置 UI")]
    public AssemblyLine.UI.UIPortConfigPanel PortConfigPanel;

    [Header("仓库库存 UI")]
    public AssemblyLine.UI.UIWarehousePanel WarehousePanel; // 拖入挂载了此脚本的 TabPages[1] 节点
    
    // 【新增】：状态互斥锁，记录当前激活的是哪个分页
    private int _currentTabIndex = 0;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 游戏开始时隐藏面板
        if (PanelRoot != null) PanelRoot.SetActive(false);

        // 【新增】：代码绑定关闭按钮事件，点 X 即可关闭
        if (CloseButton != null)
        {
            CloseButton.onClick.AddListener(ClosePanel);
        }
    }

    // ==========================================
    // 外部入口：打开面板并绑定数据
    // ==========================================

    /// <summary>
    /// 打开界面并同步机箱数据。
    /// </summary>
    public void OpenPanel(MachineShellData shell)
    {
        _currentShell = shell;
        PanelRoot.SetActive(true);
        // 暂停游戏大世界时间 (按需)
        SimulationController.Instance.CurrentSpeed = TimeSpeed.Paused;

        // 【新增】：判断是否为仓库，控制 Tab 页权限
        bool isWarehouse = shell.MainCore is WarehouseCoreData;
        if (TabButtons.Length > 1) 
        {
            // 如果不是仓库，直接隐藏“库存”按钮标签
            TabButtons[1].gameObject.SetActive(isWarehouse); 
        }

        // 重置 Tab 为初始状态
        SwitchTab(0);
        GenerateGrid();

        // 每次打开面板时，刷新左侧侧边栏
        GenerateSidebar();

        // 打开面板时，把机器里已经存在的模块渲染出来
        foreach (var module in shell.Modules)
        {
            SpawnUIModuleVisual(module);
        }

        // 【新增】：如果是仓库，初始化库存面板数据映射
        if (isWarehouse && WarehousePanel != null)
        {
            WarehousePanel.InitPanel(shell.MainCore as WarehouseCoreData);
        }
    }

    // 关闭面板
    public void ClosePanel()
    {
        PanelRoot.SetActive(false);
        _currentShell = null;

        // 关闭面板时，清空手里抓着的模块
        ClearHands();

    }





    // ==========================================
    // 核心引擎：生成网格并执行自适应缩放
    // ==========================================
    private void GenerateGrid()
    {
        // 1. 清理所有旧表现
        foreach (var cell in _spawnedCells) Destroy(cell);
        _spawnedCells.Clear();
        
        // 【极其关键】：确保清空字典，为重新注册做准备
        _cellDict.Clear(); 

        foreach (var kvp in _moduleUIDict) Destroy(kvp.Value);
        _moduleUIDict.Clear();

        if (_currentShell == null || _currentShell.Profile == null) return;

        MachineShellProfile profile = _currentShell.Profile;
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        // 依据配置文件的逻辑宽高生成交互格子
        for (int x = 0; x < profile.LogicWidth; x++)
        {
            for (int y = 0; y < profile.LogicHeight; y++)
            {
                Vector2Int logicPos = new Vector2Int(x, y);
                
                // 获取格子的视觉偏移量（支持非对齐布局）
                CellVisualConfig visualConfig = GetVisualConfig(profile, logicPos);
                // 如果找不到，GetVisualConfig 默认返回坐标为 (-1,-1) 的空壳，以此作为判断依据
                if (visualConfig.LogicalPos.x == -1) continue;

                GameObject cellObj = Instantiate(UICellPrefab, GridContainer);
                RectTransform rt = cellObj.GetComponent<RectTransform>();
                rt.anchoredPosition = visualConfig.VisualOffset;
                rt.sizeDelta = new Vector2(BaseCellSize, BaseCellSize);

                UICell cellScript = cellObj.GetComponent<UICell>();
                if (cellScript != null)
                {
                    bool isDead = profile.DeadCells.Contains(logicPos);
                    cellScript.Init(logicPos, visualConfig, isDead);
                    _cellDict[logicPos] = cellScript; 
                }

                _spawnedCells.Add(cellObj);

                // 更新边界用于缩放计算
                minX = Mathf.Min(minX, visualConfig.VisualOffset.x);
                minY = Mathf.Min(minY, visualConfig.VisualOffset.y);
                maxX = Mathf.Max(maxX, visualConfig.VisualOffset.x + BaseCellSize);
                maxY = Mathf.Max(maxY, visualConfig.VisualOffset.y + BaseCellSize);
            }
        }

        AutoFitGrid(minX, minY, maxX, maxY);
    }

    /// <summary>
    /// 自适应网格缩放。确保不同尺寸的机箱在 UI 窗口中都能完整显示。
    /// </summary>
    private void AutoFitGrid(float minX, float minY, float maxX, float maxY)
    {
        if (_spawnedCells.Count == 0) return;

        float contentWidth = maxX - minX;
        float contentHeight = maxY - minY;

        float scaleX = ViewportSize.x / contentWidth;
        float scaleY = ViewportSize.y / contentHeight;
        float finalScale = Mathf.Min(scaleX, scaleY) * 0.9f;
        finalScale = Mathf.Min(finalScale, 1.5f);

        GridContainer.localScale = new Vector3(finalScale, finalScale, 1f);

        float centerX = minX + contentWidth / 2f;
        float centerY = minY + contentHeight / 2f;
        GridContainer.anchoredPosition = new Vector2(-centerX * finalScale, -centerY * finalScale);
    }

    

    /// <summary>
    /// 为指定的模块数据创建一个对应的 UI 视觉对象。
    /// </summary>
    private void SpawnUIModuleVisual(MachineModuleData module)
    {
        List<Vector2Int> occupied = module.GetOccupiedLocalCells();
        if (occupied.Count == 0 || module.Definition == null) return;

        // 计算模块未旋转时的原始像素尺寸
        Vector2 baseSize = CalculateModuleBaseSize(module.Definition);

        // 计算当前所有占据格子在 UI 上的几何中心点
        Vector2 visualCenter = CalculateOccupiedCenter(occupied);

        // 创建 UI 表现层对象
        GameObject modObj = new GameObject("UIModule_" + module.Definition.ModuleID);
        modObj.transform.SetParent(GridContainer, false);
        modObj.transform.SetAsLastSibling(); 

        Image img = modObj.AddComponent<Image>();
        img.sprite = module.Definition.Icon; 
        img.raycastTarget = false; 

        RectTransform rt = modObj.GetComponent<RectTransform>();
        rt.sizeDelta = baseSize;
        rt.anchoredPosition = visualCenter;

        // 根据旋转数据应用视觉旋转角度
        rt.localRotation = Quaternion.Euler(0, 0, GetRotationAngle(module.Rotation));

        // 【关键修复】：将新生成的 GameObject 存入表现层字典，供后续拆卸时销毁
        _moduleUIDict[module] = modObj;
    }










    
    // ==========================================
    // 交互输入总控 (Update)
    // ==========================================
    private void Update()
    {
        if (_currentShell == null) return;

        // 【修复 2】：优先拦截 ESC 键，实现层级退栈
        if (Input.GetKeyDown(KeyCode.Escape)) 
        { 
            // 如果子面板开启，只关闭子面板
            if (PortConfigPanel != null && PortConfigPanel.PanelRoot != null && PortConfigPanel.PanelRoot.activeSelf)
            {
                PortConfigPanel.ClosePanel();
            }
            // 否则关闭整个机器大面板
            else
            {
                ClosePanel(); 
            }
            return; 
        }

        // 【关键防御】：只有在“装配视图”(Tab 0) 时，才处理内部大网格的交互！
        // 防止玩家在“库存视图”点击背包格子时，误把手里的模块塞进了机箱里！
        if (_currentTabIndex == 0)
        {
            // 【修复 1】：颠倒控制链的执行顺序！
            // 必须先处理交互（此时若手里有模块会被直接 return 阻断），再处理放置。
            // 这彻底杜绝了“放置后清空手导致同帧误触”的幽灵穿透 Bug。
            HandleModuleInteraction();
            HandleModulePlacement();
            HandleModuleRemoval();
        }

    }

    //处理模块放置
    private void HandleModulePlacement()
    {
        if (_selectedModuleDef == null) return;

        DragGhost.position = Input.mousePosition;

        if (Input.GetKeyDown(KeyCode.R))
        {
            _previewModuleData.Rotation = (ModuleRotation)(((int)_previewModuleData.Rotation + 1) % 4);
            DragGhost.localRotation *= Quaternion.Euler(0, 0, -90f); 
            RefreshPreview();
        }

        if (Input.GetMouseButtonDown(1)) { ClearHands(); return; }

        if (Input.GetMouseButtonDown(0) && _currentHoverPos.x != -1)
        {
            // 呼叫控制层进行物理仲裁
            if (MachineManager.Instance.CanPlaceModule(_currentShell, _previewModuleData))
            {
                // 创建正式的运行时数据实例
                MachineModuleData finalModule = _selectedModuleDef.CreateRuntimeInstance();
                finalModule.LocalBottomLeft = _currentHoverPos;
                finalModule.Rotation = _previewModuleData.Rotation;

                // 写入 Model 层
                MachineManager.Instance.PlaceModule(_currentShell, finalModule);
                
                // 【新增】：通知 UI 重算网格容量 (如果当前是仓库的话)
                if (_currentShell.MainCore is WarehouseCoreData && WarehousePanel != null)
                {
                    WarehousePanel.SyncGridCapacity();
                }
                
                // 刷新 View 层视觉
                SpawnUIModuleVisual(finalModule);
                
                // 刷新大世界物流标识（如有）
                InteractionController.Instance.RefreshPortOverlayVisuals(_currentShell);
                
                ApplyInitialConfiguration(finalModule);
                ClearHands(); 
            }
        }
    }

    //处理模块移除
    private void HandleModuleRemoval()
    {
        if (_selectedModuleDef != null || _currentHoverPos.x == -1) return;

        if (Input.GetMouseButtonDown(1)) 
        {
            Vector2Int worldPos = new Vector2Int(_currentShell.Bounds.xMin + _currentHoverPos.x, _currentShell.Bounds.yMin + _currentHoverPos.y);
            GridCell cell = GridManager.Instance.GetGridCell(worldPos);

            if (cell != null && cell.OccupyingModule != null && cell.OccupyingModule.ParentShell == _currentShell)
            {
                MachineModuleData targetModule = cell.OccupyingModule;

                // 1. 通知控制层删除数据
                MachineManager.Instance.RemoveModule(_currentShell, targetModule);
                // 【新增】：通知 UI 重算网格容量 (如果当前是仓库的话)
                if (_currentShell.MainCore is WarehouseCoreData && WarehousePanel != null)
                {
                    WarehousePanel.SyncGridCapacity();
                }

                // 2. 表现层根据字典查找到 GameObject 并销毁
                if (_moduleUIDict.TryGetValue(targetModule, out GameObject uiObj))
                {
                    Destroy(uiObj);
                    _moduleUIDict.Remove(targetModule);
                }

                InteractionController.Instance.RefreshPortOverlayVisuals(_currentShell);
            }
        }
    }
    


    // ==========================================
    // 辅助工具方法
    // ==========================================

    private Vector2 CalculateModuleBaseSize(ModuleDefinition def)
    {
        int minX = 0, maxX = 0, minY = 0, maxY = 0;
        foreach (var pos in def.Shape.BaseCells)
        {
            minX = Mathf.Min(minX, pos.x); maxX = Mathf.Max(maxX, pos.x);
            minY = Mathf.Min(minY, pos.y); maxY = Mathf.Max(maxY, pos.y);
        }
        return new Vector2((maxX - minX + 1) * BaseCellSize, (maxY - minY + 1) * BaseCellSize);
    }

    private Vector2 CalculateOccupiedCenter(List<Vector2Int> occupied)
    {
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (Vector2Int pos in occupied)
        {
            if (_cellDict.TryGetValue(pos, out UICell cell))
            {
                Vector2 posInUI = cell.GetComponent<RectTransform>().anchoredPosition;
                minX = Mathf.Min(minX, posInUI.x);
                minY = Mathf.Min(minY, posInUI.y);
                maxX = Mathf.Max(maxX, posInUI.x);
                maxY = Mathf.Max(maxY, posInUI.y);
            }
        }
        return new Vector2((minX + maxX) / 2f, (minY + maxY) / 2f);
    }

    private float GetRotationAngle(ModuleRotation rot)
    {
        switch (rot)
        {
            case ModuleRotation.R0: return 0f;
            case ModuleRotation.R90: return -90f;
            case ModuleRotation.R180: return 180f;
            case ModuleRotation.R270: return 90f;
            default: return 0f;
        }
    }

    private CellVisualConfig GetVisualConfig(MachineShellProfile profile, Vector2Int logicPos)
    {
        if (profile.VisualConfigs == null || profile.VisualConfigs.Count == 0)
        {
            return new CellVisualConfig { LogicalPos = logicPos, VisualOffset = new Vector2(logicPos.x * BaseCellSize, logicPos.y * BaseCellSize) };
        }

        int index = profile.VisualConfigs.FindIndex(c => c.LogicalPos == logicPos);
        if (index >= 0) return profile.VisualConfigs[index];

        // 找不到时返回一个无效的占位符
        return new CellVisualConfig { LogicalPos = new Vector2Int(-1, -1) };
    }

    private void ApplyInitialConfiguration(MachineModuleData module)
    {
        if (module is MachineCoreData core)
        {
            if (core.GetType() == typeof(MachineCoreData) && InteractionController.Instance.TestRecipe != null)
                MachineManager.Instance.SetRecipe(_currentShell, 0, InteractionController.Instance.TestRecipe);
            else if (module is ImporterCoreData importer && InteractionController.Instance.TestImportItem != null)
                importer.TargetItem = InteractionController.Instance.TestImportItem;
        }
    }

    private void ClearHands()
    {
        _selectedModuleDef = null;
        _previewModuleData = null;
        DragGhost.gameObject.SetActive(false);
        RefreshPreview();
    }

    public void OnCellHoverEnter(Vector2Int logicPos) { _currentHoverPos = logicPos; RefreshPreview(); }
    public void OnCellHoverExit(Vector2Int logicPos) { if (_currentHoverPos == logicPos) { _currentHoverPos = new Vector2Int(-1, -1); RefreshPreview(); } }

    private void RefreshPreview()
    {
        foreach (var kvp in _cellDict) kvp.Value.ClearHighlight();
        if (_selectedModuleDef == null || _currentShell == null || _currentHoverPos.x == -1) return;

        _previewModuleData.LocalBottomLeft = _currentHoverPos;
        List<Vector2Int> occupied = _previewModuleData.GetOccupiedLocalCells();
        bool canPlace = MachineManager.Instance.CanPlaceModule(_currentShell, _previewModuleData);
        Color tint = canPlace ? new Color(0, 1, 0, 0.5f) : new Color(1, 0, 0, 0.5f);

        foreach (Vector2Int pos in occupied)
        {
            if (_cellDict.TryGetValue(pos, out UICell cell)) cell.SetHighlight(tint);
        }
    }

    private void GenerateSidebar()
    {
        foreach (Transform child in SidebarContent) Destroy(child.gameObject);
        if (CurrentCatalog == null) return;
        foreach (var modDef in CurrentCatalog.AvailableModules)
        {
            GameObject entryObj = Instantiate(UIModuleEntryPrefab, SidebarContent);
            UIModuleEntry entryScript = entryObj.GetComponent<UIModuleEntry>();
            if (entryScript != null) entryScript.Init(modDef);
        }
    }

    public void PickUpModule(ModuleDefinition targetDef)
    {
        _selectedModuleDef = targetDef;
        _previewModuleData = targetDef.CreateRuntimeInstance();
        DragGhost.gameObject.SetActive(true);
        DragGhostImage.sprite = targetDef.Icon;
        DragGhost.localRotation = Quaternion.identity; 
        RefreshPreview();
    }

    public void SwitchTab(int targetIndex)
    {
        _currentTabIndex = targetIndex; // 【关键修改】：记录当前页

        for (int i = 0; i < TabButtons.Length; i++) TabButtons[i]?.SetSelectedStatus(i == targetIndex);
        for (int i = 0; i < TabPages.Length; i++) TabPages[i]?.SetActive(i == targetIndex);
    }

    /// <summary>
    /// 处理对已放置模块的配置交互（如：点击输出匣设置白名单）
    /// </summary>
    private void HandleModuleInteraction()
    {
        // 【新增防御】：如果配置面板已经打开，直接阻断所有大网格底层的点击交互
        if (PortConfigPanel != null && PortConfigPanel.PanelRoot != null && PortConfigPanel.PanelRoot.activeSelf) return;
        
        // 核心防御：必须是空手状态（未选中模块），且鼠标悬停在有效格子上
        if (_selectedModuleDef != null || _currentHoverPos.x == -1) return;

        // 检测左键点击
        if (Input.GetMouseButtonDown(0))
        {
            Vector2Int worldPos = new Vector2Int(_currentShell.Bounds.xMin + _currentHoverPos.x, _currentShell.Bounds.yMin + _currentHoverPos.y);
            GridCell cell = GridManager.Instance.GetGridCell(worldPos);

            // 如果该格子上存在属于本机箱的模块
            if (cell != null && cell.OccupyingModule != null && cell.OccupyingModule.ParentShell == _currentShell)
            {
                // 【多态寻址】：判断该模块是否为可配置端口！
                if (cell.OccupyingModule is IConfigurablePort configurablePort)
                {
                    // 呼出配置面板，将该端口的规则引用传给面板
                    if (PortConfigPanel != null)
                    {
                        PortConfigPanel.OpenPanel(configurablePort.GetRules());
                    }
                }
            }
        }
    }
}