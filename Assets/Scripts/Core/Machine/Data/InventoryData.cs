using System;
using System.Collections.Generic;

namespace AssemblyLine.Data.Machine
{
    /// <summary>
    /// 库存槽位数据实体。
    /// 负责记录单一类型物品的堆叠状态。
    /// </summary>
    [Serializable]
    public class InventorySlot
    {
        /// <summary>
        /// 槽位存储的物品图纸（为空代表此槽位空闲）。
        /// </summary>
        public ItemDefinition ItemType;
        
        /// <summary>
        /// 当前物品数量。
        /// </summary>
        public int Count;
        
        /// <summary>
        /// 槽位是否为空。
        /// </summary>
        public bool IsEmpty => ItemType == null || Count <= 0;

        public void Clear()
        {
            ItemType = null;
            Count = 0;
        }
    }

    /// <summary>
    /// 纯数据驱动的机器库存系统。
    /// 封装基于数组的扁平化槽位运算，供控制层安全调用。
    /// </summary>
    [Serializable]
    public class InventoryData
    {
        /// <summary>
        /// 扁平化的槽位数组。使用数组而非 List 以保障 Tick 遍历时的极致性能。
        /// </summary>
        public InventorySlot[] Slots;

        /// <summary>
        /// 每个槽位的默认最大堆叠上限（可后期通过高级扩展模块修改）。
        /// </summary>
        public int MaxStackPerSlot = 4;

        /// <summary>
        /// 初始化指定容量的库存系统。
        /// </summary>
        /// <param name="capacity">初始槽位数量</param>
        public InventoryData(int capacity)
        {
            Slots = new InventorySlot[capacity];
            for (int i = 0; i < capacity; i++)
            {
                Slots[i] = new InventorySlot();
            }
        }

        /// <summary>
        /// 执行库存“碎片整理”与“同类堆叠合并”。
        /// 1. 尝试将后方槽位的物品向前合并到未满的同类堆叠中。
        /// 2. 消除中间的空槽位，将所有物品整体向数组头部（左侧）紧凑。
        /// </summary>
        public void CompressSlots()
        {
            // 1. 同类合并堆叠
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].IsEmpty) continue;
                
