using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class AdvancedScrollViewController : MonoBehaviour
{
    [Header("UI组件")]
    public GameObject scrollView;
    public Button toggleButton;
    public List<Button> excludeButtons;  // 点击这些按钮时不隐藏

    [Header("显示设置")]
    public bool hideOnStart = true;
    public bool hideOnClickOutside = true;
    public bool hideOnRightClick = true;
    public bool hideOnEscape = true;      // 按ESC键隐藏

    [Header("动画效果")]
    public bool useAnimation = false;
    public float animationDuration = 0.3f;

    private bool isScrollViewVisible = false;
    private CanvasGroup canvasGroup;
    private Vector3 originalScale;

    void Start()
    {
        // 获取或添加CanvasGroup用于动画
        if (useAnimation)
        {
            canvasGroup = scrollView.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = scrollView.AddComponent<CanvasGroup>();
            }
            originalScale = scrollView.transform.localScale;
        }

        // 初始状态
        if (hideOnStart)
        {
            SetScrollViewVisible(false, false);
        }
        else
        {
            SetScrollViewVisible(true, false);
        }

        // 绑定按钮
        if (toggleButton != null)
        {
            toggleButton.onClick.AddListener(ToggleScrollView);
        }

        // 添加自动排除的按钮
        if (!excludeButtons.Contains(toggleButton))
        {
            excludeButtons.Add(toggleButton);
        }

    }

    void Update()
    {
        if (!isScrollViewVisible) return;

        // 检查隐藏条件
        if (hideOnClickOutside && Input.GetMouseButtonDown(0))
        {
            CheckHideOnClick();
        }

        if (hideOnRightClick && Input.GetMouseButtonDown(1))
        {
            SetScrollViewVisible(false);
        }

        if (hideOnEscape && Input.GetKeyDown(KeyCode.Escape))
        {
            SetScrollViewVisible(false);
        }
    }

    public void ToggleScrollView()
    {
        SetScrollViewVisible(!isScrollViewVisible);
    }

    public void SetScrollViewVisible(bool visible, bool useAnim = true)
    {
        if (useAnimation && useAnim)
        {
            StartCoroutine(AnimateScrollView(visible));
        }
        else
        {
            scrollView.SetActive(visible);
            isScrollViewVisible = visible;
        }
    }

    private void CheckHideOnClick()
    {
        PointerEventData pointerData = new PointerEventData(EventSystem.current);
        pointerData.position = Input.mousePosition;

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        // 检查点击的对象
        foreach (var result in results)
        {
            GameObject clicked = result.gameObject;

            // 检查是否是排除的按钮
            foreach (Button excludeBtn in excludeButtons)
            {
                if (excludeBtn != null && (clicked == excludeBtn.gameObject ||
                    clicked.transform.IsChildOf(excludeBtn.transform)))
                {
                    return;
                }
            }

            // 检查是否在滚动视图内
            if (clicked == scrollView || clicked.transform.IsChildOf(scrollView.transform))
            {
                return;
            }
        }

        SetScrollViewVisible(false);
    }

    private System.Collections.IEnumerator AnimateScrollView(bool visible)
    {
        if (canvasGroup == null) yield break;

        float elapsed = 0f;
        Vector3 startScale = visible ? Vector3.zero : originalScale;
        Vector3 endScale = visible ? originalScale : Vector3.zero;
        float startAlpha = visible ? 0f : 1f;
        float endAlpha = visible ? 1f : 0f;

        if (visible)
        {
            scrollView.SetActive(true);
            canvasGroup.alpha = startAlpha;
            scrollView.transform.localScale = startScale;
        }

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;

            // 使用缓动函数
            t = Mathf.SmoothStep(0, 1, t);

            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
            scrollView.transform.localScale = Vector3.Lerp(startScale, endScale, t);

            yield return null;
        }

        if (!visible)
        {
            scrollView.SetActive(false);
        }

        isScrollViewVisible = visible;
    }
}