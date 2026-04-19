using System;
using System.Collections.Generic;
using UnityEngine;
using Core.Economy;

namespace Core.Market
{
    /// <summary>
    /// 模拟的物品字典键，用来代替具体的实例化Item数据
    /// </summary>
    [Serializable]
    public class DummyItemDefinition
    {
        public string ItemID;
        public string ItemName;
        public float BasePrice;
        public float QualityScore; // 综合质量评分的基数
    }

    /// <summary>
    /// 表示物品大盘数据的结构，包含当前周期的需求极限
    /// </summary>
    [Serializable]
    public struct MarketBoardData
    {
        public DummyItemDefinition Item;
        public int TotalDemand; // 本期市场总需求最大值
    }

    /// <summary>
    /// 竞争性市场与爆仓系统处理中心
    /// </summary>
    public class MarketManager : MonoBehaviour
    {
        public static MarketManager Instance { get; private set; }

        [Header("市场大盘基础池")]
        public List<MarketBoardData> ActiveMarketItems = new List<MarketBoardData>();

        [Header("玩家仓库模拟 (物理承载极限限制)")]
        [Tooltip("当前总库存件数")]
        public int CurrentWarehouseLoad = 0;
        [Tooltip("仓库能容纳的最大极限载重")]
        public int MaxWarehouseCapacity = 2000;

        // 【模拟数据字典】：假装这个是你的仓储管理系统里提取出的玩家备货区
        // 实际开发中可以通过 GetComponent/事件获取，这里仅为打通数据流链路预留接口
        private Dictionary<DummyItemDefinition, int> _pendingSales = new Dictionary<DummyItemDefinition, int>();
        
        // 【模拟数据字典】：玩家自己自定义的物品发售定价卡
        private Dictionary<DummyItemDefinition, float> _playerPrices = new Dictionary<DummyItemDefinition, float>();

        // 临时缓存本次爆仓导致退回的滞销品，用于处理玩家对于危机的不同应对操作
        private Dictionary<DummyItemDefinition, int> _crisisOverflowItems = new Dictionary<DummyItemDefinition, int>();

        // ========== 事件广播通道 ========== //
        // UI专用：触发爆仓时，可向UI面板发送数据 (参数：当前滞销退回后库存, 当前仓库极限容量)
        public event Action<int, int> OnWarehouseOverflowCrisis; 

        // 标志位：是否正处于爆仓危机（未解除），此时防止进一步进行经济或时间滚动
        private bool _isCrisisPending = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            InitMockData();
        }

        private void Start()
        {
            if (SimulationController.Instance != null)
            {
                // 仅依附周期触发，拒绝Update轮询
                SimulationController.Instance.OnNewCycleStarted += HandleMarketSettlement;
            }
        }

        private void OnDestroy()
        {
            if (SimulationController.Instance != null)
            {
                SimulationController.Instance.OnNewCycleStarted -= HandleMarketSettlement;
            }
        }

        /// <summary>
        /// 粗略生成一点假数据用来展示跑通这个黑盒
        /// </summary>
        private void InitMockData()
        {
            var stone = new DummyItemDefinition { ItemID = "stone", ItemName = "精打磨石块", BasePrice = 15f, QualityScore = 1.2f };
            // 设置大盘：这个周期市场只要卖 400 个
            ActiveMarketItems.Add(new MarketBoardData { Item = stone, TotalDemand = 400 });
            
            // 玩家这周期囤了 300 个准备卖
            _pendingSales.Add(stone, 300);
            
            // 玩家贪婪心作祟，设了个稍高的价格
            _playerPrices.Add(stone, 20f); 
        }

        // =============================================================== //
        // 外部发货仓对接 API
        // =============================================================== //

        /// <summary>
        /// 供仓库 UI 或 发货机（如传送带终点站）调用的【核心上架接口】
        /// 玩家通过它，把流水线造出的物品正式推入本周期的市场预售池
        /// </summary>
        /// <param name="item">要售卖的物品定义</param>
        /// <param name="quantity">本次上架的数量</param>
        /// <param name="sellPrice">玩家当前的自定售价</param>
        public void AddPendingSale(DummyItemDefinition item, int quantity, float sellPrice)
        {
            // 1. 记录或累加上架数量
            if (_pendingSales.ContainsKey(item))
            {
                _pendingSales[item] += quantity;
            }
            else
            {
                _pendingSales.Add(item, quantity);
            }

            // 2. 更新定价（如果玩家之前已经挂过这个商品，这里简单处理为价格会被此次的新定价覆盖）
            if (_playerPrices.ContainsKey(item))
            {
                _playerPrices[item] = sellPrice;
            }
            else
            {
                _playerPrices.Add(item, sellPrice);
            }

            Debug.Log($"【物流发货】新增市场挂单: [{item.ItemName}] x{quantity}，标价：￥{sellPrice}。将在此周期末参与跨商分配！");
        }