                for (int j = i + 1; j < Slots.Length; j++)
                {
                    if (Slots[j].IsEmpty) continue;
                    
                    // 使用唯一标识符 ItemID 判断是否为同类物品
                    if (Slots[i].ItemType.ItemID == Slots[j].ItemType.ItemID)
                    {
                        int spaceLeft = MaxStackPerSlot - Slots[i].Count;
                        if (spaceLeft > 0)
                        {
                            int transferAmount = Math.Min(spaceLeft, Slots[j].Count); // 修正为 System.Math
                            Slots[i].Count += transferAmount;
                            Slots[j].Count -= transferAmount;
                            
                            if (Slots[j].Count <= 0) Slots[j].Clear();
                        }
                    }
                }
            }

            // 2. 空隙消除（向左紧凑化）
            int insertPos = 0;
            for (int i = 0; i < Slots.Length; i++)
            {
                if (!Slots[i].IsEmpty)
                {
                    if (i != insertPos)
                    {
                        // 覆盖拷贝数据
                        Slots[insertPos].ItemType = Slots[i].ItemType;
                        Slots[insertPos].Count = Slots[i].Count;
                        Slots[i].Clear();
                    }
                    insertPos++;
                }
            }
        }

        /// <summary>
        /// 动态重置库存容量，并返回因缩容而溢出且无法保全的物品。
        /// </summary>
        public List<InventorySlot> Resize(int newCapacity)
        {
            List<InventorySlot> spilledItems = new List<InventorySlot>();
            if (newCapacity == Slots.Length) return spilledItems;

            // 若发生缩容，必须先进行极致压缩，最大化利用前部保留空间
            if (newCapacity < Slots.Length)
            {
                CompressSlots();
                
                // 提取被切断在 newCapacity 之外的物品
                for (int i = newCapacity; i < Slots.Length; i++)
                {
                    if (!Slots[i].IsEmpty)
                    {
                        spilledItems.Add(new InventorySlot { ItemType = Slots[i].ItemType, Count = Slots[i].Count });
                        Slots[i].Clear();
                    }
                }
            }

            // 执行数组内存重建
            InventorySlot[] newSlots = new InventorySlot[newCapacity];
            for (int i = 0; i < newCapacity; i++)
            {
                if (i < Slots.Length) newSlots[i] = Slots[i];
                else newSlots[i] = new InventorySlot(); // 扩容部分初始化
            }
            
            Slots = newSlots;
            return spilledItems;
        }

        /// <summary>
        /// 尝试将一个物品存入库存。
        /// 优先寻找未满的同类堆叠，其次寻找空槽位。
        /// </summary>
        public bool TryAdd(ItemDefinition itemDef)
        {
            if (itemDef == null) return false;

            // 1. 优先尝试堆叠合并
            for (int i = 0; i < Slots.Length; i++)
            {
                if (!Slots[i].IsEmpty && Slots[i].ItemType.ItemID == itemDef.ItemID)
                {
                    if (Slots[i].Count < MaxStackPerSlot)
                    {
                        Slots[i].Count++;
                        return true;
                    }
                }
            }

            // 2. 若无同类或堆叠已满，寻找空槽位
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].IsEmpty)
                {
                    Slots[i].ItemType = itemDef;
                    Slots[i].Count = 1;
                    return true;
                }
            }

            return false; // 库存已满
        }

        /// <summary>
        /// 尝试根据输出端口规则，从库存中抽取一个物品。
        /// 按顺序遍历，提取出第一个符合过滤规则的物品。
        /// </summary>
        public bool TryTake(PortRuleConfig rules, out ItemDefinition takenItem)
        {
            takenItem = null;
            for (int i = 0; i < Slots.Length; i++)
            {
                // 校验：槽位非空 且 物品满足该端口的白名单规则
                if (!Slots[i].IsEmpty && rules.IsAllowed(Slots[i].ItemType))
                {
                    takenItem = Slots[i].ItemType;
                    Slots[i].Count--; // 执行扣减
                    
                    if (Slots[i].Count <= 0)
                    {
                        Slots[i].Clear(); // 腾出空槽位
                    }
                    return true;
                }
            }
            return false; // 无符合规则的物品可取
        }

        /// <summary>
        /// 盘点库存中指定物品的总数量。
        /// </summary>
        public int GetItemCount(string itemID)
        {
            int count = 0;
            for (int i = 0; i < Slots.Length; i++)
            {
                if (!Slots[i].IsEmpty && Slots[i].ItemType.ItemID == itemID)
                {
                    count += Slots[i].Count;
                }
            }
            return count;
        }

        /// <summary>
        /// 从库存中定向定量抽取指定物品，并返回实际成功抽取的数量。
        /// </summary>
        public int ExtractItem(string itemID, int amountRequired)
        {
            int extracted = 0;
            for (int i = 0; i < Slots.Length; i++)
            {
                if (!Slots[i].IsEmpty && Slots[i].ItemType.ItemID == itemID)
                {
                    // 计算本次能从该槽位拿走多少
                    int take = Math.Min(amountRequired - extracted, Slots[i].Count);
                    
                    Slots[i].Count -= take;
                    extracted += take;
                    
                    if (Slots[i].Count <= 0) Slots[i].Clear();
                    
                    // 如果已经凑齐所需数量，立刻终止遍历
                    if (extracted >= amountRequired) break;
                }
            }
            return extracted;
        }

        // 追加在 AssemblyLine.Data.Machine.InventoryData 类中：

        /// <summary>
        /// 处理两个槽位之间的安全交互（拖拽操作的底层响应）。
        /// 若物品相同则尝试合并堆叠；若物品不同则互换位置。
        /// </summary>
        public void InteractSlots(int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex) return;
            // 防御性编程：数组越界防护
            if (fromIndex < 0 || fromIndex >= Slots.Length || toIndex < 0 || toIndex >= Slots.Length) return;

            InventorySlot source = Slots[fromIndex];
            InventorySlot target = Slots[toIndex];

            // 如果源槽位为空，不产生任何交互
            if (source.IsEmpty) return;

            // 分支 1：目标为空，或异类物品 -> 互相交换数据
            if (target.IsEmpty || target.ItemType.ItemID != source.ItemType.ItemID)
            {
                ItemDefinition tempType = target.ItemType;
                int tempCount = target.Count;

                target.ItemType = source.ItemType;
                target.Count = source.Count;

                source.ItemType = tempType;
                source.Count = tempCount;
            }
            // 分支 2：同类物品 -> 堆叠合并
            else
            {
                int spaceLeft = MaxStackPerSlot - target.Count;
                if (spaceLeft > 0)
                {
                    int transferAmount = Math.Min(spaceLeft, source.Count);
                    target.Count += transferAmount;
                    source.Count -= transferAmount;
                    
                    // 若源槽位被搬空，彻底清理脏数据
                    if (source.Count <= 0) source.Clear();
                }
            }
        }
    }
}