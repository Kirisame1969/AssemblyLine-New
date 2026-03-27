using System.Collections.Generic; // 使用 List 需要这个命名空间
using UnityEngine;
using TMPro; // 必须引入 TextMeshPro 命名空间

public class DropdownAutoFiller : MonoBehaviour
{
    [Header("拖入你的下拉列表")]
    public TMP_Dropdown myDropdown;

    void Start()
    {
        // 游戏开始时自动执行填充
        PopulateDropdown();

        // （可选）监听玩家选择了哪一个选项
        myDropdown.onValueChanged.AddListener(OnDropdownValueChanged);
    }

    void PopulateDropdown()
    {
        // 1. 清空下拉列表里自带的默认选项（比如 Option A, Option B）
        myDropdown.ClearOptions();

        // 2. 创建一个字符串列表，准备你要塞进去的选项名称
        // 这里以你刚才问的“多语言切换”为例
        List<string> languageOptions = new List<string>
        {
            "简体中文", // 索引为 0
            "English",  // 索引为 1
            "日本語"    // 索引为 2
        };

        // 3. 将准备好的列表一次性塞进下拉列表中！
        myDropdown.AddOptions(languageOptions);
    }

    // 当玩家点击并改变了下拉列表的选项时，会自动调用这个方法
    void OnDropdownValueChanged(int index)
    {
        Debug.Log("玩家选择了第 " + index + " 个选项");

        // 💡 联动上一问：如果你把这里和之前的语言管理器结合
        // 就可以直接调用：languageManager.ChangeLanguage(index);
    }
}