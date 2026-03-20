using UnityEngine;

// 这是一个纯数据类，不挂载到任何 GameObject 上
public class GridCell
{
    // 网格的逻辑坐标 (例如: x=5, y=10)
    public Vector2Int GridPosition { get; private set; }

    // 这个格子上铺设的传送带数据
    public BeltData Belt { get; set; }

    // 这个格子当前承载的物品数据（为 null 代表格子上没东西）
    public ItemData Item { get; set; }

    // 记录玩家是否在这个格子的四个方向使用了“拆分工具” (上0, 右1, 下2, 左3)
    public bool[] CutEdges { get; private set; }

    // 圈地标记。如果不为 null，说明该格子属于某个机箱的内部
    public MachineShellData ShellRegion { get; set; }

    // 实体标记。如果不为 null，说明该格子上具体放置了某个模块
    public MachineModuleData OccupyingModule { get; set; }

    // 构造函数：初始化格子时赋予坐标
    public GridCell(Vector2Int pos)
    {
        GridPosition = pos;
        CutEdges = new bool[4]; 
    }
}