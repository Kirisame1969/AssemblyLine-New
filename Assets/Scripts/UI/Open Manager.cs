using UnityEngine;
using UnityEngine.UI;

public class UIManagerAAA : MonoBehaviour
{
    [Header("UI 面板引用")]
    public GameObject myPanel;      // 需要操作的面板

    [Header("按钮引用")]
    public Button hideButton;       // 按钮一：用于隐藏面板
    public Button quitButton;       // 按钮二：用于退出游戏

    void Start()
    {
        // 1. 游戏开始时，初始显示面板
        if (myPanel != null)
        {
            myPanel.SetActive(true);
        }

        // 2. 为“按钮一”绑定点击事件，执行隐藏操作
        if (hideButton != null)
        {
            hideButton.onClick.AddListener(HidePanel);
        }

        // 3. 为“按钮二”绑定点击事件，执行退出游戏操作
        if (quitButton != null)
        {
            quitButton.onClick.AddListener(QuitGame);
        }
    }

    // --- 下面是具体的执行方法 ---

    // 隐藏面板的方法
    void HidePanel()
    {
        if (myPanel != null)
        {
            myPanel.SetActive(false);
        }
    }

    // 退出游戏的方法
    void QuitGame()
    {
        // 注意：Application.Quit() 在 Unity 编辑器运行模式下是看不出效果的。
        // 所以这里加一句 Debug.Log，方便你在控制台(Console)确认代码是否被成功触发。
        Debug.Log("执行了退出游戏操作！");

        // 打包出游戏本体（如 .exe 或 .apk）后，这行代码才会真正关闭整个游戏程序。
        Application.Quit();
    }
}