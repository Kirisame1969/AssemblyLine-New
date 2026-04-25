using System.Collections.Generic;
using UnityEngine;

public class StripManager : MonoBehaviour
{
    public static StripManager Instance { get; private set; }

    // 记录当前世界上所有存活的条带
    public List<StripData> ActiveStrips = new List<StripData>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 当任何传送带发生增、删、改时，调用此方法
    public void OnBeltModified(Vector2Int pos)
    {
        List<Vector2Int> affectedPositions = GetAffectedPositions(pos);

        HashSet<StripData> stripsToDestroy = new HashSet<StripData>();
        foreach (var p in affectedPositions)
        {
            GridCell cell = GridManager.Instance.GetGridCell(p);
            // 这里找的是：修改点及其周围，还在其他条带里的传送带
            if (cell != null && cell.Belt != null && cell.Belt.ParentStrip != null)
            {
                stripsToDestroy.Add(cell.Belt.ParentStrip);
            }
        }

        HashSet<Vector2Int> orphanedBelts = new HashSet<Vector2Int>();
        
        // 【修复点 1】：解散旧条带时，过滤掉已经被删除的传送带
        foreach (var strip in stripsToDestroy)
        {
            foreach (var cellPos in strip.Cells)
            {
                GridCell c = GridManager.Instance.GetGridCell(cellPos);
                // 只有当格子上确实还有传送带时，才加进孤儿名单
                if (c != null && c.Belt != null)
                {
                    orphanedBelts.Add(cellPos);
                    c.Belt.ParentStrip = null; // 解除旧关系
                }
            }
            ActiveStrips.Remove(strip);
        }

        // 把当前修改的这个中心点也加进孤儿列表（如果是放置操作，它会有Belt；如果是删除，它就是null进不去）
        GridCell centerCell = GridManager.Instance.GetGridCell(pos);
        if (centerCell != null && centerCell.Belt != null)
        {
            orphanedBelts.Add(pos);
            centerCell.Belt.ParentStrip = null;
        }

        // 使用 BFS 为所有孤儿重新分配条带
        RebuildStrips(orphanedBelts);

        // BFS 重组完成后，通知表现层刷新所有方块的颜色
        if (InteractionController.Instance != null)
        {
            InteractionController.Instance.UpdateBeltColorVisuals();
        }
    }

    // --- 核心：BFS 重组算法 ---
    private void RebuildStrips(HashSet<Vector2Int> orphanedBelts)
    {
        while (orphanedBelts.Count > 0)
        {
            var enumerator = orphanedBelts.GetEnumerator();
            enumerator.MoveNext();
            Vector2Int startPos = enumerator.Current;

            StripData newStrip = new StripData();
            ActiveStrips.Add(newStrip);

            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(startPos);
            orphanedBelts.Remove(startPos);

            while (queue.Count > 0)
            {
                Vector2Int currentPos = queue.Dequeue();
                GridCell currentCell = GridManager.Instance.GetGridCell(currentPos);
                
                // 【修复点 2：防御性编程】万一拿到了空数据，直接跳过，防止报错崩溃
                if (currentCell == null || currentCell.Belt == null) continue;

                // 将当前格子正式编入新条带 (原本第86行报错的地方)
                currentCell.Belt.ParentStrip = newStrip;
                newStrip.Cells.Add(currentPos);

                List<Vector2Int> neighbors = GetAlongAxisPositions(currentPos, currentCell.Belt.Dir);
                foreach (var neighborPos in neighbors)
                {
                    if (orphanedBelts.Contains(neighborPos))
                    {
                        GridCell neighborCell = GridManager.Instance.GetGridCell(neighborPos);
                        
                        // 【新增】：检查这两个格子之间是否被玩家手动切断了！
                        Direction dirToNeighbor = GridManager.Instance.GetDirectionToNeighbor(currentPos, neighborPos);
                        bool isCut = currentCell.CutEdges[(int)dirToNeighbor];

                        // 判定是否能连通：格子有效 + 有传送带 + 方向完全相同 + 【没有被手动切断】
                        if (!isCut && 
                            neighborCell != null && 
                            neighborCell.Belt != null && 
                            neighborCell.Belt.Dir == currentCell.Belt.Dir)
                        {
                            queue.Enqueue(neighborPos);
                            orphanedBelts.Remove(neighborPos); 
                        }
                    }
                }
            }
        }
    }


    // 获取中心点及其十字方向的 4 个坐标
    private List<Vector2Int> GetAffectedPositions(Vector2Int center)
    {
        return new List<Vector2Int>
        {
            center,
            new Vector2Int(center.x, center.y + 1), // 上
            new Vector2Int(center.x, center.y - 1), // 下
            new Vector2Int(center.x - 1, center.y), // 左
            new Vector2Int(center.x + 1, center.y)  // 右
        };
    }

    // 获取传送带所在轴向（前进和后退方向）的两个相邻坐标
    private List<Vector2Int> GetAlongAxisPositions(Vector2Int pos, Direction dir)
    {
        List<Vector2Int> axisNeighbors = new List<Vector2Int>();
        
        if (dir == Direction.Up || dir == Direction.Down)
        {
            // 纵向传送带，只找上下邻居
            axisNeighbors.Add(new Vector2Int(pos.x, pos.y + 1));
            axisNeighbors.Add(new Vector2Int(pos.x, pos.y - 1));
        }
        else 
        {
            // 横向传送带，只找左右邻居
            axisNeighbors.Add(new Vector2Int(pos.x - 1, pos.y));
            axisNeighbors.Add(new Vector2Int(pos.x + 1, pos.y));
        }
        
        return axisNeighbors;
    }
}