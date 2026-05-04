using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using AssemblyLine.Core.Manager.SaveLoad;
using AssemblyLine.Core.Manager.Audio;
using DG.Tweening;

namespace AssemblyLine.Core.Manager.GameFlow
{
    public class GameFlowManager : MonoBehaviour
    {
        public static GameFlowManager Instance { get; private set; }

        [Header("UI 过渡 (全局不穿透黑幕)")]
        public CanvasGroup FadeCanvasGroup; 

        // 核心状态：只保留数据，不持有 UI 引用
        public bool IsGamePaused { get; private set; } = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        }

        /// <summary>
        /// 核心切换方法：仅处理逻辑状态与全局总线调度
        /// </summary>
        public void TogglePauseState()
        {
            // 如果正在黑屏转场，禁止切换状态
            if (FadeCanvasGroup != null && FadeCanvasGroup.gameObject.activeSelf) return;

            IsGamePaused = !IsGamePaused;

            // 1. 驱动底层模拟系统 (SimulationController)
            if (SimulationController.Instance != null)
            {
                SimulationController.Instance.SetPauseState(IsGamePaused);
            }

            // 2. 驱动音频总线 (AudioManager)
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetAudioPauseState(IsGamePaused);
            }
            
            Debug.Log($"[GameFlow] 全局暂停状态已切换为: {IsGamePaused}");
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