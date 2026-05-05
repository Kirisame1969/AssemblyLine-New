using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using TMPro; // 引入 TextMeshPro 命名空间

public class TechTreeGridUI : MonoBehaviour, IPointerClickHandler
{
    [Header("UI 容器引用")]
    public RectTransform NodesContainer;
    public RectTransform LinesContainer;
    public UIZoomPanController PanController;

    [Header("详情面板引用 (Right Panel)")]
    public Image InfoIcon;
    public TextMeshProUGUI InfoNameText;
    public TextMeshProUGUI InfoDescText;

    [Header("升级面板引用 (Bottom Panel)")]
    public TextMeshProUGUI UpgradeCostText;
    public Button UnlockButton;
    public TextMeshProUGUI UnlockButtonText; // 按钮上的文字，用于显示“已解锁”或“点击解锁”

    [Header("预制体")]
    public GameObject NodePrefab;
    [Tooltip("必须是一张可平铺(Tiled)的虚线Image预制体，Pivot为 0, 0.5")]
    public GameObject DashedLinePrefab;

    [Header("网格参数")]
    public float HexSize = 55f;

    // 视觉映射字典
    private Dictionary<string, RectTransform> _nodeVisuals = new Dictionary<string, RectTransform>();
    private Dictionary<string, CanvasGroup> _nodeCanvasGroups = new Dictionary<string, CanvasGroup>();

    private class DashedLineData
    {
        public string StartId;
        public string EndId;
        public GameObject LineObj;
    }
    private List<DashedLineData> _dashedLines = new List<DashedLineData>();

    private string _currentFocusedNode = null;
    private string _currentSelectedNode = null; // 当前单击选中的节点

    private void Start()
    {
        // 初始状态下清空右侧面板
        ClearInfoPanel();
        GenerateGrid();
    }

    private void GenerateGrid()
    {
        if (TechManager.Instance == null) return;

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        var allNodes = TechManager.Instance.AllTechNodes;

        for (int i = 0; i < allNodes.Count; i++)
        {
            var data = allNodes[i];
            if (data == null) continue;

            float xPos = HexSize * Mathf.Sqrt(3f) * (data.AxialCoords.x + data.AxialCoords.y / 2f);
            float yPos = HexSize * (3f / 2f) * data.AxialCoords.y;
            Vector2 anchoredPos = new Vector2(xPos, yPos);

            minX = Mathf.Min(minX, xPos); maxX = Mathf.Max(maxX, xPos);
            minY = Mathf.Min(minY, yPos); maxY = Mathf.Max(maxY, yPos);

            GameObject nodeObj = Instantiate(NodePrefab, NodesContainer);
            RectTransform rect = nodeObj.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;

            // ==========================================
            // 新增：提取并绑定特有标签图片 (Icon)
            // ==========================================
            Transform iconTrans = nodeObj.transform.Find("Icon");
            if (iconTrans != null && data.Icon != null)
            {
                Image iconImg = iconTrans.GetComponent<Image>();
                if (iconImg != null) iconImg.sprite = data.Icon;
            }

            _nodeVisuals[data.NodeID] = rect;
            _nodeCanvasGroups[data.NodeID] = nodeObj.GetComponent<CanvasGroup>() ?? nodeObj.AddComponent<CanvasGroup>();

            TechNodeClickHandler clickHandler = nodeObj.AddComponent<TechNodeClickHandler>();
            clickHandler.Initialize(data.NodeID, OnNodeSingleClicked, OnNodeDoubleClicked);
        }

        float padding = 1000f;
        if (PanController != null && minX != float.MaxValue)
        {
            PanController.SetBounds(new Vector2(-maxX - padding, -maxY - padding), new Vector2(-minX + padding, -minY + padding));
        }
        // 2. 第二次遍历：生成依赖连线
        
        for (int i = 0; i < allNodes.Count; i++)
        {
            var data = allNodes[i];
            if (data == null || data.Prerequisites == null) continue;

            for (int j = 0; j < data.Prerequisites.Count; j++)
            {
                var pre = data.Prerequisites[j];
                int distance = GetHexDistance(data.AxialCoords, pre.AxialCoords);
                if (distance <= 1) continue;

                if (_nodeVisuals.TryGetValue(data.NodeID, out var endRt) &&
                    _nodeVisuals.TryGetValue(pre.NodeID, out var startRt))
                {
                    GameObject lineObj = DrawDashedLine(startRt.anchoredPosition, endRt.anchoredPosition);
                    lineObj.SetActive(false);

                    _dashedLines.Add(new DashedLineData
                    {
                        StartId = pre.NodeID,
                        EndId = data.NodeID,
                        LineObj = lineObj
                    });
                }
            }
        }
    }

    private int GetHexDistance(Vector2Int a, Vector2Int b)
    {
        return (Mathf.Abs(a.x - b.x) +
                Mathf.Abs(a.x + a.y - b.x - b.y) +
                Mathf.Abs(a.y - b.y)) / 2;
    }

    private GameObject DrawDashedLine(Vector2 startPos, Vector2 endPos)
    {
        GameObject lineObj = Instantiate(DashedLinePrefab, LinesContainer);
        RectTransform rect = lineObj.GetComponent<RectTransform>();

        Vector2 dir = endPos - startPos;
        float distance = dir.magnitude;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        rect.anchoredPosition = startPos;
        rect.sizeDelta = new Vector2(distance, 4f);
        rect.localRotation = Quaternion.Euler(0, 0, angle);

        return lineObj;
    }

