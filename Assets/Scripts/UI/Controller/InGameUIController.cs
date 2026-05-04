using UnityEngine;
using AssemblyLine.Core.Manager.GameFlow; // 引用全局总线
using AssemblyLine.Core.Manager.SaveLoad;

public class InGameUIController : MonoBehaviour
{
    [Header("暂停菜单引用")]
    public PauseMenuWindow PauseMenu;
    public PauseMenuButton BtnSave;
    public PauseMenuButton BtnLoad;
    public PauseMenuButton BtnSettings;
    public PauseMenuButton BtnMainMenu;

    private void Start()
    {
        // 1. 在场景层执行 UI 业务绑定
        if (BtnSave != null) BtnSave.OnClickAction = () => SaveLoadManager.Instance.SaveGame("AutoSave");
        
        if (BtnLoad != null) BtnLoad.OnClickAction = () => {
            // 打开子面板（代码已在 PauseMenuWindow 中实现）
            PauseMenu.OpenSubPanel(PauseMenu.LoadPanel); 
        };

        if (BtnMainMenu != null) BtnMainMenu.OnClickAction = () => {
            // 先通过总线切回运行状态（否则主菜单可能也是静音/停止的）
            GameFlowManager.Instance.TogglePauseState();
            PauseMenu.HideMenu();
            GameFlowManager.Instance.ReturnToMainMenu();
        };
    }

    private void Update()
    {
        // 2. 监听按键：这是场景级的行为
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // 向总线申请切换状态
            GameFlowManager.Instance.TogglePauseState();
            
            // 根据切换后的状态，指挥 UI 表现
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