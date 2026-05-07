using UnityEngine;
using DG.Tweening;

public class PauseMenuWindow : MonoBehaviour
{
    [Header("UI 引用 - 主干")]
    public RectTransform TopBlackBar;
    public RectTransform BottomBlackBar;
    public CanvasGroup SidebarCanvasGroup;
    public RectTransform SidebarTransform;

    [Header("UI 引用 - 子面板")]
    public RectTransform SavePanel;
    public RectTransform LoadPanel;
    public RectTransform SettingsPanel;
    
    [Header("动效参数")]
    public float AnimationDuration = 0.4f;
    public float CameraZoomMultiplier = 0.85f;
    public float ParallaxStrength = 0.5f;

    // 状态缓存
    private Camera _mainCamera;
    private float _originalOrthoSize;
    private Vector3 _originalCamPos;
    private bool _isMenuOpen = false;
    private Sequence _transitionSequence;

    // 【新增】：将位置参数暴露给面板，告别写死的魔法数字
    public float SidebarTargetPosX = 100f;  // 展开时距离左边缘的距离
    public float SidebarHiddenPosX = -600f; // 隐藏时藏在屏幕外的距离（稍微加大一点确保完全藏住）
    
    // 当前激活的子面板
    private RectTransform _currentActivePanel = null;

    private void Awake()
    {
        _mainCamera = Camera.main;
        
        // 初始化主干 UI 状态
        TopBlackBar.anchoredPosition = new Vector2(0, TopBlackBar.rect.height);
        BottomBlackBar.anchoredPosition = new Vector2(0, -BottomBlackBar.rect.height);
        SidebarCanvasGroup.alpha = 0f;
        // 【修改】：使用变量初始化隐藏位置
        SidebarTransform.anchoredPosition = new Vector2(SidebarHiddenPosX, 0);
        
        // 初始化所有子面板状态（隐藏在右侧屏幕外）
        InitSubPanel(SavePanel);
        InitSubPanel(LoadPanel);
        InitSubPanel(SettingsPanel);

        gameObject.SetActive(false);
    }

    private void InitSubPanel(RectTransform panel)
    {
        if (panel == null) return;
        panel.anchoredPosition = new Vector2(800f, 0); // 藏于右侧
        if (panel.TryGetComponent(out CanvasGroup cg))
        {
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
        }
        panel.gameObject.SetActive(false);
    }

    // ==========================================
    // 子面板排他性路由逻辑
    // ==========================================
    public void OpenSubPanel(RectTransform targetPanel)
    {
        if (targetPanel == null || targetPanel == _currentActivePanel) return;

        // 1. 如果当前有打开的面板，先让它退场 (向右滑出并淡出)
        if (_currentActivePanel != null)
        {
            RectTransform oldPanel = _currentActivePanel;
            if (oldPanel.TryGetComponent(out CanvasGroup oldCg))
            {
                oldCg.blocksRaycasts = false;
                oldCg.DOFade(0f, 0.2f).SetUpdate(true);
            }
            oldPanel.DOAnchorPosX(800f, 0.3f).SetEase(Ease.InCubic).SetUpdate(true)
                .OnComplete(() => oldPanel.gameObject.SetActive(false));
        }

        // 2. 目标面板入场 (从右侧滑入中心并淡入)
        targetPanel.gameObject.SetActive(true);
        if (targetPanel.TryGetComponent(out CanvasGroup newCg))
        {
            newCg.DOFade(1f, 0.3f).SetUpdate(true);
            newCg.blocksRaycasts = true;
        }
        targetPanel.DOAnchorPosX(0f, 0.3f).SetEase(Ease.OutCubic).SetUpdate(true);

        _currentActivePanel = targetPanel;
    }

    public void ShowMenu()
    {
        gameObject.SetActive(true);
        _isMenuOpen = true;

        if (_mainCamera != null)
        {
            _originalOrthoSize = _mainCamera.orthographicSize;
            _originalCamPos = _mainCamera.transform.position;
        }

        _transitionSequence?.Kill();
        _transitionSequence = DOTween.Sequence().SetUpdate(true);

        if (_mainCamera != null)
            _transitionSequence.Join(_mainCamera.DOOrthoSize(_originalOrthoSize * CameraZoomMultiplier, AnimationDuration).SetEase(Ease.OutCubic));

        _transitionSequence.Join(TopBlackBar.DOAnchorPosY(0, AnimationDuration).SetEase(Ease.OutCubic));
        _transitionSequence.Join(BottomBlackBar.DOAnchorPosY(0, AnimationDuration).SetEase(Ease.OutCubic));
        // 【修改】：滑入动画的目标值改为 SidebarTargetPosX
        _transitionSequence.Join(SidebarTransform.DOAnchorPosX(SidebarTargetPosX, AnimationDuration).SetEase(Ease.OutCubic));
        _transitionSequence.Join(SidebarCanvasGroup.DOFade(1f, AnimationDuration));
    }

    public void HideMenu(System.Action onComplete = null)
    {
        _isMenuOpen = false;

        // 强制收回当前打开的子面板
        if (_currentActivePanel != null)
        {
            if (_currentActivePanel.TryGetComponent(out CanvasGroup cg)) cg.DOFade(0f, AnimationDuration).SetUpdate(true);
            _currentActivePanel.DOAnchorPosX(800f, AnimationDuration).SetEase(Ease.InCubic).SetUpdate(true);
            _currentActivePanel = null;
        }

        _transitionSequence?.Kill();
        _transitionSequence = DOTween.Sequence().SetUpdate(true);

        if (_mainCamera != null)
        {
            _transitionSequence.Join(_mainCamera.DOOrthoSize(_originalOrthoSize, AnimationDuration).SetEase(Ease.OutCubic));
            _transitionSequence.Join(_mainCamera.transform.DOMove(_originalCamPos, AnimationDuration).SetEase(Ease.OutCubic));
        }

        _transitionSequence.Join(TopBlackBar.DOAnchorPosY(TopBlackBar.rect.height, AnimationDuration).SetEase(Ease.InCubic));
        _transitionSequence.Join(BottomBlackBar.DOAnchorPosY(-BottomBlackBar.rect.height, AnimationDuration).SetEase(Ease.InCubic));
        // 【修改】：退场动画的目标值改为 SidebarHiddenPosX
        _transitionSequence.Join(SidebarTransform.DOAnchorPosX(SidebarHiddenPosX, AnimationDuration).SetEase(Ease.InCubic));
        _transitionSequence.Join(SidebarCanvasGroup.DOFade(0f, AnimationDuration).SetEase(Ease.InCubic));

        _transitionSequence.OnComplete(() => {
            gameObject.SetActive(false);
            onComplete?.Invoke();
        });
    }

    private void Update()
    {
        if (!_isMenuOpen || _mainCamera == null) return;
        Vector2 mousePos = Input.mousePosition;
        Vector2 normalizedMousePos = new Vector2((mousePos.x / Screen.width) - 0.5f, (mousePos.y / Screen.height) - 0.5f);
        Vector3 targetPos = _originalCamPos + new Vector3(normalizedMousePos.x, normalizedMousePos.y, 0f) * ParallaxStrength;
        _mainCamera.transform.position = Vector3.Lerp(_mainCamera.transform.position, targetPos, Time.unscaledDeltaTime * 5f);
    }
}