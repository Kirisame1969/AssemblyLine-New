using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIRaycastDebugger : MonoBehaviour
{
    void Update()
    {
        // 当按下鼠标左键时触发扫描
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current == null) return;

            // 构造一个模拟鼠标当前位置的事件数据
            PointerEventData pointerData = new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition
            };

            // 发射 UI 射线，收集所有被击中的对象
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            Debug.Log($"========== 鼠标点击射线测试 (共穿透 {results.Count} 个 UI 元素) ==========");
            for (int i = 0; i < results.Count; i++)
            {
                // 索引 0 是渲染在最顶层、最先接收到点击的元素
                Debug.Log($"[阻挡层级 {i}] 击中实体: {results[i].gameObject.name}");
            }
        }
    }
}