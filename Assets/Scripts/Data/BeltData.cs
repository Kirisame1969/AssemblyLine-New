
// 1. 定义方向枚举 (顺时针，方便后续计算)
public enum Direction { Up = 0, Right = 1, Down = 2, Left = 3 }

// 2. 传送带的纯数据类
public class BeltData
{
    public Direction Dir;
    
    // 预留：未来它会归属于某个条带 (StripData)
    public StripData ParentStrip; 
}