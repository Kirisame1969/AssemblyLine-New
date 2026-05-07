using UnityEngine;
using System;
using AssemblyLine.Core.Manager.GameFlow;
using AssemblyLine.Core.Manager.SaveLoad;

public class InGameUIController : MonoBehaviour
{
    [Header("暂停菜单引用")]
    public PauseMenuWindow PauseMenu;
    public PauseMenuButton BtnSave;
    public PauseMenuButton BtnLoad;
    public PauseMenuButton BtnSettings;
    public PauseMenuButton BtnMainMenu;

    [Header("子面板控制器")]
    public UISaveLoadPanel LoadPanelController;

    [Header("弹窗引用")]
    public UIConfirmationPopup GeneralPopup;

    // 【核心修复】：将回调函数缓存为委托变量，防止刷新列表时失去指针导致卡片失效
    private Action<string, bool> _onSaveCardClicked;
    private Action<string, bool> _onLoadCardClicked;

    private void Start()
    {
        // ==========================================
        // 1. 初始化委托路由规则 (绝对隔离：在弹窗确认前，绝不调用 SaveGame)
        // ==========================================
        _onSaveCardClicked = (slotName, isNew) => 
        {
            if (isNew)
            {
                string defaultName = "Save_" + DateTime.Now.ToString("yyyyMMdd_HHmm");
                GeneralPopup.Show("新建存档", defaultName, (newName) => {
                    if (!string.IsNullOrEmpty(newName))
                    {
                        SaveLoadManager.Instance.SaveGame(newName);
                        // 使用缓存的委托重新刷新列表，保证新生成的卡片依然可以点击！
                        LoadPanelController.RefreshList(SaveLoadMode.Save, _onSaveCardClicked); 
                    }
                });
            }
            else
            {
                GeneralPopup.Show($"确认覆盖存档 {slotName} 吗？", null, (unused) => {
                    SaveLoadManager.Instance.SaveGame(slotName);
                    LoadPanelController.RefreshList(SaveLoadMode.Save, _onSaveCardClicked);
                });
            }
        };

        _onLoadCardClicked = (slotName, isNew) => 
        {
            GameFlowManager.Instance.TogglePauseState();
            PauseMenu.HideMenu(() => SetCameraLocked(false));
            SaveLoadManager.Instance.LoadGame(slotName);
        };

        // ==========================================
        // 2. 绑定侧边栏主干按钮
        // ==========================================
        if (BtnSave != null) BtnSave.OnClickAction = () => {
            PauseMenu.OpenSubPanel(PauseMenu.SavePanel);
            if (LoadPanelController != null) LoadPanelController.RefreshList(SaveLoadMode.Save, _onSaveCardClicked);
        };

        if (BtnLoad != null) BtnLoad.OnClickAction = () => {
            PauseMenu.OpenSubPanel(PauseMenu.LoadPanel);
            if (LoadPanelController != null) LoadPanelController.RefreshList(SaveLoadMode.Load, _onLoadCardClicked);
        };

        if (BtnMainMenu != null) BtnMainMenu.OnClickAction = () => {
            GameFlowManager.Instance.TogglePauseState();
            PauseMenu.HideMenu();
            GameFlowManager.Instance.ReturnToMainMenu();
        };
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (GameFlowManager.Instance == null || PauseMenu == null) return;

            GameFlowManager.Instance.TogglePauseState();
            bool isPaused = GameFlowManager.Instance.IsGamePaused;

            if (isPaused)
            {
                SetCameraLocked(true);
                PauseMenu.ShowMenu();
            }
            else
            {
                PauseMenu.HideMenu(() => SetCameraLocked(false));
            }
        }
    }

    private void SetCameraLocked(bool isLocked)
    {
        if (Camera.main != null && Camera.main.TryGetComponent(out CameraController camCtrl))
        {
            camCtrl.IsControlDisabled = isLocked;
        }
    }
}