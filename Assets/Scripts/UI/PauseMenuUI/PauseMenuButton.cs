using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;
using System;

public class PauseMenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("UI 引用")]
    public Image LeftBar;               // 左侧细黑条
    public TextMeshProUGUI ButtonText;  // 按钮文字
    public Image GradientFill;          // 渐变背景层 (需设置 Image.Type 为 Filled, Method 为 Horizontal)
    public Image BaseBackground;        // 底层基础背景 (灰白色)

    [Header("颜色配置")]
    public Color NormalBarColor = Color.black;
    public Color HoverBarColor = Color.white;
    public Color NormalTextColor = Color.black;
    public Color HoverTextColor = Color.white;

    // 对外暴露的点击事件委托
    public Action OnClickAction;

    private Tween _fillTween;

    private void Awake()
    {
        // 确保渐变层初始处于清空状态
        if (GradientFill != null)
        {
            GradientFill.fillAmount = 0f;
            
            // 【核心修复】：读取原有颜色，仅修改透明度为0，保留 Inspector 中设置的蓝色！
            Color originalColor = GradientFill.color;
            originalColor.a = 0f;
            GradientFill.color = originalColor;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 左侧条与文字变色
        LeftBar.DOColor(HoverBarColor, 0.2f).SetUpdate(true);
        ButtonText.DOColor(HoverTextColor, 0.2f).SetUpdate(true);

        // 渐变背景显现
        GradientFill.DOFade(1f, 0.2f).SetUpdate(true);
        GradientFill.DOFillAmount(1f, 0.3f).SetEase(Ease.OutQuint).SetUpdate(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        LeftBar.DOColor(NormalBarColor, 0.2f).SetUpdate(true);
        ButtonText.DOColor(NormalTextColor, 0.2f).SetUpdate(true);

        GradientFill.DOFade(0f, 0.2f).SetUpdate(true);
        GradientFill.DOFillAmount(0f, 0.3f).SetUpdate(true);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 点击动效：左侧蓝条迅速跳动回弹
        _fillTween?.Kill();
        GradientFill.fillAmount = 1f;
        _fillTween = GradientFill.DOFillAmount(0f, 0.25f).SetEase(Ease.InCubic).SetUpdate(true).OnComplete(() => 
        {
            // 恢复 Hover 状态的填充
            if (EventSystem.current.currentSelectedGameObject == gameObject || RectTransformUtility.RectangleContainsScreenPoint(GetComponent<RectTransform>(), Input.mousePosition))
            {
                GradientFill.DOFillAmount(1f, 0.2f).SetUpdate(true);
            }
        });

        // 触发外部绑定的业务逻辑
        OnClickAction?.Invoke();
    }
}