    // ==========================================
    // 新增：面板数据绑定与刷新逻辑
    // ==========================================
    private void OnNodeSingleClicked(string nodeId)
    {
        _currentSelectedNode = nodeId;
        RefreshInfoPanel();
    }

    private void RefreshInfoPanel()
    {
        if (string.IsNullOrEmpty(_currentSelectedNode)) return;

        TechNodeDefinition nodeData = TechManager.Instance.GetNode(_currentSelectedNode);
        if (nodeData == null) return;

        // 1. 刷新右侧详情面板
        if (InfoNameText != null) InfoNameText.text = nodeData.DisplayName;
        if (InfoDescText != null) InfoDescText.text = nodeData.Description;
        if (InfoIcon != null)
        {
            InfoIcon.sprite = nodeData.Icon;
            InfoIcon.enabled = nodeData.Icon != null; // 如果没配图标就隐藏 Image
        }

        // 2. 刷新下方升级面板与状态机
        if (UpgradeCostText != null) UpgradeCostText.text = $"需求资金: ${nodeData.UnlockCost}";

        if (UnlockButton != null)
        {
            // 清理旧的监听器，防止点击一次触发多次扣钱
            UnlockButton.onClick.RemoveAllListeners();

            bool isUnlocked = TechManager.Instance.TechData.IsUnlocked(_currentSelectedNode);
            bool isAvailable = TechManager.Instance.IsTechAvailable(_currentSelectedNode);

            if (isUnlocked)
            {
                // 已解锁状态
                UnlockButton.interactable = false;
                if (UnlockButtonText != null) UnlockButtonText.text = "已研发";
            }
            else if (!isAvailable)
            {
                // 前置条件未满足
                UnlockButton.interactable = false;
                if (UnlockButtonText != null) UnlockButtonText.text = "前置未解锁";
            }
            else
            {
                // 可解锁状态（这里暂未判断玩家钱够不够，可以在 TechManager 扣钱时拦截）
                UnlockButton.interactable = true;
                if (UnlockButtonText != null) UnlockButtonText.text = "点击研发";

                // 绑定扣钱与解锁逻辑
                UnlockButton.onClick.AddListener(() =>
                {
                    if (TechManager.Instance.TryUnlockTech(_currentSelectedNode))
                    {
                        // 扣钱成功，刷新面板状态
                        RefreshInfoPanel();
                    }
                    else
                    {
                        Debug.LogWarning("[UI] 资金不足，无法解锁！");
                        // 此处可接漂浮字提示玩家资金不足
                    }
                });
            }
        }
    }

    private void ClearInfoPanel()
    {
        if (InfoNameText != null) InfoNameText.text = "请选择节点";
        if (InfoDescText != null) InfoDescText.text = "点击左侧的科技节点以查看详细信息与升级需求。";
        if (InfoIcon != null) InfoIcon.enabled = false;
        if (UpgradeCostText != null) UpgradeCostText.text = "";
        if (UnlockButton != null) UnlockButton.interactable = false;
        if (UnlockButtonText != null) UnlockButtonText.text = "---";
    }

    // ==========================================
    // 聚焦与导航 (保持不变)
    // ==========================================
    private void OnNodeDoubleClicked(string nodeId)
    {
        // 1. 防御性拦截
        if (string.IsNullOrEmpty(nodeId)) return;
        if (TechManager.Instance == null) return;

        if (_currentFocusedNode == nodeId)
        {
            ClearFocus();
            return;
        }

        _currentFocusedNode = nodeId;
        HashSet<string> focusedSet = TechManager.Instance.GetFocusedNodes(nodeId, 2);

        // 即使出错返回了null，也强制初始化防止下游崩溃
        if (focusedSet == null) focusedSet = new HashSet<string>();

        // 2. 节点明暗切分 (强制对象非空校验)
        foreach (var kvp in _nodeCanvasGroups)
        {
            if (kvp.Value != null)
            {
                kvp.Value.alpha = focusedSet.Contains(kvp.Key) ? 1f : 0.2f;
            }
        }

        // 3. 跨距虚线动态显隐 (强制对象非空校验)
        foreach (var line in _dashedLines)
        {
            if (line != null && line.LineObj != null)
            {
                line.LineObj.SetActive(focusedSet.Contains(line.StartId) && focusedSet.Contains(line.EndId));
            }
        }
    }

    private void ClearFocus()
    {
        _currentFocusedNode = null;

        // 恢复节点高亮
        foreach (var kvp in _nodeCanvasGroups)
        {
            if (kvp.Value != null) kvp.Value.alpha = 1f;
        }

        // 退出时隐藏所有虚线
        foreach (var line in _dashedLines)
        {
            if (line != null && line.LineObj != null) line.LineObj.SetActive(false);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.clickCount == 2) ClearFocus();
    }

    

    public void CloseTechTreeUI() { gameObject.SetActive(false); }

    public void NavigateToNode(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId)) return;

        if (_nodeVisuals.TryGetValue(nodeId, out RectTransform nodeRect))
        {
            Vector2 targetCanvasPos = -nodeRect.anchoredPosition * PanController.TargetContainer.localScale.x;
            PanController.SmoothPanTo(targetCanvasPos);
            OnNodeSingleClicked(nodeId);
        }
    }
}