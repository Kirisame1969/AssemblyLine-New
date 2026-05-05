using System;
using System.Collections.Generic;
using UnityEngine;
using AssemblyLine.Data.SaveData;

public class StripData
{
    public string StripID;
    public float MoveSpeed = 1.0f; // 整个条带共享的速度
    
    // 记录这个条带包含了哪些网格坐标
    public List<Vector2Int> Cells = new List<Vector2Int>(); 

    // 【新增】该条带的专属颜色
    public Color StripColor;

    public StripData()
    {
        // 生成一个唯一ID，方便调试时区分不同的条带
        StripID = Guid.NewGuid().ToString().Substring(0, 5); 

        // 【新增】随机生成一个高饱和度、高亮度的颜色，确保在深色背景下也能看清
        StripColor = UnityEngine.Random.ColorHSV(0f, 1f, 0.8f, 1f, 0.8f, 1f);
    }
    // ==========================================
    // 存档扩展方法
    // ==========================================
    public StripSaveData ToSaveData()
    {
        return new StripSaveData
        {
            StripID = this.StripID,
            MoveSpeed = this.MoveSpeed,
            Cells = new List<Vector2Int>(this.Cells),
            StripColorHex = ColorUtility.ToHtmlStringRGBA(this.StripColor)
        };
    }

    public static StripData CreateFromSaveData(StripSaveData data)
    {
        var strip = new StripData();
        strip.StripID = data.StripID;
        strip.MoveSpeed = data.MoveSpeed;
        if (data.Cells != null) strip.Cells = new List<Vector2Int>(data.Cells);
        
        // 恢复颜色
        if (!string.IsNullOrEmpty(data.StripColorHex) && ColorUtility.TryParseHtmlString("#" + data.StripColorHex, out Color color))
        {
            strip.StripColor = color;
        }
        return strip;
    }
}