using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using AssemblyLine.Core.Manager.SaveLoad;
using DG.Tweening;

namespace AssemblyLine.Core.Manager.GameFlow
{
    public class GameFlowManager : MonoBehaviour
    {
        public static GameFlowManager Instance { get; private set; }

        [Header("UI 过渡")]
        public CanvasGroup FadeCanvasGroup; // 拖入用于黑屏过渡的 CanvasGroup

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
            // 【核心修改】：改为开启协程处理过渡，不再直接跳转
            StartCoroutine(TransitionToMenuRoutine());
        }

        // ==========================================
    // 核心异步流转协程：平滑返回菜单
    // ==========================================
    private IEnumerator TransitionToMenuRoutine()
    {
        // 1. 黑屏淡入：遮挡当前场景并拦截点击
        if (FadeCanvasGroup != null)
        {
            FadeCanvasGroup.gameObject.SetActive(true);
            FadeCanvasGroup.blocksRaycasts = true; // 拦截点击
            // 等待淡入动画播放完毕
            yield return FadeCanvasGroup.DOFade(1f, 0.5f).WaitForCompletion();
        }

        // 2. 执行场景跳转：卸载工厂场景，加载主菜单
        SceneManager.LoadScene("MainMenuScene");

        // 3. 等待一帧：确保主菜单场景的 Awake/Start 初始化逻辑执行完毕
        yield return new WaitForEndOfFrame();

        // 4. 黑屏淡出：揭开主菜单
        if (FadeCanvasGroup != null)
        {
            FadeCanvasGroup.DOFade(0f, 0.5f).OnComplete(() => {
                FadeCanvasGroup.gameObject.SetActive(false);
                FadeCanvasGroup.blocksRaycasts = false; // 恢复交互
            });
        }

        Debug.Log("[GameFlow] 已平滑返回主菜单。");
    }

        // ==========================================
        // 核心异步流转协程 (接入黑屏动画)
        // ==========================================
        private IEnumerator TransitionToGameRoutine(string saveSlotToLoad)
        {
            // 【UI 动画】：黑屏淡入遮挡视线，并拦截玩家的所有点击
            if (FadeCanvasGroup != null)
            {
                FadeCanvasGroup.gameObject.SetActive(true);
                FadeCanvasGroup.blocksRaycasts = true; // 拦截鼠标射线
                
                // WaitForCompletion() 是 DOTween 提供的优雅协程等待方式
                yield return FadeCanvasGroup.DOFade(1f, 0.5f).WaitForCompletion(); 
            }

            // 1. 异步加载游戏主场景
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("MainGameScene");
            while (!asyncLoad.isDone)
            {
                yield return null;
            }

            // 2. 等待一帧，让所有管理器完成 Awake
            yield return new WaitForEndOfFrame();

            // 3. 执行数据注入
            if (!string.IsNullOrEmpty(saveSlotToLoad))
            {
                SaveLoadManager.Instance.LoadGame(saveSlotToLoad);
            }

            // 【UI 动画】：黑屏淡出揭开新场景，恢复玩家控制权
            if (FadeCanvasGroup != null)
            {
                FadeCanvasGroup.DOFade(0f, 0.5f).OnComplete(() => {
                    FadeCanvasGroup.gameObject.SetActive(false);
                    FadeCanvasGroup.blocksRaycasts = false; // 撤销拦截
                });
            }
        }
    }
}