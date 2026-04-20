// 新建文件或附加到你的 UI 目录：EconomyUIController.cs
using UnityEngine;
using TMPro; // 强烈建议使用 TextMeshPro

public class EconomyUIController : MonoBehaviour
{
    [Tooltip("拖入显示金钱的 TextMeshProUGUI 组件")]
    public TMP_Text FundsText;

    private void Start()
    {
        // 防呆
        if (EconomyManager.Instance == null || FundsText == null) return;

        // 1. 游戏启动时，主动拉取一次最新数据 (Pull)
        UpdateFundsDisplay(EconomyManager.Instance.EconomyData.Funds);

        // 2. 订阅数据层的变动事件 (Observe)
        // 任何地方（买、卖、测试按钮）修改了资金，这里都会自动触发！
        EconomyManager.Instance.OnFundsChanged += UpdateFundsDisplay;
    }

    private void OnDestroy()
    {
        // 【严苛规范】：MonoBehaviour 销毁时必须注销事件，防止内存泄漏与空指针！
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.OnFundsChanged -= UpdateFundsDisplay;
        }
    }

    // 真正的改字逻辑
    private void UpdateFundsDisplay(long newFunds)
    {
        // 格式化为带逗号的千分位（例如 1,500,000）
        FundsText.text = $"$ {newFunds:N0}";
        
        // 进阶体验：你可以在这里加一个 DoTween 的 PunchScale 动画，让文字在跳钱时“蹦”一下
    }
}