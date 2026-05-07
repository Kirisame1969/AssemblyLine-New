using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class TechNodeClickHandler : MonoBehaviour, IPointerClickHandler
{
    private const float DOUBLE_CLICK_TIME = 0.25f; // 双击判定阈值

    private string _nodeId;
    private Action<string> _onSingleClick;
    private Action<string> _onDoubleClick;

    private Coroutine _singleClickTimer;

    public void Initialize(string nodeId, Action<string> onSingleClick, Action<string> onDoubleClick)
    {
        _nodeId = nodeId;
        _onSingleClick = onSingleClick;
        _onDoubleClick = onDoubleClick;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 仅响应左键
        if (eventData.button != PointerEventData.InputButton.Left) return;

        if (eventData.clickCount == 1)
        {
            // 第一次点击：开启计时器，不立刻触发单击逻辑
            _singleClickTimer = StartCoroutine(SingleClickRoutine());
        }
        else if (eventData.clickCount == 2)
        {
            // 第二次点击：在阈值内发生，判定为双击。打断单击计时器！
            if (_singleClickTimer != null)
            {
                StopCoroutine(_singleClickTimer);
                _singleClickTimer = null;
            }
            _onDoubleClick?.Invoke(_nodeId);
        }
    }

    private IEnumerator SingleClickRoutine()
    {
        // 挂起 0.25 秒
        yield return new WaitForSecondsRealtime(DOUBLE_CLICK_TIME);

        // 如果没人打断我，说明是纯粹的单击
        _onSingleClick?.Invoke(_nodeId);
        _singleClickTimer = null;
    }
}