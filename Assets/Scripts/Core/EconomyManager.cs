using System;
using UnityEngine;

/// <summary>
/// 控制层：全局经济调度中心
/// 负责拦截资金流转、执行周期结算与广播，绝对不引用 UI。
/// </summary>
public class EconomyManager : MonoBehaviour
{
    public static EconomyManager Instance { get; private set; }

    public PlayerEconomyData EconomyData = new PlayerEconomyData();

    // ==========================================
    // 事件总线
    // ==========================================
    public event Action<long> OnFundsChanged;
    public event Action<long, long> OnCycleFinancialReport; // revenue, expenses
    public event Action<int> OnTalentPointsChanged; // 新增：天赋点变动委托

    // ==========================================
    // 简易方案常量：动态维护费基准值
    // ==========================================
    private const long BASE_SHELL_MAINTENANCE_COST = 50;
    private const long PER_MODULE_MAINTENANCE_COST = 10;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        OnFundsChanged?.Invoke(EconomyData.Funds);
        OnTalentPointsChanged?.Invoke(EconomyData.TalentPoints);

        // 【第二步核心】：挂载到心脏起搏器！订阅周期交替事件
        if (SimulationController.Instance != null)
        {
            SimulationController.Instance.OnNewCycleStarted += HandleCycleEnd;
        }
        else
        {
            Debug.LogWarning("[经济系统] 未找到 SimulationController，无法订阅周期结算事件！");
        }
    }

    private void OnDestroy()
    {
        // 防御性编程：严格注销事件，防止切场景或销毁时引发内存泄漏
        if (SimulationController.Instance != null)
        {
            SimulationController.Instance.OnNewCycleStarted -= HandleCycleEnd;
        }
    }

    // ==========================================
    // 资金拦截器 (来自第一步)
    // ==========================================
    public void AddFunds(long amount)
    {
        if (amount <= 0) return;
        EconomyData.Funds += amount;
        EconomyData.CurrentCycleRevenue += amount; 
        OnFundsChanged?.Invoke(EconomyData.Funds);
    }

    public bool HasEnoughFunds(long amount)
    {
        return EconomyData.Funds >= amount;
    }

    public bool ConsumeFunds(long amount)
    {
        if (amount <= 0 || !HasEnoughFunds(amount)) return false;
        EconomyData.Funds -= amount;
        EconomyData.CurrentCycleExpenses += amount; 
        OnFundsChanged?.Invoke(EconomyData.Funds);
        return true;
    }
    public void AddTalentPoints(int amount)
    {
        if (amount <= 0) return;
        EconomyData.TalentPoints += amount;
        OnTalentPointsChanged?.Invoke(EconomyData.TalentPoints);
    }

    public bool HasEnoughTalentPoints(int amount)
    {
        return EconomyData.TalentPoints >= amount;
    }

    public bool ConsumeTalentPoints(int amount)
    {
        if (amount <= 0 || !HasEnoughTalentPoints(amount)) return false;
        EconomyData.TalentPoints -= amount;
        OnTalentPointsChanged?.Invoke(EconomyData.TalentPoints);
        return true;
    }
    public void ForceDeductFunds(long amount)
    {
        if (amount <= 0) return;
        EconomyData.Funds -= amount;
        EconomyData.CurrentCycleExpenses += amount; 
        OnFundsChanged?.Invoke(EconomyData.Funds);
    }

    // ==========================================
    // 【第二步新增】：周期结算与扁平化算法引擎
    // ==========================================

    /// <summary>
    /// 响应周期结束：执行算账、扣款与财报生成
    /// </summary>
    private void HandleCycleEnd(int cycle)
    {
        // 1. 瞬间遍历全图实体，计算本期动态维护费
        long maintenanceCost = CalculateDynamicMaintenanceCost();

        // 2. 执行系统强制扣款（允许跌破为负数）
        if (maintenanceCost > 0)
        {
            ForceDeductFunds(maintenanceCost);
            Debug.Log($"[经济系统] 周期 {cycle} 结算：扣除全图设施维护费 ${maintenanceCost}。");
        }

        // 3. 抛出完整的周期财报，供UI监听
        OnCycleFinancialReport?.Invoke(EconomyData.CurrentCycleRevenue, EconomyData.CurrentCycleExpenses);

        // 4. 清空当期寄存器，开启下一周期的记账
        EconomyData.CurrentCycleRevenue = 0;
        EconomyData.CurrentCycleExpenses = 0;
    }

    /// <summary>
    /// 性能核心算法：无 GC 扁平化暴力遍历
    /// </summary>
    private long CalculateDynamicMaintenanceCost()
    {
        // 防御性校验
        if (MachineManager.Instance == null || MachineManager.Instance.AllActiveShells == null)
            return 0;

        long totalCost = 0;
        var shells = MachineManager.Instance.AllActiveShells;

        // 绝对禁止使用 foreach。利用 for 循环进行无装箱内存的遍历
        for (int i = 0; i < shells.Count; i++)
        {
            var shell = shells[i];
            if (shell == null) continue;

            // 算法：单机箱费用 = 外壳基础费 + (内部模块数量 * 单模块均价)
            long shellCost = BASE_SHELL_MAINTENANCE_COST;
            
            if (shell.Modules != null)
            {
                shellCost += shell.Modules.Count * PER_MODULE_MAINTENANCE_COST;
            }

            totalCost += shellCost;
        }

        return totalCost;
    }
}