using System.Collections.Generic;
using UnityEngine;
using AssemblyLine.Core.Configuration;

namespace AssemblyLine.Core.Manager
{
    /// <summary>
    /// 全局静态配置管理器。
    /// 负责在游戏启动时构建全资产的哈希索引，为 UI 渲染与存档反序列化提供极速检索。
    /// </summary>
    public class ConfigManager : MonoBehaviour
    {
        public static ConfigManager Instance { get; private set; }

        [Header("全局资产目录")]
        public ItemCatalog MainItemCatalog;
        public RecipeCatalog MainRecipeCatalog;
        // 你已有的 ModuleCatalog 也可以在这里接入，进行统一管理

        // 核心检索哈希表
        private Dictionary<string, ItemDefinition> _itemDict = new Dictionary<string, ItemDefinition>();
        private Dictionary<string, RecipeDefinition> _recipeDict = new Dictionary<string, RecipeDefinition>();

        private void Awake()
        {
            // 单例去重契约
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }

            InitializeIndices();
        }

        /// <summary>
        /// 提取 Catalog 数据构建 O(1) 检索字典，并执行数据安全性校验
        /// </summary>
        private void InitializeIndices()
        {
            // 1. 初始化物品字典
            if (MainItemCatalog != null)
            {
                foreach (var item in MainItemCatalog.AllItems)
                {
                    if (item == null) continue; // 防御：跳过空插槽
                    if (string.IsNullOrEmpty(item.ItemID))
                    {
                        Debug.LogError($"[ConfigManager] 发现物品未配置 ID，已跳过加载！名称: {item.name}");
                        continue;
                    }

                    if (!_itemDict.ContainsKey(item.ItemID))
                    {
                        _itemDict.Add(item.ItemID, item);
                    }
                    else
                    {
                        Debug.LogError($"[ConfigManager] 致命冲突！物品 ID 重复: {item.ItemID}");
                    }
                }
            }

            // 2. 初始化配方字典 (同理)
            if (MainRecipeCatalog != null)
            {
                foreach (var recipe in MainRecipeCatalog.AllRecipes)
                {
                    if (recipe == null) continue;
                    // 假设 RecipeDefinition 有 RecipeID 字段。如果没有，请根据你的代码调整。
                    if (!_recipeDict.ContainsKey(recipe.name)) 
                    {
                        _recipeDict.Add(recipe.name, recipe); 
                    }
                }
            }
        }

        // ==========================================
        // 核心对外接口
        // ==========================================

        /// <summary>
        /// [存档还原专用] 通过 ID 快速获取物品图纸实例
        /// </summary>
        public ItemDefinition GetItem(string itemID)
        {
            if (string.IsNullOrEmpty(itemID)) return null;
            _itemDict.TryGetValue(itemID, out var item);
            return item;
        }

        /// <summary>
        /// [UI 与科研系统专用] 获取所有已注册的物品。
        /// 预留 includeLocked 参数供未来科技树系统接入。
        /// </summary>
        public List<ItemDefinition> GetAllItems(bool includeLocked = true)
        {
            // 目前默认返回全部。未来可以在此进行遍历过滤
            return MainItemCatalog != null ? MainItemCatalog.AllItems : new List<ItemDefinition>();
        }
    }
}