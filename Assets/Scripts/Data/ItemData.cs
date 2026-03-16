public class ItemData
{
    public string ItemName = "铁矿石"; // 暂时硬编码为铁矿石
    
    // 物品在当前格子里的移动进度 (0.0f 到 1.0f)
    // 0.0 代表刚进入格子中心，1.0 代表即将离开格子进入下一个格子
    public float Progress = 0.0f; 
    // 【新增】：物品的“GPS”，时刻记录自己所在的格子
    public GridCell CurrentCell;
}