        /// <summary>
        /// 核心引擎：在每个周期更新点执行跨商家的需求博弈吃蛋糕分配
        /// </summary>
        private void HandleMarketSettlement(int newCycle)
        {
            // 若上个危机的红牌没解开，不执行新的结算逻辑
            if (_isCrisisPending) return;

            _crisisOverflowItems.Clear();

            // 遍历市场上的每个活跃品类大盘
            foreach (var boardData in ActiveMarketItems)
            {
                var itemType = boardData.Item;
                
                // 1. 获取玩家挂单与定价
                int playerStock = _pendingSales.ContainsKey(itemType) ? _pendingSales[itemType] : 0;
                float playerAskPrice = _playerPrices.ContainsKey(itemType) ? _playerPrices[itemType] : itemType.BasePrice;

                // 若这件物品完全没有备货，直接跳过计算节约性能
                if (playerStock <= 0) continue;

                // 2. 计算玩家大盘的竞争力评分 = 质量分 * (基础价 / 玩家挂单价) 
                float playerCompetitiveness = itemType.QualityScore * (itemType.BasePrice / playerAskPrice);
                float playerMarketWeight = playerStock * playerCompetitiveness; // 玩家在这个品类的【抢配额占有权权重】

                // 3. 构建 3个虚拟黑盒 AI 对手的信息，产生博弈干扰
                int aiTotalSupply = 0;
                float aiTotalWeight = 0f;
                for (int i = 0; i < 3; i++)
                {
                    int aiQty = UnityEngine.Random.Range(50, 150);
                    float aiCompScore = UnityEngine.Random.Range(0.6f, 1.4f); // AI的浮动竞争力（价格也浮动过）
                    aiTotalSupply += aiQty;
                    aiTotalWeight += aiQty * aiCompScore;
                }

                // 4. 总供给与分配逻辑 (核心分配切蛋糕算法)
                int totalSupplySum = playerStock + aiTotalSupply;
                float totalMarketWeight = playerMarketWeight + aiTotalWeight;
                
                int playerActualSold = 0;

                // 供不应求，玩家直接全部卖光
                if (totalSupplySum <= boardData.TotalDemand)
                {
                    playerActualSold = playerStock;
                }
                else
                {
                    // 供过于求，按【挂单数量 × 竞争力权重】在总池子中占用的切片比例分配实际出货量
                    float playerShareRatio = playerMarketWeight / totalMarketWeight;
                    playerActualSold = Mathf.FloorToInt(boardData.TotalDemand * playerShareRatio);

                    // 兜个底，系统保证卖出去的数量绝对不可能超过你挂货的数量
                    playerActualSold = Mathf.Min(playerActualSold, playerStock); 
                }

                // 5. 结算与滞销回退处理
                int unsoldReturned = playerStock - playerActualSold;
                float actualEarned = playerActualSold * playerAskPrice;

                // (A) 资金打款结汇
                if (EconomyManager.Instance != null && actualEarned > 0)
                {
                    EconomyManager.Instance.AddIncome(actualEarned);
                }

                Debug.Log($"【市场黑盒战报】品类 {itemType.ItemName} | 玩家售出 {playerActualSold} | AI售出 {boardData.TotalDemand - playerActualSold} | 退回滞销品 {unsoldReturned} | 玩家获利 {actualEarned}");

                // (B) 滞销物流回库，压榨玩家的存储网络
                if (unsoldReturned > 0)
                {
                    CurrentWarehouseLoad += unsoldReturned;
                    _crisisOverflowItems.Add(itemType, unsoldReturned); // 加入本周期的缓存池
                }

                // (C) 清空这周期的预售队列配额（这里直接写0简单模拟）
                _pendingSales[itemType] = 0;
            }

            // ====== 收尾清算：是否要发爆仓警报 ======
            if (CurrentWarehouseLoad > MaxWarehouseCapacity)
            {
                TriggerOverflowCrisis();
            }
        }

