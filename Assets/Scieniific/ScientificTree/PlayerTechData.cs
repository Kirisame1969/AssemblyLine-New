using System.Collections.Generic;

[System.Serializable]
public class PlayerTechData
{
    // 用于存档序列化的 List（因为 Unity 原生不支持序列化 HashSet）
    public List<string> UnlockedTechIDs = new List<string>();

    // 运行时驻留内存的高速 O(1) 查询器（绝对禁止在此处进行装箱操作）
    private HashSet<string> _unlockedSet = new HashSet<string>();

    public void Initialize()
    {
        _unlockedSet = new HashSet<string>(UnlockedTechIDs);
    }

    public bool IsUnlocked(string id)
    {
        return _unlockedSet.Contains(id);
    }

    public void Unlock(string id)
    {
        if (_unlockedSet.Add(id))
        {
            UnlockedTechIDs.Add(id); // 同步保存至序列化列表
        }
    }
}