using AssemblyLine.Data.SaveData;
// 升级版实体物品数据

public class ItemData
{
    // 【新增】：指向配置表的引用（这个物品到底是个啥？）
    public ItemDefinition Definition; 
    public GridCell CurrentCell;
    public float Progress;

    // 强制要求：现在凭空生成一个物品，必须告诉系统它是什么！
    public ItemData(ItemDefinition def)
    {
        Definition = def;
    }
    
    // ========== 【在此处新增以下方法】 ==========
    /// <summary>
    /// 将运行时物品数据降维为可序列化的 DTO。
    /// </summary>
    public ItemSaveData ToSaveData()
    {
        return new ItemSaveData
        {
            // 【核心修复】：必须使用 ItemID 而不是 name！与 ConfigManager 保持绝对一致！
                ItemID = this.Definition != null ? this.Definition.ItemID : null, 
                Progress = this.Progress
        };
    }

}
