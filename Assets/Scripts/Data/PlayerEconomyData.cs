using System;

/// <summary>
/// 纯数据类：玩家经济档案 (严格脱离 MonoBehaviour)
/// </summary>
[Serializable]
public class PlayerEconomyData
{
    // 【核心防线】：必须使用 long (长整型)，防止自动化游戏后期的天文数字导致 int 溢出变成负数。
    // 我们在此赋予 500 的测试启动资金，防止冷启动死锁。
    public long Funds = 500; 
}