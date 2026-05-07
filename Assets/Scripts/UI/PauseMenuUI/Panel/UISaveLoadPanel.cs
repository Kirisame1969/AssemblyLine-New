using UnityEngine;
using System;
using System.Collections.Generic;
using AssemblyLine.Core.Manager.SaveLoad;

public enum SaveLoadMode { Save, Load }

public class UISaveLoadPanel : MonoBehaviour
{
    [Header("动态生成配置")]
    public Transform ContentParent;     
    public GameObject SlotItemPrefab;   

    public void RefreshList(SaveLoadMode mode, Action<string, bool> onCardClicked)
    {
        // 1. 清空旧列表
        foreach (Transform child in ContentParent) 
        {
            if (child != null) Destroy(child.gameObject);
        }

        if (SlotItemPrefab == null) return;

        // 2. 如果是存档模式，强制生成第一张【新建存档】虚拟卡片
        if (mode == SaveLoadMode.Save)
        {
            GameObject newBtn = Instantiate(SlotItemPrefab, ContentParent);
            if (newBtn.TryGetComponent(out UISaveSlotItem itemView))
            {
                itemView.Setup("NewSave", "", true, onCardClicked);
            }
        }

        // 3. 生成真实的硬盘存档卡片
        if (SaveLoadManager.Instance != null)
        {
            List<SaveFileInfo> saves = SaveLoadManager.Instance.GetAvailableSaves();
            foreach (SaveFileInfo save in saves)
            {
                GameObject card = Instantiate(SlotItemPrefab, ContentParent);
                if (card.TryGetComponent(out UISaveSlotItem itemView))
                {
                    itemView.Setup(save.SlotName, save.SaveTime.ToString("yyyy-MM-dd HH:mm"), false, onCardClicked);
                }
            }
        }
    }
}