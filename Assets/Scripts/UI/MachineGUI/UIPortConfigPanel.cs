using System.Collections.Generic;
using UnityEngine;
using AssemblyLine.Data.Machine;
using AssemblyLine.Core.Manager;

namespace AssemblyLine.UI
{
    /// <summary>
    /// 端口配置界面控制器。
    /// 初始化时动态读取 ConfigManager 中的全资产，利用对象池避免反复实例化的 GC。
    /// </summary>
    public class UIPortConfigPanel : MonoBehaviour
    {
        [Header("UI 绑定")]
        public GameObject PanelRoot;          // 悬浮窗根节点
        public Transform IconGridContainer;   // 挂载 GridLayoutGroup 的容器
        public GameObject UIPortFilterIconPrefab; // 上方编写的图标预制体

        private PortRuleConfig _currentRules;
        private Dictionary<string, UIPortFilterIcon> _spawnedIcons = new Dictionary<string, UIPortFilterIcon>();
        private bool _isInitialized = false;

        private void Awake()
        {
            if (PanelRoot != null) PanelRoot.SetActive(false);
        }

        /// <summary>
        /// 开启面板并绑定指定端口的规则数据。
        /// </summary>
        public void OpenPanel(PortRuleConfig rules)
        {
            if (rules == null) return;

            _currentRules = rules;
            PanelRoot.SetActive(true);

            if (!_isInitialized)
            {
                GenerateIconPool();
                _isInitialized = true;
            }

            RefreshVisuals();
        }

        public void ClosePanel()
        {
            PanelRoot.SetActive(false);
            _currentRules = null;
        }

        private void GenerateIconPool()
        {
            if (ConfigManager.Instance == null) return;

            // 从全局资产字典获取所有物品 (预留了未来 includeLocked 参数)
            var allItems = ConfigManager.Instance.GetAllItems();

            foreach (var item in allItems)
            {
                if (item == null || string.IsNullOrEmpty(item.ItemID)) continue;

                GameObject obj = Instantiate(UIPortFilterIconPrefab, IconGridContainer);
                UIPortFilterIcon iconScript = obj.GetComponent<UIPortFilterIcon>();
                
                iconScript.Init(item, this);
                _spawnedIcons.Add(item.ItemID, iconScript);
            }
        }

        /// <summary>
        /// 刷新所有图标的高亮状态
        /// </summary>
        private void RefreshVisuals()
        {
            if (_currentRules == null) return;

            foreach (var kvp in _spawnedIcons)
            {
                bool isAllowed = _currentRules.Whitelist.Contains(kvp.Key);
                kvp.Value.SetHighlight(isAllowed);
            }
        }

        /// <summary>
        /// 响应图标点击：向底层 HashSet 写入或移除规则
        /// </summary>
        public void ToggleItemRule(string itemID)
        {
            if (_currentRules == null) return;

            // MVC 事务：修改模型层数据
            if (_currentRules.Whitelist.Contains(itemID))
            {
                _currentRules.Whitelist.Remove(itemID);
            }
            else
            {
                _currentRules.Whitelist.Add(itemID);
            }

            // 触发视图层刷新
            RefreshVisuals();
        }
    }
}