        /// <summary>
        /// 触发爆仓危机，拉停时间切流
        /// </summary>
        private void TriggerOverflowCrisis()
        {
            _isCrisisPending = true;

            // 调用已经写好的控制中心，暂停跳周期，等玩家擦完屁股再运行
            if (SimulationController.Instance != null)
            {
                SimulationController.Instance.CurrentSpeed = TimeSpeed.Paused;
            }

            Debug.LogWarning($"<color=red>【红色警报】发生严重爆仓危机！当前库存量 [{CurrentWarehouseLoad}/{MaxWarehouseCapacity}]</color>");
            OnWarehouseOverflowCrisis?.Invoke(CurrentWarehouseLoad, MaxWarehouseCapacity);
        }

        // =============================================================== //
        // 供外部 UI 或输入系统直接调用的 API 接入口，用于危机解除处理
        // =============================================================== //

        /// <summary>
        /// API解围一：跳楼价清仓。瞬间全清当回退换物品，拿到一点点补贴
        /// </summary>
        public void HandleCrisisFireSale()
        {
            if (!_isCrisisPending) return;

            int totalCleared = 0;
            float scrapIncome = 0f;

            foreach (var kvp in _crisisOverflowItems)
            {
                int qty = kvp.Value;
                DummyItemDefinition item = kvp.Key;

                totalCleared += qty;
                // 按市场价 15% 的比例回收补贴
                scrapIncome += (qty * item.BasePrice) * 0.15f;
            }

            CurrentWarehouseLoad -= totalCleared;
            _crisisOverflowItems.Clear();

            if (EconomyManager.Instance != null)
            {
                EconomyManager.Instance.AddIncome(scrapIncome);
                Debug.Log($"【危机管控中心机制】执行[跳楼清仓]，销毁了 {totalCleared} 件占容的滞销品，回血金币 {scrapIncome}。");
            }

            EndCrisis();
        }

        /// <summary>
        /// API解围二：直接全量抛弃当作工业垃圾，并且交一波环保罚款
        /// </summary>
        public void HandleCrisisDiscard()
        {
            if (!_isCrisisPending) return;

            int totalCleared = 0;
            float wasteFines = 0f;

            foreach (var kvp in _crisisOverflowItems)
            {
                totalCleared += kvp.Value;
                wasteFines += kvp.Value * 1.5f; // 假设每件丢弃垃圾需付1.5处理费
            }

            CurrentWarehouseLoad -= totalCleared;
            _crisisOverflowItems.Clear();

            if (EconomyManager.Instance != null)
            {
                EconomyManager.Instance.RecordExpense(wasteFines);
                Debug.Log($"【危机管控中心机制】执行[全量抛弃]，销毁了 {totalCleared} 件占容物，并因此缴纳了 {wasteFines} 违规处理罚款。");
            }

            EndCrisis();
        }

        /// <summary>
        /// API解围三：租借临时大仓。花巨资马上暴力扩充本周期容量
        /// </summary>
        public void HandleCrisisRentStorage()
        {
            if (!_isCrisisPending) return;

            float emergencyRentFee = 2500f; // 一笔巨大的租金
            int emergencySpaceBuff = 3000;  // 获取大量额外空间

            if (EconomyManager.Instance != null)
            {
                // 花钱消灾
                EconomyManager.Instance.RecordExpense(emergencyRentFee);
                MaxWarehouseCapacity += emergencySpaceBuff;
                
                Debug.Log($"【危机管控中心机制】执行[租借大仓]，硬扛下 {emergencyRentFee} 租金，容积当场膨胀至 {MaxWarehouseCapacity}！");
            }

            // 之后你可以在下一个新周期的 HandleMarketSettlement 里再重置回去，这里仅模拟临时扩容
            EndCrisis();
        }

        /// <summary>
        /// 清理标识并恢复正常游戏速度流转
        /// </summary>
        private void EndCrisis()
        {
            _isCrisisPending = false;
            
            if (SimulationController.Instance != null)
            {
                // 可以记忆前一次调速的状态，这里硬核复位到正常1档速度让游戏继续
                SimulationController.Instance.CurrentSpeed = TimeSpeed.Normal;
            }

            Debug.Log("【系统】：爆仓红色警报已解除！传送带开始进行运转...");
        }
    }
}
