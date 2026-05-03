using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using AssemblyLine.Core.Manager.SaveLoad;

namespace AssemblyLine.Core.Manager.GameFlow
{
    public class GameFlowManager : MonoBehaviour
    {
        public static GameFlowManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
            // 注意：由于它和 Bootstrapper 挂在同一个 GlobalManagers 节点上，
            // 它也会自动享受 DontDestroyOnLoad 的效果。
        }

        // ==========================================
        // 外部流转接口
        // ==========================================
        public void StartNewGame()
        {
            StartCoroutine(TransitionToGameRoutine(null));
        }

        public void LoadGame(string slotName)
        {
            StartCoroutine(TransitionToGameRoutine(slotName));
        }

        public void ReturnToMainMenu()
        {
            // 返回主菜单时，MainGameScene 会被直接卸载，所有局部管理器(网格/机器等)被瞬间安全销毁
            SceneManager.LoadScene("MainMenuScene");
            Debug.Log("[GameFlow] 已返回主菜单，释放工厂内存。");
        }

        // ==========================================
        // 核心异步流转协程
        // ==========================================
        private IEnumerator TransitionToGameRoutine(string saveSlotToLoad)
        {
            // 【UI 预留口】：这里可以触发“黑屏淡入”或“显示 Loading 界面”的方法

            // 1. 异步加载游戏主场景 (这里需要你将原来的 MainScene 改名为 MainGameScene)
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("MainGameScene");
            
            // 2. 等待场景加载完成
            while (!asyncLoad.isDone)
            {
                // 【UI 预留口】：这里可以更新进度条 asyncLoad.progress
                yield return null;
            }

            // 3. 【时序防呆】：再额外等待一帧，确保 MainGameScene 里所有管理器的 Awake 和 Start 都已执行完毕
            yield return new WaitForEndOfFrame();

            // 4. 根据载荷执行数据注入
            if (string.IsNullOrEmpty(saveSlotToLoad))
            {
                Debug.Log("[GameFlow] 开启新游戏。");
                // 新游戏无需特殊操作，场景自带的管理器会生成空网格和初始资金 (500)
            }
            else
            {
                Debug.Log($"[GameFlow] 场景加载完毕，开始反序列化存档: {saveSlotToLoad}");
                SaveLoadManager.Instance.LoadGame(saveSlotToLoad);
            }

            // 【UI 预留口】：这里触发“黑屏淡出”或“关闭 Loading 界面”
        }
    }
}