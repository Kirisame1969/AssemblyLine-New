using UnityEngine;
using UnityEngine.UI; // 必须引入UI命名空间，才能使用 Button 类

public class UIManager : MonoBehaviour
{
    [Header("UI 面板引用")]
    public GameObject myPanel;      // 画布二中需要隐藏/显示的面板

    [Header("按钮引用")]
    public Button openButton;       // 画布一中的“打开”按钮
    public Button closeButton;      // 画布二面板中的“关闭”按钮

    void Start()
    {
        // 1. 游戏开始时，初始隐藏面板（继承自方法二）
        if (myPanel != null)
        {
            myPanel.SetActive(false);
        }

        // 2. 为“打开”按钮绑定点击事件
        if (openButton != null)
        {
            // 当 openButton 被点击时，执行 OpenPanel 方法
            openButton.onClick.AddListener(OpenPanel);
        }

        // 3. 为“关闭”按钮绑定点击事件
        if (closeButton != null)
        {
            // 当 closeButton 被点击时，执行 ClosePanel 方法
            closeButton.onClick.AddListener(ClosePanel);
        }
    }

    // --- 下面是具体的执行方法 ---

    // 打开面板的方法
    void OpenPanel()
    {
        myPanel.SetActive(true);
    }

    // 关闭面板的方法
    void ClosePanel()
    {
        myPanel.SetActive(false);
    }
}