// 新建文件：EconomyManager.cs

using System;
using UnityEngine;

/// <summary>
/// 控制层：全局经济调度中心
/// 负责资金的增减逻辑校验，并向表现层广播变动事件。
/// </summary>
public class EconomyManager : MonoBehaviour
{
    // 极简严格单例
    public static EconomyManager Instance { get; private set; }

    // 挂载纯数据层档案
    public PlayerEconomyData EconomyData = new PlayerEconomyData();

    // ==========================================
    // C# 委托事件：供 UI 层 (View) 监听。
    // 这样数据层就不需要引用任何 UI 组件，实现完美解耦。
    // ==========================================
    public event Action<long> OnFundsChanged;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // 游戏启动时，广播一次初始资金，让 UI 面板初始化显示
        OnFundsChanged?.Invoke(EconomyData.Funds);
    }

    /// <summary>
    /// 增加资金 (例如交付仓库卖出物品时调用)
    /// </summary>
    public void AddFunds(long amount)
    {
        if (amount <= 0) return;
        EconomyData.Funds += amount;
        
        // 广播资金变动
        OnFundsChanged?.Invoke(EconomyData.Funds);
    }

    /// <summary>
    /// 检查资金是否充足 (例如采购终端进货前调用)
    /// </summary>
    public bool HasEnoughFunds(long amount)
    {
        return EconomyData.Funds >= amount;
    }

    /// <summary>
    /// 消费资金 (扣款成功返回 true，余额不足返回 false)
    /// </summary>
    public bool ConsumeFunds(long amount)
    {
        if (amount <= 0 || !HasEnoughFunds(amount)) return false;
        
        EconomyData.Funds -= amount;
        
        // 广播资金变动
        OnFundsChanged?.Invoke(EconomyData.Funds);
        return true;
    }
}