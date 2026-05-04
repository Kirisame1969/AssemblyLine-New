using System;
using System.Collections.Generic;
using UnityEngine;

public class TechManager : MonoBehaviour
{
    public static TechManager Instance { get; private set; }

    [Header("科技树数据库")]
    [Tooltip("将所有创建好的 TechNodeDefinition 拖入此处")]
    public List<TechNodeDefinition> AllTechNodes;

    public PlayerTechData TechData = new PlayerTechData();

    // ==========================================
    // 事件总线（表现层严禁直接轮询，只能被动监听事件渲染）
    // ==========================================
    public event Action<string> OnTechUnlocked;

    // 内存拓扑图：用于 O(1) 查询与快速遍历
    private Dictionary<string, TechNodeDefinition> _nodeDict = new Dictionary<string, TechNodeDefinition>();
    private Dictionary<string, List<string>> _successorsMap = new Dictionary<string, List<string>>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializeGraph();
        TechData.Initialize();
    }

    private void InitializeGraph()
    {
        _nodeDict.Clear();
        _successorsMap.Clear();

        // 扁平化：第一次遍历生成字典，初始化空后置列表
        for (int i = 0; i < AllTechNodes.Count; i++)
        {
            var node = AllTechNodes[i];
            if (node == null || string.IsNullOrEmpty(node.NodeID)) continue;

            _nodeDict[node.NodeID] = node;
            _successorsMap[node.NodeID] = new List<string>();
        }

        // 扁平化：第二次遍历，反向注入构建后置节点 (Successors) 图谱
        for (int i = 0; i < AllTechNodes.Count; i++)
        {
            var node = AllTechNodes[i];
            if (node == null || string.IsNullOrEmpty(node.NodeID)) continue;

            if (node.Prerequisites != null)
            {
                for (int j = 0; j < node.Prerequisites.Count; j++)
                {
                    var pre = node.Prerequisites[j];
                    if (pre != null && _successorsMap.ContainsKey(pre.NodeID))
                    {
                        _successorsMap[pre.NodeID].Add(node.NodeID);
                    }
                }
            }
        }
    }

    public TechNodeDefinition GetNode(string id)
    {
        _nodeDict.TryGetValue(id, out var node);
        return node;
    }

    // 纯数据逻辑判断：节点是否呈可解锁状态
    public bool IsTechAvailable(string id)
    {
        if (TechData.IsUnlocked(id)) return false;
        if (!_nodeDict.TryGetValue(id, out var node)) return false;

        // 必须所有前置条件都被点亮
        for (int i = 0; i < node.Prerequisites.Count; i++)
        {
            if (!TechData.IsUnlocked(node.Prerequisites[i].NodeID)) return false;
        }
        return true;
    }

    // ==========================================
    // 经济系统对接与解锁执行
    // ==========================================
    public bool TryUnlockTech(string id)
    {
        if (!IsTechAvailable(id)) return false;
        if (!_nodeDict.TryGetValue(id, out var node)) return false;

        // 调用刚才你提供的 EconomyManager 进行资金划扣
        if (EconomyManager.Instance.ConsumeFunds(node.UnlockCost))
        {
            TechData.Unlock(id);
            OnTechUnlocked?.Invoke(id);
            Debug.Log($"[TechManager] 成功点亮科技: {node.DisplayName}，消耗资金: ${node.UnlockCost}");
            return true;
        }

        Debug.LogWarning($"[TechManager] 资金不足！需要 ${node.UnlockCost} 来解锁 {node.DisplayName}");
        return false;
    }

    // ==========================================
    // 双击聚焦算法：BFS 宽度优先遍历
    // 寻找向上与向下各延伸 N 级的节点集合
    // ==========================================
    // ==========================================
    // 双击聚焦算法：BFS 宽度优先遍历 (已加入极限防御)
    // ==========================================
    public HashSet<string> GetFocusedNodes(string centerId, int depth = 2)
    {
        HashSet<string> result = new HashSet<string>();
        if (string.IsNullOrEmpty(centerId) || !_nodeDict.ContainsKey(centerId)) return result;

        // 向前遍历（祖先节点）
        Queue<(string id, int dist)> queue = new Queue<(string, int)>();
        queue.Enqueue((centerId, 0));

        while (queue.Count > 0)
        {
            var curr = queue.Dequeue();
            result.Add(curr.id);

            if (curr.dist < depth && _nodeDict.TryGetValue(curr.id, out var node))
            {
                if (node.Prerequisites != null)
                {
                    for (int i = 0; i < node.Prerequisites.Count; i++)
                    {
                        var pre = node.Prerequisites[i];
                        // 【核心防御】：拦截 Inspector 中未拖入文件的空洞 (None)
                        if (pre != null && !string.IsNullOrEmpty(pre.NodeID))
                        {
                            if (!result.Contains(pre.NodeID)) queue.Enqueue((pre.NodeID, curr.dist + 1));
                        }
                    }
                }
            }
        }

        // 向后遍历（子孙节点）
        queue.Clear();
        queue.Enqueue((centerId, 0));

        while (queue.Count > 0)
        {
            var curr = queue.Dequeue();
            result.Add(curr.id);

            if (curr.dist < depth && _successorsMap.TryGetValue(curr.id, out var successors))
            {
                if (successors != null)
                {
                    for (int i = 0; i < successors.Count; i++)
                    {
                        string succId = successors[i];
                        // 【核心防御】：防止子节点映射出异常
                        if (!string.IsNullOrEmpty(succId) && !result.Contains(succId))
                        {
                            queue.Enqueue((succId, curr.dist + 1));
                        }
                    }
                }
            }
        }

        return result;
    }
}