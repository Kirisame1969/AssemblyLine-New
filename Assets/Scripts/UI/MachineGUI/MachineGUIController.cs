using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
// 【新增】：如果未来使用 ScrollRect，需要引入 UI 命名空间
using UnityEngine.EventSystems;

public class MachineGUIController : MonoBehaviour
{
    public static MachineGUIController Instance { get; private set; }

    [Header("UI 容器与预制体")]
    public RectTransform GridViewport;    // 【新增】：网格的视口遮罩节点（Step 3 限制拖拽不出界时需要用到它的尺寸）
    public GameObject PanelRoot;          // 整个机器面板的根节点（用于控制显隐）
    public RectTransform GridContainer;   // 承载所有 UICell 的父节点（缩放引擎的作用对象）
    public GameObject UICellPrefab;       // 上面写好的 UICell 预制体
    public Button CloseButton;// 【新增】：关闭按钮引用

    [Header("视图配置")]
    public float BaseCellSize = 64f;      // 基础格子的像素大小 (如 64x64)
    public Vector2 ViewportSize = new Vector2(600, 600); // UI 视口的最大可用大小（用于缩放计算）

    private MachineShellData _currentShell; // 当前正在查看的底层机箱数据
    private List<GameObject> _spawnedCells = new List<GameObject>(); // 已生成的 UI 格子缓存

    [Header("交互测试配置 (按T键获取)")]
    public ModuleDefinition TestModuleDef; // 【新增】：请在 Inspector 拖入一个模块图纸作为测试

    // 【新增】：字典缓存，方便通过逻辑坐标快速找到对应的 UI 实体变颜色
    private Dictionary<Vector2Int, UICell> _cellDict = new Dictionary<Vector2Int, UICell>(); 
    private Dictionary<MachineModuleData, GameObject> _moduleUIDict = new Dictionary<MachineModuleData, GameObject>();  // 存放生成的模块贴图
    
    // 【新增】：拖拽状态机
    private ModuleDefinition _selectedModuleDef; 
    private MachineModuleData _previewModuleData; // 用于底层校验的临时数据替身
    private Vector2Int _currentHoverPos = new Vector2Int(-1, -1);

    [Header("Tab 标签页系统 (Phase 5)")]
    [Tooltip("拖入右侧所有的黄色标签按钮 (挂载了 UITabButton 脚本)")]
    public UITabButton[] TabButtons; 
    [Tooltip("拖入与标签对应的页面节点 (第0个是内部空间，第1个是统计页等)")]
    public GameObject[] TabPages; 


    [Header("侧边栏与拖拽系统 (Phase 5)")]
    public ModuleCatalog CurrentCatalog;      // 填入我们刚写的商品目录
    public Transform SidebarContent;          // 左侧 ScrollView 里的 Content 节点
    public GameObject UIModuleEntryPrefab;    // 左侧列表项的预制体

    [Tooltip("跟随鼠标的虚影节点")]
    public RectTransform DragGhost;           // 虚影的 RectTransform
    public Image DragGhostImage;              // 虚影的 Image


    private int _currentTabIndex = 0; // 当前停留在哪一页

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
    public void OpenPanel(MachineShellData shell)
    {
        _currentShell = shell;
        PanelRoot.SetActive(true);
        // 暂停游戏大世界时间 (按需)
        SimulationController.Instance.CurrentSpeed = TimeSpeed.Paused;

        // 重置 Tab 为初始状态
        SwitchTab(0);
        GenerateGrid();

        // 【新增】：每次打开面板时，刷新左侧侧边栏
        GenerateSidebar();

        // 打开面板时，把机器里已经存在的模块渲染出来
        foreach (var module in shell.Modules)
        {
            SpawnUIModuleVisual(module);
        }
    }

    // 关闭面板
    public void ClosePanel()
    {
        PanelRoot.SetActive(false);
        _currentShell = null;

        // 【新增】：关闭面板时，清空手里抓着的模块
        _selectedModuleDef = null;
        _previewModuleData = null;
        SimulationController.Instance.CurrentSpeed = TimeSpeed.Normal; // 恢复时间
    }

