using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System;

public class UIConfirmationPopup : MonoBehaviour
{
    [Header("UI 引用")]
    public TextMeshProUGUI TxtTitle;
    public TMP_InputField InputSaveName;
    public Button BtnConfirm;
    public Button BtnCancel;
    public CanvasGroup MainCanvasGroup;

    private Action<string> _onConfirm;
    private Action _onCancel;

    private void Awake()
    {
        // 强制清空可能在 Inspector 中错误绑定的事件，完全由纯代码接管
        if (BtnConfirm != null) {
            BtnConfirm.onClick.RemoveAllListeners();
            BtnConfirm.onClick.AddListener(Confirm);
        }
        if (BtnCancel != null) {
            BtnCancel.onClick.RemoveAllListeners();
            BtnCancel.onClick.AddListener(Cancel);
        }
        
        MainCanvasGroup.alpha = 0;
        MainCanvasGroup.blocksRaycasts = false;
        gameObject.SetActive(false);
    }

    public void Show(string title, string defaultInput, Action<string> onConfirm, Action onCancel = null)
    {
        gameObject.SetActive(true);
        TxtTitle.text = title;
        _onConfirm = onConfirm;
        _onCancel = onCancel;

        if (InputSaveName != null)
        {
            // 只有传入了默认文本（新建存档时），才激活输入框
            InputSaveName.gameObject.SetActive(defaultInput != null);
            if (defaultInput != null) InputSaveName.text = defaultInput;
        }

        MainCanvasGroup.DOFade(1f, 0.2f).SetUpdate(true);
        transform.localScale = Vector3.one * 0.8f;
        transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);
        MainCanvasGroup.blocksRaycasts = true;
    }

    private void Confirm()
    {
        string result = "";
        if (InputSaveName != null && InputSaveName.gameObject.activeSelf)
        {
            // 【核心修复】：剔除首尾空格，并干掉 TMP 独有的隐藏 \u200B 字符！
            result = InputSaveName.text.Trim().Replace("\u200B", "");
        }
        
        _onConfirm?.Invoke(result);
        Close();
    }

    private void Cancel()
    {
        _onCancel?.Invoke();
        Close();
    }

    public void Close()
    {
        MainCanvasGroup.blocksRaycasts = false;
        MainCanvasGroup.DOFade(0f, 0.2f).SetUpdate(true).OnComplete(() => gameObject.SetActive(false));
    }
}