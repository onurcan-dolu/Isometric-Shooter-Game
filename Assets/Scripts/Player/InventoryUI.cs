using UnityEngine;
using IsometricShooter.Core;

namespace IsometricShooter.Player
{
    public class InventoryUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Inventory inventory;
        [SerializeField] private WeaponController weaponController;

        [Header("Layout")]
        [SerializeField] private Transform slotContainer;

        private InventorySlotUI[] slots;

        private int selectedSlot = -1;

        private void Awake()
        {
            ResolveSlots();
        }

        private void OnEnable()
        {
            if (inventory != null)
            {
                inventory.OnInventoryChanged += RefreshAll;
            }

            if (weaponController != null)
            {
                weaponController.OnItemChanged += OnEquippedChanged;
            }

            RefreshAll();
        }

        private void OnDisable()
        {
            if (inventory != null)
            {
                inventory.OnInventoryChanged -= RefreshAll;
            }

            if (weaponController != null)
            {
                weaponController.OnItemChanged -= OnEquippedChanged;
            }
        }

        private void Start()
        {
            if (inventory == null)
                inventory = GetComponentInParent<Inventory>();

            if (weaponController == null)
                weaponController = GetComponentInParent<WeaponController>();

            if (slots == null || slots.Length == 0)
                ResolveSlots();

            OnEnable();
        }

        private void Update()
        {
            if (inventory == null || !inventory.isLocalPlayer)
                return;

            if (slots == null) return;

            for (int i = 0; i < slots.Length; i++)
            {
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)))
                {
                    SelectSlot(i);
                    break;
                }
            }

            if (Input.GetKeyDown(KeyCode.G))
            {
                DropSelectedSlot();
            }
        }

        private void ResolveSlots()
        {
            if (slotContainer == null)
                slotContainer = transform;

            slots = slotContainer.GetComponentsInChildren<InventorySlotUI>(true);

            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].SetKeyNumber(i + 1);
            }
        }

        private void RefreshAll()
        {
            if (inventory == null || slots == null)
                return;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                    continue;

                InventorySlot slot = inventory.GetSlot(i);
                slots[i].RefreshSlot(slot);
                slots[i].SetHighlighted(i == selectedSlot);
            }
        }

        private void OnEquippedChanged()
        {
            string equippedId = weaponController != null ? weaponController.EquippedUniqueId : string.Empty;

            int index = -1;
            if (!string.IsNullOrEmpty(equippedId) && inventory != null && slots != null)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    InventorySlot slot = inventory.GetSlot(i);
                    if (!slot.IsEmpty && slot.uniqueId == equippedId)
                    {
                        index = i;
                        break;
                    }
                }
            }

            selectedSlot = index;
            RefreshAll();
        }

        private void DropSelectedSlot()
        {
            if (inventory == null || weaponController == null)
                return;

            if (selectedSlot < 0 || selectedSlot >= slots.Length)
                return;

            InventorySlot slot = inventory.GetSlot(selectedSlot);
            if (slot.IsEmpty || string.IsNullOrEmpty(slot.uniqueId))
                return;

            weaponController.CmdDropItem(slot.uniqueId);

            if (weaponController.EquippedUniqueId == slot.uniqueId)
            {
                selectedSlot = -1;
            }

            RefreshAll();
        }

        private void SelectSlot(int index)
        {
            if (inventory == null || weaponController == null)
                return;

            if (index < 0 || index >= slots.Length)
                return;

            InventorySlot slot = inventory.GetSlot(index);
            ItemData item = slot.ResolveItem();

            selectedSlot = index;

            if (item == null || slot.amount <= 0 || string.IsNullOrEmpty(slot.uniqueId))
            {
                weaponController.CmdEquipSlot(string.Empty);
                RefreshAll();
                return;
            }

            if (item is WeaponData || item is MeleeItemData)
            {
                weaponController.CmdEquipSlot(slot.uniqueId);
            }

            RefreshAll();
        }
    }
}