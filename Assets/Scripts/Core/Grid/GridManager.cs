using UnityEngine;

public class GridManager : MonoBehaviour
{
    // 单例模式，方便全局访问网格数据
    public static GridManager Instance { get; private set; }

    [Header("Grid Settings")]
    public int Width = 32;
    public int Height = 32;
    public float CellSize = 1.0f; // 每个格子在Unity世界中占多大（1个单位）
    public Vector2 OriginPosition = Vector2.zero; // 网格的左下角原点位置

    // 核心：存储所有格子数据的二维数组
    private GridCell[,] _gridArray;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializeGrid();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 初始化网格数据
    private void InitializeGrid()
    {
        _gridArray = new GridCell[Width, Height];
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                _gridArray[x, y] = new GridCell(new Vector2Int(x, y));
            }
        }
        Debug.Log($"网格初始化完成: {Width}x{Height}");
    }

    // --- 工具方法：坐标转换 ---

    // 1. 将 Unity 世界坐标 (鼠标点击位置) 转换为 网格坐标 (0~31, 0~31)
    public Vector2Int WorldToGridPosition(Vector2 worldPosition)
    {
        int x = Mathf.FloorToInt((worldPosition.x - OriginPosition.x) / CellSize);
        int y = Mathf.FloorToInt((worldPosition.y - OriginPosition.y) / CellSize);
        return new Vector2Int(x, y);
    }

    // 2. 将 网格坐标 转换为 Unity 世界坐标 (用于渲染建筑或物品的中心点)
    public Vector2 GridToWorldPosition(Vector2Int gridPosition)
    {
        float x = (gridPosition.x * CellSize) + OriginPosition.x + (CellSize * 0.5f);
        float y = (gridPosition.y * CellSize) + OriginPosition.y + (CellSize * 0.5f);
        return new Vector2(x, y);
    }

    // 3. 安全地获取某个坐标的格子数据（如果越界则返回 null）
    public GridCell GetGridCell(Vector2Int gridPosition)
    {
        if (gridPosition.x >= 0 && gridPosition.y >= 0 && gridPosition.x < Width && gridPosition.y < Height)
        {
            return _gridArray[gridPosition.x, gridPosition.y];
        }
        return null;
    }

    // 4. 获取从起点到终点（相邻格子）的方向
    public Direction GetDirectionToNeighbor(Vector2Int from, Vector2Int to)
    {
        if (to.y > from.y) return Direction.Up;
        if (to.y < from.y) return Direction.Down;
        if (to.x > from.x) return Direction.Right;
        return Direction.Left;
    }

    // 5. 切换（切断/缝合）两个相邻格子之间的连接状态
    public void ToggleCutEdge(Vector2Int posA, Vector2Int posB)
    {
        GridCell cellA = GetGridCell(posA);
        GridCell cellB = GetGridCell(posB);
        
        if (cellA == null || cellB == null) return;

        Direction dirAToB = GetDirectionToNeighbor(posA, posB);
        Direction dirBToA = GetDirectionToNeighbor(posB, posA);

        // 翻转布尔值：如果原来连着就切断，原来断了就缝合
        bool isCurrentlyCut = cellA.CutEdges[(int)dirAToB];
        cellA.CutEdges[(int)dirAToB] = !isCurrentlyCut;
        cellB.CutEdges[(int)dirBToA] = !isCurrentlyCut;

        Debug.Log($"修改了 [{posA.x},{posA.y}] 和 [{posB.x},{posB.y}] 之间的连接状态。当前切断状态: {!isCurrentlyCut}");
    }
}