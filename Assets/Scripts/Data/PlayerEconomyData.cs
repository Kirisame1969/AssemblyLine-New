using System;

/// <summary>
/// 纯数据类：玩家经济档案 (严格脱离 MonoBehaviour)
/// </summary>
[Serializable]
public class PlayerEconomyData
{
    // 【核心防线】：必须使用 long (长整型) 防止溢出。初始测试资金 500。
    public long Funds = 500;
    // 新增：天赋点 (用于科技树解锁的次级核心货币)
    public int TalentPoints = 0;
    // ==========================================
    // 周期财务寄存器 (Tick 驱动下的临时状态)
    // ==========================================

    /// <summary>
    /// 本期累计总收入 (例如交付仓库变现)
    /// </summary>
    public long CurrentCycleRevenue = 0;

    /// <summary>
    /// 本期累计总支出 (例如市场采购与维护费)
    /// </summary>
    public long CurrentCycleExpenses = 0;
}