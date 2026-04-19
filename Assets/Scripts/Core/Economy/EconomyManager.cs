using System;
using UnityEngine;

namespace Core.Economy
{
    /// <summary>
    /// 全局经济中枢管理系统
    /// </summary>
    public class EconomyManager : MonoBehaviour
    {
        public static EconomyManager Instance { get; private set; }

        [Header("资金设定")]
        [Tooltip("当前玩家拥有的资金")]
        public float CurrentFunds = 10000f;
        [Tooltip("破产警戒线，低于此值无法进行建造等即时消费")]
        public float BankruptcyThreshold = -5000f;
        [Tooltip("资金为负时的破产倒计时周期数")]
        public int BankruptcyCountdown = 3;

        [Header("本期财务数据")]
        [Tooltip("本周期的总计收入")]
        public float GrossRevenue = 0f;
        [Tooltip("本周期的总计支出")]
        public float TotalExpenses = 0f;

        // 向UI或其他系统广播事件的高解耦委托
        public event Action<float> OnFundsChanged;
        public event Action<float, float> OnCycleFinancialReport; // revenue, expenses
        public event Action OnGameOver;

        private int _defaultCountdown;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            _defaultCountdown = BankruptcyCountdown;
        }

        private void Start()
        {
            if (SimulationController.Instance != null)
            {
                // 订阅周期交替事件
                SimulationController.Instance.OnNewCycleStarted += HandleCycleEnd;
            }
            else
            {
                Debug.LogWarning("未找到 SimulationController，EconomyManager 无法订阅周期事件。");
            }
        }

        private void OnDestroy()
        {
            if (SimulationController.Instance != null)
            {
                SimulationController.Instance.OnNewCycleStarted -= HandleCycleEnd;
            }
        }

        /// <summary>
        /// 尝试进行大世界即时消费（例如建造）
        /// </summary>
        public bool TrySpend(float amount)
        {
            // 判断即时扣款后是否突破了警戒线
            if (CurrentFunds - amount < BankruptcyThreshold)
            {
                Debug.LogWarning("资金不足且超出破产警戒线，不允许建造/支出。");
                return false;
            }

            CurrentFunds -= amount;
            TotalExpenses += amount; // 将该笔大世界消费也记录为当前周期的支出
            
            OnFundsChanged?.Invoke(CurrentFunds);
            return true;
        }

        /// <summary>
        /// 增加收入并记录到报表
        /// </summary>
        public void AddIncome(float amount)
        {
            CurrentFunds += amount;
            GrossRevenue += amount;
            OnFundsChanged?.Invoke(CurrentFunds);
        }

        /// <summary>
        /// 仅记录支出/扣款，计入本周期总支出
        /// </summary>
        public void RecordExpense(float amount)
        {
            CurrentFunds -= amount;
            TotalExpenses += amount;
            OnFundsChanged?.Invoke(CurrentFunds);
        }

        /// <summary>
        /// 模拟全图运行的期末设施维护费
        /// </summary>
        private float CalculateMaintenanceCost()
        {
            // TODO: 未来可在此遍历 GridManager 或 MachineManager 里的所有机器统计费用
            // 目前先用模拟的一个基础值作为周期维护费扣除
            return 150f;
        }

        /// <summary>
        /// 响应新周期开始（视为上一个周期的结算尾声）
        /// </summary>
        private void HandleCycleEnd(int cycle)
        {
            // 1. 周期末结算全图设施维护费并扣款
            float maintenanceCost = CalculateMaintenanceCost();
            RecordExpense(maintenanceCost);
            
            // 2. 抛出上一期完整的财务结算报表，供UI面板使用
            Debug.Log($"【财务报表】周期 {cycle} 结算。本期总收入: {GrossRevenue}, 本期总支出: {TotalExpenses}");
            OnCycleFinancialReport?.Invoke(GrossRevenue, TotalExpenses);

            // 3. 检查银行账户健康度，触发破产
            if (CurrentFunds < 0)
            {
                BankruptcyCountdown--;
                Debug.LogWarning($"【资金告警】警告，资金已为负！破产倒计时剩余：{BankruptcyCountdown} 个周期。");

                if (BankruptcyCountdown <= 0)
                {
                    Debug.LogError("【游戏结束】触发 GAME OVER: 资金链断裂，进入破产清算。");
                    OnGameOver?.Invoke();
                }
            }
            else
            {
                // 回归正数，恢复倒计时血量
                BankruptcyCountdown = _defaultCountdown;
            }

            // 4. 重置本期的财报寄存器
            GrossRevenue = 0f;
            TotalExpenses = 0f;
        }
    }
}