    // ==========================================
    // 【新增】：执行 Tab 切换逻辑
    // ==========================================
    public void SwitchTab(int targetIndex)
    {
        _currentTabIndex = targetIndex;

        // 1. 刷新所有 Tab 按钮的视觉长度
        for (int i = 0; i < TabButtons.Length; i++)
        {
            if (TabButtons[i] != null)
            {
                TabButtons[i].SetSelectedStatus(i == targetIndex);
            }
        }

        // 2. 刷新所有页面的显隐状态
        for (int i = 0; i < TabPages.Length; i++)
        {
            if (TabPages[i] != null)
            {
                TabPages[i].SetActive(i == targetIndex);
            }
        }
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
        bool useFallback = profile.VisualConfigs == null || profile.VisualConfigs.Count == 0;

        // 2. 读取配置并生成格子
        for (int x = 0; x < profile.LogicWidth; x++)
        {
            for (int y = 0; y < profile.LogicHeight; y++)
            {
                Vector2Int logicPos = new Vector2Int(x, y);
                CellVisualConfig visualConfig = new CellVisualConfig();
                
                if (useFallback)
                {
                    visualConfig.LogicalPos = logicPos;
                    visualConfig.VisualOffset = new Vector2(x * BaseCellSize, y * BaseCellSize);
                }
                else
                {
                    bool found = false;
                    foreach (var cfg in profile.VisualConfigs)
                    {
                        if (cfg.LogicalPos == logicPos)
                        {
                            visualConfig = cfg;
                            found = true;
                            break;
                        }
                    }
                    if (!found) continue; 
                }

                GameObject cellObj = Instantiate(UICellPrefab, GridContainer);
                RectTransform rt = cellObj.GetComponent<RectTransform>();
                rt.anchoredPosition = visualConfig.VisualOffset;
                rt.sizeDelta = new Vector2(BaseCellSize, BaseCellSize);

                UICell cellScript = cellObj.GetComponent<UICell>();
                if (cellScript != null)
                {
                    bool isDead = profile.DeadCells.Contains(logicPos);
                    cellScript.Init(logicPos, visualConfig, isDead);
                    
                    // 【修复 2】：这里一定要将生成的 UI 格子注册进字典！否则 RefreshPreview 找不到格子！
                    _cellDict[logicPos] = cellScript; 
                }

                _spawnedCells.Add(cellObj);

                minX = Mathf.Min(minX, visualConfig.VisualOffset.x);
                minY = Mathf.Min(minY, visualConfig.VisualOffset.y);
                maxX = Mathf.Max(maxX, visualConfig.VisualOffset.x + BaseCellSize);
                maxY = Mathf.Max(maxY, visualConfig.VisualOffset.y + BaseCellSize);
            }
        }

        // 3. 缩放引擎
        if (_spawnedCells.Count > 0)
        {
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
    }

    // ================== [新增生成侧边栏逻辑] ==================
    private void GenerateSidebar()
    {
        // 清理旧的列表项
        foreach (Transform child in SidebarContent) Destroy(child.gameObject);

        if (CurrentCatalog == null) return;

        // 根据目录生成列表项
        foreach (var modDef in CurrentCatalog.AvailableModules)
        {
            GameObject entryObj = Instantiate(UIModuleEntryPrefab, SidebarContent);
            UIModuleEntry entryScript = entryObj.GetComponent<UIModuleEntry>();
            if (entryScript != null)
            {
                entryScript.Init(modDef);
            }
        }
    }

    // ================== [新增拾取逻辑] ==================
    // 供 UIModuleEntry 点击时调用
    public void PickUpModule(ModuleDefinition targetDef)
    {
        _selectedModuleDef = targetDef;
        _previewModuleData = targetDef.CreateRuntimeInstance();
        
        // 激活鼠标跟随的虚影
        DragGhost.gameObject.SetActive(true);
        DragGhostImage.sprite = targetDef.Icon;
        // 恢复初始旋转角度
        DragGhost.localRotation = Quaternion.identity; 

        RefreshPreview();
    }


    // ================== [新增清空双手辅助方法] ==================
    private void ClearHands()
    {
        _selectedModuleDef = null;
        _previewModuleData = null;
        DragGhost.gameObject.SetActive(false);
        RefreshPreview();
    }

    // ==========================================
    // 交互输入总控 (Update)
    // ==========================================
    private void Update()
    {
        if (_currentShell == null) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ClosePanel();
            return;
        }

        // 状态 A：手里【拿着】模块
        if (_selectedModuleDef != null)
        {
            // 【新增】：让虚影时刻跟随鼠标移动
            // 适用 Screen Space - Overlay 的 Canvas
            DragGhost.position = Input.mousePosition;

            // 按 R 旋转
            if (Input.GetKeyDown(KeyCode.R))
            {
                _previewModuleData.Rotation = (ModuleRotation)(((int)_previewModuleData.Rotation + 1) % 4);
                // 虚影也跟着转
                DragGhost.localRotation *= Quaternion.Euler(0, 0, -90f); 
                RefreshPreview();
            }

            // 右键取消拾取
            if (Input.GetMouseButtonDown(1))
            {
                ClearHands(); // 【修改】：封装成一个小方法
                return;
            }

            // 左键放置 (前提是鼠标正在网格里，且有合法坐标)
            if (Input.GetMouseButtonDown(0) && _currentHoverPos.x != -1)
            {
                if (MachineManager.Instance.CanPlaceModule(_currentShell, _previewModuleData))
                {
                    MachineModuleData finalModule = _selectedModuleDef.CreateRuntimeInstance();
                    finalModule.LocalBottomLeft = _currentHoverPos;
                    finalModule.Rotation = _previewModuleData.Rotation;

                    MachineManager.Instance.PlaceModule(_currentShell, finalModule);
                    SpawnUIModuleVisual(finalModule);
                    InteractionController.Instance.RefreshPortOverlayVisuals(_currentShell);

                    // 放完后你可以选择清空双手，或者像 Factorio 一样保持拿取状态继续连放
                    // 如果你想连放，就把下面这行注释掉。目前我们设定为放一次就清空双手：
                    ClearHands(); 
                }
            }
        }
        // ===================================================
        // 状态 B：手里【空着】时的拆除逻辑 (Phase 4 核心)
        // ===================================================
        else if (_selectedModuleDef == null && _currentHoverPos.x != -1)
        {
            if (Input.GetMouseButtonDown(1)) // 右键拆卸
            {
                // 将 UI 的局部坐标转换为大世界的真实网格坐标
                Vector2Int worldPos = new Vector2Int(_currentShell.Bounds.xMin + _currentHoverPos.x, _currentShell.Bounds.yMin + _currentHoverPos.y);
                GridCell cell = GridManager.Instance.GetGridCell(worldPos);

                // 探查底层的网格：这个格子上有没有模块？模块属于现在的机箱吗？
                if (cell != null && cell.OccupyingModule != null && cell.OccupyingModule.ParentShell == _currentShell)
                {
                    MachineModuleData targetModule = cell.OccupyingModule;

                    // 1. Data 层拆除 (内部会清理大世界占位并重算 Buff)
                    MachineManager.Instance.RemoveModule(_currentShell, targetModule);

                    // 2. View 层 UI 清理
                    if (_moduleUIDict.TryGetValue(targetModule, out GameObject uiObj))
                    {
                        Destroy(uiObj);
                        _moduleUIDict.Remove(targetModule);
                    }

                    // 3. View 层大世界同步清理！把地上的 I/O 匣子抹掉
                    InteractionController.Instance.RefreshPortOverlayVisuals(_currentShell);

                    Debug.Log($"[GUI] 成功拆除了模块: {targetModule.Definition.DisplayName}");
                }
            }
        }
    }

