using UnityEngine;

/// <summary>
/// 全局 UI 热键监听器
/// 必须挂载在永远处于激活状态的物体上（如 Core 空物体 或 主 Canvas）
/// </summary>
public class UIGlobalHotkey : MonoBehaviour
{
    private void Update()
    {
        // 监听 P 键，完全独立，不干涉大世界交互代码
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (TechTreeGridUI.Instance != null)
            {
                TechTreeGridUI.Instance.ToggleTechTreeUI();
            }
        }
    }
}