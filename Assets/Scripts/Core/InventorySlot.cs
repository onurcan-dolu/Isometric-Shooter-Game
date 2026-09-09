using System;

namespace IsometricShooter.Core
{
    [Serializable]
    public struct InventorySlot
    {
        public string itemId;
        public string uniqueId;
        public int amount;
        public int slotIndex;

        public bool IsEmpty => string.IsNullOrEmpty(itemId);

        public ItemData ResolveItem() => ItemData.GetItem(itemId);
    }
}