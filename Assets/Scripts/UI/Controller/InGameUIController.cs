using UnityEngine;
using UnityEngine.UI;
using AssemblyLine.Core.Manager.GameFlow; // 引入流程控制器所在的命名空间

namespace AssemblyLine.UI
{
    /// <summary>
    /// 表现层：游戏内 UI 总控。
    /// 负责管理 MainGameScene 中的顶部栏、返回按钮等。
    /// </summary>
    public class InGameUIController : MonoBehaviour
    {
        [Header("UI 元素引用")]
        [SerializeField] private Button returnToMenuButton;

        private void Start()
        {
            // 严谨性检查：确保按钮引用已拖入
            if (returnToMenuButton != null)
            {
                // 【核心绑定】：通过代码给按钮添加监听事件
                // 这样就不需要手动在 Inspector 里拖拽
                returnToMenuButton.onClick.AddListener(OnReturnButtonClicked);
            }
            else
            {
                Debug.LogWarning("[InGameUIController] 返回主菜单按钮未在 Inspector 中赋值！");
            }
        }

        private void OnReturnButtonClicked()
        {
            // 呼叫控制层单例执行场景切换
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.ReturnToMainMenu();
            }
        }
        
        private void OnDestroy()
        {
            // 良好的编程习惯：销毁时移除监听，防止内存泄漏
            if (returnToMenuButton != null)
            {
                returnToMenuButton.onClick.RemoveListener(OnReturnButtonClicked);
            }
        }
    }
}