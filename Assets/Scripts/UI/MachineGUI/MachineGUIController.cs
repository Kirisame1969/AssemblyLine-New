using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MachineGUIController : MonoBehaviour
{
    public static MachineGUIController Instance { get; private set; }

    [Header("UI 容器引用")]
    public GameObject PanelRoot; // 整个面板的根节点（用于控制显示/隐藏）
    public RectTransform GridContainer; // 承载所有格子和模块的 UI 父节点

    [Header("UI 渲染配置")]
    public float CellSize = 64f; // UI面板中每个格子的像素边长

    [Header("UI 预制体/贴图配置")]
    // 这里未来会拖入你做好的 UI 预制体或贴图
    public GameObject UICellPrefab;       // 普通空白格子背景
    public GameObject UIDeadCellPrefab;   // 死区格子背景 (比如黑色打叉)
    public GameObject UIModulePrefab;     // 模块生成的UI图标
    public GameObject UIPreviewPrefab;    // 悬浮时的红绿预览块

    // --- 内部数据缓存 ---
    private MachineShellData _currentShell;
    private MachineModuleData _previewModule;
    private List<GameObject> _spawnedUIVisuals = new List<GameObject>(); // 已生成的UI元素(用于刷新时清理)
    private List<GameObject> _previewUIVisuals = new List<GameObject>(); // 悬浮预览的UI元素

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 默认隐藏面板
        ClosePanel();
    }

    // ==========================================
    // 1. 面板开关与数据注入
    // ==========================================
    public void OpenPanel(MachineShellData shellData)
    {
        _currentShell = shellData;
        PanelRoot.SetActive(true);

        // 清空手里可能拿着的预览模块
        _previewModule = null;

        // 根据机箱尺寸动态调整 GridContainer 的大小
        GridContainer.sizeDelta = new Vector2(shellData.Bounds.width * CellSize, shellData.Bounds.height * CellSize);

        RefreshUI();
    }

    public void ClosePanel()
    {
        _currentShell = null;
        _previewModule = null;
        PanelRoot.SetActive(false);
        ClearPreviews();
    }

    // 供 UI 侧边栏按钮点击调用（例如点击了“输入匣”按钮）
    public void SelectModuleForPlacement(MachineModuleData module)
    {
        _previewModule = module;
    }

    // ==========================================
    // 2. 核心 Update：处理 UI 上的悬浮与点击
    // ==========================================
    private void Update()
    {
        if (_currentShell == null || !PanelRoot.activeInHierarchy) return;

        // 如果按右键，清空手里的图纸，或者关闭面板
        if (Input.GetMouseButtonDown(1))
        {
            if (_previewModule != null) _previewModule = null;
            else ClosePanel();
            ClearPreviews();
            return;
        }

        // 如果手里拿着模块，处理悬浮预览与放置
        if (_previewModule != null)
        {
            HandlePreviewAndPlacement();
            
            // 按 R 键在 UI 里旋转模块
            if (Input.GetKeyDown(KeyCode.R))
            {
                _previewModule.Rotation = (ModuleRotation)(((int)_previewModule.Rotation + 1) % 4);
            }
        }
        else
        {
            ClearPreviews();
        }
    }

    // ==========================================
    // 3. 坐标转换与 MVC 标准调用
    // ==========================================
    private void HandlePreviewAndPlacement()
    {
        // 获取鼠标在 GridContainer 内部的 UI 局部坐标
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            GridContainer, Input.mousePosition, null, out Vector2 localMousePos);

        // 将 UI 像素坐标转换为机箱内部的逻辑网格坐标 (0,0 代表左下角)
        int logicX = Mathf.FloorToInt(localMousePos.x / CellSize);
        int logicY = Mathf.FloorToInt(localMousePos.y / CellSize);
        Vector2Int hoveredLocalPos = new Vector2Int(logicX, logicY);

        _previewModule.LocalBottomLeft = hoveredLocalPos;

        // 【MVC 严苛遵守】：绝对不去自己查数据，而是询问 Core 层的主任：能不能放？
        bool canPlace = MachineManager.Instance.CanPlaceModule(_currentShell, _previewModule);

        // 渲染红绿灯虚影
        UpdatePreviewVisuals(canPlace);

        // 左键点击放置
        if (Input.GetMouseButtonDown(0))
        {
            if (canPlace)
            {
                // 【MVC 严苛遵守】：调用 Core 层写入数据
                MachineManager.Instance.PlaceModule(_currentShell, _previewModule);

                // 如果是 IO 匣子，回调大世界的 InteractionController 叠加外面那一层边缘贴图
                InteractionController.Instance.SpawnPortOverlayVisual(_currentShell, _previewModule);

                // 放置成功后，立刻重新读取 Model 数据并刷新 UI 画布
                RefreshUI();

                // 连续放置逻辑（重新 new 一个同类的模块放手里），这里先简单置空
                _previewModule = null; 
                ClearPreviews();
            }
            else
            {
                Debug.LogWarning("UI 面板：放置失败！违反了五重锁校验。");
            }
        }
    }

    // ==========================================
    // 4. 视图层渲染逻辑 (View)
    // ==========================================
    private void RefreshUI()
    {
        if (_currentShell == null) return;

        // 清理旧的 UI 元素
        foreach (var obj in _spawnedUIVisuals) Destroy(obj);
        _spawnedUIVisuals.Clear();

        // 1. 绘制底板（空白格与死区格）
        for (int x = 0; x < _currentShell.Bounds.width; x++)
        {
            for (int y = 0; y < _currentShell.Bounds.height; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                bool isDead = _currentShell.DeadCells.Contains(pos);
                
                GameObject prefab = isDead ? UIDeadCellPrefab : UICellPrefab;
                if (prefab != null)
                {
                    GameObject cellObj = Instantiate(prefab, GridContainer);
                    RectTransform rt = cellObj.GetComponent<RectTransform>();
                    
                    // 将逻辑坐标转换为 UI 锚点坐标 (左下角为起点)
                    rt.anchoredPosition = new Vector2(x * CellSize, y * CellSize);
                    rt.sizeDelta = new Vector2(CellSize, CellSize);
                    
                    _spawnedUIVisuals.Add(cellObj);
                }
            }
        }

        // 2. 绘制已放置的模块 (未来可以替换为你设计的精美 UI 图标)
        foreach (MachineModuleData module in _currentShell.Modules)
        {
            foreach (Vector2Int cellPos in module.GetOccupiedLocalCells())
            {
                if (UIModulePrefab != null)
                {
                    GameObject modObj = Instantiate(UIModulePrefab, GridContainer);
                    RectTransform rt = modObj.GetComponent<RectTransform>();
                    rt.anchoredPosition = new Vector2(cellPos.x * CellSize, cellPos.y * CellSize);
                    rt.sizeDelta = new Vector2(CellSize, CellSize);
                    
                    // TODO: 这里可以根据 module 的具体类型 (Core, InputPort) 赋予不同的 UI 图片
                    
                    _spawnedUIVisuals.Add(modObj);
                }
            }
        }

        // 3. 绘制隔断墙 (略，未来可以用细长的 Image 挡在格子中间)
    }

    private void UpdatePreviewVisuals(bool canPlace)
    {
        List<Vector2Int> occupied = _previewModule.GetOccupiedLocalCells();
        Color color = canPlace ? new Color(0, 1, 0, 0.5f) : new Color(1, 0, 0, 0.5f);

        if (_previewUIVisuals.Count != occupied.Count)
        {
            ClearPreviews();
            for (int i = 0; i < occupied.Count; i++)
            {
                if (UIPreviewPrefab != null)
                {
                    GameObject obj = Instantiate(UIPreviewPrefab, GridContainer);
                    _previewUIVisuals.Add(obj);
                }
            }
        }

        for (int i = 0; i < occupied.Count; i++)
        {
            if (i < _previewUIVisuals.Count)
            {
                RectTransform rt = _previewUIVisuals[i].GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(occupied[i].x * CellSize, occupied[i].y * CellSize);
                rt.sizeDelta = new Vector2(CellSize, CellSize);
                
                Image img = _previewUIVisuals[i].GetComponent<Image>();
                if (img != null) img.color = color;
            }
        }
    }

    private void ClearPreviews()
    {
        foreach (var obj in _previewUIVisuals) Destroy(obj);
        _previewUIVisuals.Clear();
    }
}