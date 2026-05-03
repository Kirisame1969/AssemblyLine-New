using UnityEngine;
using UnityEngine.UI;
using AssemblyLine.Core.Manager.GameFlow;

namespace AssemblyLine.UI.MainMenu
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("按钮绑定")]
        public Button NewGameButton;
        public Button LoadGameButton; // 目前暂设为一个固定读取 "TestSlot" 的按钮，后期可扩展为存档列表面板
        public Button ExitButton;

        private void Start()
        {
            if (NewGameButton != null) NewGameButton.onClick.AddListener(OnNewGameClicked);
            if (LoadGameButton != null) LoadGameButton.onClick.AddListener(OnLoadGameClicked);
            if (ExitButton != null) ExitButton.onClick.AddListener(OnExitClicked);
        }

        private void OnNewGameClicked()
        {
            // 禁用按钮防多次连点
            NewGameButton.interactable = false; 
            GameFlowManager.Instance.StartNewGame();
        }

        private void OnLoadGameClicked()
        {
            LoadGameButton.interactable = false;
            // 暂时硬编码读取刚才测试用的槽位，下一阶段可以做完整的存档列表UI
            GameFlowManager.Instance.LoadGame("TestSlot"); 
        }

        private void OnExitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}