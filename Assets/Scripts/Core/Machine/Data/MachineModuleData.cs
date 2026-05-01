using System.Collections.Generic;
using UnityEngine;

// 作为所有机箱内部模块（如核心、I/O 端口、普通组件）的抽象父类。
// 它统一管理模块的基础状态数据（坐标、旋转、所属机壳、配置图纸），并提供默认的空间占用与连通性计算逻辑。

namespace AssemblyLine.Data.Machine
{
    /// <summary>
    /// 机器内部模块的数据基类。
    /// 存储模块的运行时状态，并提供基于图纸的基础空间计算方法。
    /// </summary>
    public abstract class MachineModuleData
    {
        /// <summary>
        /// 指向该模块所属的机器外壳实例。
        /// 注意：在进行数据序列化（存档）时，可能有堆栈溢出问题, 需处理此处的双向引用关系。
        /// </summary>
        public MachineShellData ParentShell; 
        
        /// <summary>
        /// 该模块对应的静态配置数据（图纸）。
        /// </summary>
        public ModuleDefinition Definition; 
        
        /// <summary>
        /// 模块在机箱内部网格的局部坐标锚点（通常为左下角）。
        /// </summary>
        public Vector2Int LocalBottomLeft; 
        
        /// <summary>
        /// 模块当前的旋转状态。
        /// </summary>
        public ModuleRotation Rotation = ModuleRotation.R0;

        /// <summary>
        /// 计算模块在当前旋转状态下，实际占据的所有局部坐标点。
        /// </summary>
        /// <returns>局部坐标集合</returns>
        public virtual List<Vector2Int> GetOccupiedLocalCells()
        {
            List<Vector2Int> result = new List<Vector2Int>();
            
            // 若未配置形状数据，默认仅占用锚点所在的一格
            // 【架构师修正】：增加 Definition.Shape.BaseCells == null 的空值校验，防止列表未初始化
            if (Definition == null || Definition.Shape == null || Definition.Shape.BaseCells == null || Definition.Shape.BaseCells.Count == 0)
            {
                result.Add(LocalBottomLeft);
                return result;
            }

            // 获取经过旋转矩阵计算后的相对坐标系
            List<Vector2Int> rotatedCells = Definition.Shape.GetRotatedCells(Rotation);

            // 将相对坐标转换为机箱内部的实际局部坐标
            foreach (Vector2Int cellOffset in rotatedCells)
            {
                result.Add(new Vector2Int(LocalBottomLeft.x + cellOffset.x, LocalBottomLeft.y + cellOffset.y));
            }

            return result;
        }

        /// <summary>
        /// 计算该模块内部必须保持物理连通的边界集合。
        /// 逻辑依据：如果模块占据的两个格子在物理上相邻（曼哈顿距离为1），则这两格之间不允许有机壳隔断墙。
        /// </summary>
        /// <returns>必须连通的内部墙壁结构体集合</returns>
        public virtual List<InternalWall> GetRequiredInternalConnections()
        {
            List<InternalWall> connections = new List<InternalWall>();
            List<Vector2Int> cells = GetOccupiedLocalCells();

            // 双重循环遍历模块占据的所有格子
            for (int i = 0; i < cells.Count; i++)
            {
                for (int j = i + 1; j < cells.Count; j++)
                {
                    // 计算两格之间的曼哈顿距离
                    int dx = Mathf.Abs(cells[i].x - cells[j].x);
                    int dy = Mathf.Abs(cells[i].y - cells[j].y);
                    
                    // 距离为1代表相邻，生成连通性约束
                    if (dx + dy == 1)
                    {
                        connections.Add(new InternalWall(cells[i], cells[j]));
                    }
                }
            }
            return connections;
        }
    }
}