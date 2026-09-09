using UnityEngine;
using Mirror;
using System;

namespace IsometricShooter.Core
{
    public class Inventory : NetworkBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int maxSlots = 6;

        public readonly SyncList<InventorySlot> items = new SyncList<InventorySlot>();

        public event Action OnInventoryChanged;

        public override void OnStartServer()
        {
            for (int i = 0; i < maxSlots; i++)
            {
                items.Add(new InventorySlot { slotIndex = i });
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            items.Callback += OnItemsUpdated;
            OnInventoryChanged?.Invoke();
        }

        private void OnItemsUpdated(SyncList<InventorySlot>.Operation op, int index, InventorySlot oldSlot, InventorySlot newSlot)
        {
            OnInventoryChanged?.Invoke();
        }

        public bool TryAddItem(string itemId, int amount)
        {
            string uniqueId;
            return TryAddItem(itemId, amount, out uniqueId);
        }

        public bool TryAddItem(string itemId, int amount, out string addedUniqueId)
        {
            addedUniqueId = null;
            if (!isServer) return false;

            ItemData item = ItemData.GetItem(itemId);
            int stackSize = item != null ? item.StackSize : int.MaxValue;
            bool stackable = stackSize > 1;

            if (stackable)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].itemId == itemId)
                    {
                        InventorySlot slot = items[i];
                        int room = stackSize - slot.amount;
                        if (room <= 0) continue;

                        int toAdd = Mathf.Min(amount, room);
                        slot.amount += toAdd;
                        items[i] = slot;

                        addedUniqueId = slot.uniqueId;

                        amount -= toAdd;
                        if (amount <= 0) return true;
                    }
                }
            }

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].IsEmpty)
                {
                    string uid = Guid.NewGuid().ToString("N");
                    InventorySlot slot = new InventorySlot
                    {
                        itemId = itemId,
                        uniqueId = uid,
                        amount = Mathf.Min(amount, stackSize),
                        slotIndex = i
                    };
                    items[i] = slot;

                    addedUniqueId = uid;

                    amount -= slot.amount;
                    if (amount <= 0) return true;
                }
            }

            return amount <= 0;
        }

        public bool TryAddItem(ItemData item, int amount)
        {
            if (item == null) return false;
            return TryAddItem(item.itemId, amount);
        }

        public bool RemoveItem(string itemId, int amount)
        {
            if (!isServer) return false;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].itemId == itemId && items[i].amount > 0)
                {
                    InventorySlot slot = items[i];
                    int toRemove = Mathf.Min(amount, slot.amount);
                    slot.amount -= toRemove;
                    amount -= toRemove;

                    if (slot.amount <= 0)
                    {
                        slot.itemId = "";
                        slot.uniqueId = "";
                        slot.amount = 0;
                    }

                    items[i] = slot;
                    if (amount <= 0) return true;
                }
            }

            return amount <= 0;
        }

        public bool RemoveSlot(string uniqueId, int amount = 1)
        {
            if (!isServer) return false;
            if (string.IsNullOrEmpty(uniqueId)) return false;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].uniqueId != uniqueId || items[i].amount <= 0)
                    continue;

                InventorySlot slot = items[i];
                int toRemove = Mathf.Min(amount, slot.amount);
                slot.amount -= toRemove;

                if (slot.amount <= 0)
                {
                    slot.itemId = "";
                    slot.uniqueId = "";
                    slot.amount = 0;
                }

                items[i] = slot;
                return true;
            }

            return false;
        }

        public void ClearSlot(string uniqueId)
        {
            if (!isServer) return;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].uniqueId != uniqueId)
                    continue;

                InventorySlot slot = new InventorySlot
                {
                    itemId = "",
                    uniqueId = "",
                    amount = 0,
                    slotIndex = items[i].slotIndex
                };
                items[i] = slot;
                return;
            }
        }

        public InventorySlot GetSlotByUniqueId(string uniqueId)
        {
            if (string.IsNullOrEmpty(uniqueId)) return new InventorySlot();

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].uniqueId == uniqueId)
                    return items[i];
            }
            return new InventorySlot();
        }

        public int GetSlotIndexByUniqueId(string uniqueId)
        {
            if (string.IsNullOrEmpty(uniqueId)) return -1;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].uniqueId == uniqueId)
                    return i;
            }
            return -1;
        }

        public int GetItemAmount(string itemId)
        {
            int total = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].itemId == itemId)
                    total += items[i].amount;
            }
            return total;
        }

        public bool HasItem(string itemId, int minAmount = 1)
        {
            return GetItemAmount(itemId) >= minAmount;
        }

        public InventorySlot GetSlot(int index)
        {
            if (index < 0 || index >= items.Count)
                return new InventorySlot();
            return items[index];
        }
    }
}