    // ==========================================
    // 【新增】：悬停状态接收器 (供 UICell 呼叫)
    // ==========================================
    public void OnCellHoverEnter(Vector2Int logicPos)
    {
        _currentHoverPos = logicPos;
        RefreshPreview();
    }

    public void OnCellHoverExit(Vector2Int logicPos)
    {
        if (_currentHoverPos == logicPos)
        {
            _currentHoverPos = new Vector2Int(-1, -1);
            RefreshPreview();
        }
    }

    // ==========================================
    // 【新增】：渲染红绿灯悬浮预览
    // ==========================================
    private void RefreshPreview()
    {
        // 1. 刷白：清除字典中所有格子的颜色残留
        foreach (var kvp in _cellDict) kvp.Value.ClearHighlight();

        if (_selectedModuleDef == null || _currentShell == null || _currentHoverPos.x == -1) return;

        // 2. 将临时替身的锚点对齐鼠标
        _previewModuleData.LocalBottomLeft = _currentHoverPos;

        // 3. 计算在当前旋转下，模块占据的局部坐标集
        List<Vector2Int> occupied = _previewModuleData.GetOccupiedLocalCells();
        
        // 4. 呼叫 Data 层进行物理碰撞仲裁
        bool canPlace = MachineManager.Instance.CanPlaceModule(_currentShell, _previewModuleData);
        Color tint = canPlace ? new Color(0, 1, 0, 0.5f) : new Color(1, 0, 0, 0.5f); // 绿灯或红灯

        // 5. 染色对应的 UI 格子
        foreach (Vector2Int pos in occupied)
        {
            if (_cellDict.TryGetValue(pos, out UICell cell))
            {
                cell.SetHighlight(tint);
            }
        }
    }

