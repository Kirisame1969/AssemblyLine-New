// 新建文件：FloatingTextVisual.cs
using UnityEngine;
using TMPro;

public class FloatingTextVisual : MonoBehaviour
{
    public TMP_Text FloatingText;
    
    private float _lifetime = 1.5f;   // 存活时间
    private float _timer = 0f;
    private float _floatSpeed = 1.5f; // 向上漂移的速度
    private Color _initialColor;

    public void Init(string text, Color color)
    {
        if (FloatingText == null) FloatingText = GetComponent<TMP_Text>();
        
        FloatingText.text = text;
        FloatingText.color = color;
        _initialColor = color;
        _timer = 0f;
    }

    private void Update()
    {
        _timer += Time.deltaTime;

        // 1. 向上漂移
        transform.Translate(Vector3.up * _floatSpeed * Time.deltaTime);

        // 2. 透明度淡出 (后半段开始变透明)
        float alphaProgress = _timer / _lifetime;
        Color newColor = _initialColor;
        newColor.a = Mathf.Lerp(1f, 0f, alphaProgress * alphaProgress); // 曲线淡出
        FloatingText.color = newColor;

        // 3. 寿命结束自动销毁
        if (_timer >= _lifetime)
        {
            Destroy(gameObject);
        }
    }
}