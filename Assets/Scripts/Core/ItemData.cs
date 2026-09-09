using UnityEngine;
using System.Collections.Generic;

namespace IsometricShooter.Core
{
    public enum ItemType
    {
        None,
        Weapon,
        Medkit,
        Grenade,
        Melee,
        Ammo,
        Key
    }

    public enum HoldPose
    {
        None,
        OneHanded,
        TwoHanded
    }

    public abstract class ItemData : ScriptableObject, IItem
    {
        [Header("Identity")]
        public string itemId;
        public string itemName;
        [TextArea] public string description;
        [Tooltip("Asset icon shown in UI")]
        public Sprite icon;

        [Header("Stacking")]
        [SerializeField] private int stackSize = 1;

        [Header("Type")]
        public ItemType itemType = ItemType.None;

        [Header("World Model")]
        [Tooltip("World/dropped model spawned by ItemPickup")]
        public GameObject worldModelPrefab;

        public string ItemId => itemId;
        public string ItemName => itemName;
        public ItemType ItemType => itemType;
        public int StackSize => Mathf.Max(1, stackSize);

        public virtual GameObject GetWorldModelPrefab()
        {
            return worldModelPrefab;
        }

        public virtual GameObject GetEquipModelPrefab()
        {
            return null;
        }

        public virtual HoldPose GetEquipHoldPose()
        {
            return HoldPose.None;
        }

        private static readonly Dictionary<string, ItemData> registeredItems = new Dictionary<string, ItemData>();

        public static T GetItem<T>(string id) where T : ItemData
        {
            if (string.IsNullOrEmpty(id)) return null;

            ItemData data;
            if (registeredItems.TryGetValue(id, out data))
                return data as T;

            return null;
        }

        public static ItemData GetItem(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            ItemData data;
            if (registeredItems.TryGetValue(id, out data))
                return data;

            return null;
        }

        public static void Register(ItemData item)
        {
            if (item == null || string.IsNullOrEmpty(item.itemId)) return;
            registeredItems[item.itemId] = item;
        }

        public static void Unregister(ItemData item)
        {
            if (item == null) return;

            ItemData existing;
            if (registeredItems.TryGetValue(item.itemId, out existing) && existing == item)
            {
                registeredItems.Remove(item.itemId);
            }
        }

        protected virtual void OnValidate()
        {
            Register(this);
        }

        protected virtual void OnEnable()
        {
            Register(this);
        }

        protected virtual void OnDisable()
        {
            Unregister(this);
        }
    }
}