    // ==========================================
    // 渲染：生成实体模块贴图包围盒 (带旋转修复)
    // ==========================================
    private void SpawnUIModuleVisual(MachineModuleData module)
    {
        List<Vector2Int> occupied = module.GetOccupiedLocalCells();
        if (occupied.Count == 0 || module.Definition == null) return;

        // 【修复 3-A】：计算模块在未旋转状态下的“基准尺寸”
        int minBaseX = 0, maxBaseX = 0, minBaseY = 0, maxBaseY = 0;
        foreach (var pos in module.Definition.Shape.BaseCells)
        {
            minBaseX = Mathf.Min(minBaseX, pos.x); maxBaseX = Mathf.Max(maxBaseX, pos.x);
            minBaseY = Mathf.Min(minBaseY, pos.y); maxBaseY = Mathf.Max(maxBaseY, pos.y);
        }
        float baseWidth = (maxBaseX - minBaseX + 1) * BaseCellSize;
        float baseHeight = (maxBaseY - minBaseY + 1) * BaseCellSize;

        // 计算当前在 UI 上的实际几何中心
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (Vector2Int pos in occupied)
        {
            if (_cellDict.TryGetValue(pos, out UICell cell))
            {
                Vector2 center = cell.GetComponent<RectTransform>().anchoredPosition;
                minX = Mathf.Min(minX, center.x);
                minY = Mathf.Min(minY, center.y);
                maxX = Mathf.Max(maxX, center.x);
                maxY = Mathf.Max(maxY, center.y);
            }
        }

        GameObject modObj = new GameObject("UIModule_" + module.Definition.ModuleID);
        modObj.transform.SetParent(GridContainer, false);
        modObj.transform.SetAsLastSibling(); 

        Image img = modObj.AddComponent<Image>();
        img.sprite = module.Definition.Icon; 
        img.raycastTarget = false; 

        RectTransform rt = modObj.GetComponent<RectTransform>();
        
        // 【修复 3-B】：尺寸设为基准尺寸，锚点设为几何中心
        rt.sizeDelta = new Vector2(baseWidth, baseHeight);
        rt.anchoredPosition = new Vector2((minX + maxX) / 2f, (minY + maxY) / 2f);

        // 【修复 3-C】：赋予 Z 轴旋转！
        float angle = 0f;
        switch (module.Rotation)
        {
            case ModuleRotation.R0: angle = 0f; break;
            case ModuleRotation.R90: angle = -90f; break;
            case ModuleRotation.R180: angle = 180f; break;
            case ModuleRotation.R270: angle = 90f; break;
        }
        rt.localRotation = Quaternion.Euler(0, 0, angle);

        _moduleUIDict.Add(module, modObj);
    }






}