using System.Collections.Generic;
using UnityEngine;

public class MachineManager : MonoBehaviour
{
    public static MachineManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // ==========================================
    // 核心引擎：校验模块是否允许被放置在机箱的指定位置
    // ==========================================
    public bool CanPlaceModule(MachineShellData shell, MachineModuleData module)
    {
        // 获取模块在当前旋转状态下，需要占据的局部坐标
        List<Vector2Int> localCells = module.GetOccupiedLocalCells();

        // 【第一重锁 & 第二重锁 & 第三重锁】：遍历所有占用的格子
        foreach (Vector2Int localPos in localCells)
        {
            // 1. 越界锁：模块是否超出了机箱的物理尺寸？
            if (localPos.x < 0 || localPos.x >= shell.Bounds.width ||
                localPos.y < 0 || localPos.y >= shell.Bounds.height)
            {
                Debug.LogWarning($"[放置失败] 模块超出了机箱边界: {localPos}");
                return false;
            }

            // 2. 死区锁：该格子是否为机箱的坏死区/承重柱？
            if (shell.DeadCells.Contains(localPos))
            {
                Debug.LogWarning($"[放置失败] 触碰机箱绝对死区: {localPos}");
                return false;
            }

            // 3. 占用锁：该格子是否已经被其他模块占用了？
            // 将局部坐标转换为大世界网格坐标
            Vector2Int worldPos = new Vector2Int(shell.Bounds.xMin + localPos.x, shell.Bounds.yMin + localPos.y);
            GridCell cell = GridManager.Instance.GetGridCell(worldPos);
            
            if (cell == null || cell.OccupyingModule != null)
            {
                Debug.LogWarning($"[放置失败] 世界网格位置已被占用: {worldPos}");
                return false;
            }
        }
        // ==========================================
        // 【第四重锁】：便当盒隔断锁
        // ==========================================
        // 获取模块作为一个整体，内部必须连通的接缝
        List<InternalWall> requiredConnections = module.GetRequiredInternalConnections();
        foreach (InternalWall conn in requiredConnections)
        {
            // 检查机箱的隔板字典中，是否存在这堵墙
            if (shell.PartitionWalls.Contains(conn))
            {
                Debug.LogWarning($"[放置失败] 模块内部连通性被机箱隔断墙切断! 阻挡位置: {conn.CellA} <-> {conn.CellB}");
                return false;
            }
        }

        // ==========================================
        // 【第五重锁】：I/O 匣子的边缘与朝向锁
        // ==========================================
        if (module is InputPortData || module is OutputPortData)
        {
            Vector2Int pos = module.LocalBottomLeft; // I/O 匣子是 1x1，只看这一个坐标
            Direction facing = (module is InputPortData input) ? input.FacingDir : ((OutputPortData)module).FacingDir;

            int maxX = shell.Bounds.width - 1;
            int maxY = shell.Bounds.height - 1;

            bool isValidEdgeAndFacing = false;

            // 检查：是否在顶部边缘，且朝上？
            if (pos.y == maxY && facing == Direction.Up) isValidEdgeAndFacing = true;
            // 检查：是否在底部边缘，且朝下？
            else if (pos.y == 0 && facing == Direction.Down) isValidEdgeAndFacing = true;
            // 检查：是否在右侧边缘，且朝右？
            else if (pos.x == maxX && facing == Direction.Right) isValidEdgeAndFacing = true;
            // 检查：是否在左侧边缘，且朝左？
            else if (pos.x == 0 && facing == Direction.Left) isValidEdgeAndFacing = true;

            if (!isValidEdgeAndFacing)
            {
                // 如果既不贴边，开口也不朝外，直接亮红灯！
                Debug.LogWarning($"[放置失败] I/O 匣子必须放在机箱边缘，且开口必须朝向外部！当前坐标:{pos}, 朝向:{facing}");
                return false;
            }
        }

        return true; // 恭喜，全部校验通过！
    }

    // ==========================================
    // 动作执行：正式将模块写入数据底座
    // ==========================================
    public void PlaceModule(MachineShellData shell, MachineModuleData module)
    {
        if (!CanPlaceModule(shell, module)) return;

        // 1. 建立归属关系
        module.ParentShell = shell;
        shell.Modules.Add(module);

        // 2. 将数据写入大世界网格
        List<Vector2Int> localCells = module.GetOccupiedLocalCells();
        foreach (Vector2Int localPos in localCells)
        {
            Vector2Int worldPos = new Vector2Int(shell.Bounds.xMin + localPos.x, shell.Bounds.yMin + localPos.y);
            GridCell cell = GridManager.Instance.GetGridCell(worldPos);
            
            if (cell != null)
            {
                cell.OccupyingModule = module;
            }
        }

        Debug.Log($"[系统提示] 成功在机箱 {shell.ShellID} 放置了模块！当前模块数: {shell.Modules.Count}");
    }
}