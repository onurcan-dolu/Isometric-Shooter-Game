using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace IsometricShooter.Core
{
    public class InventorySlotUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image itemSprite;
        [SerializeField] private TextMeshProUGUI stackText;
        [SerializeField] private TextMeshProUGUI keyText;

        private CanvasGroup canvasGroup;

        public int KeyNumber { get; private set; }

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public void SetKeyNumber(int number)
        {
            KeyNumber = number;
            if (keyText != null)
                keyText.text = number.ToString();
        }

        public void RefreshSlot(InventorySlot slot)
        {
            ItemData item = slot.ResolveItem();
            bool hasItem = slot.amount > 0 && item != null;

            if (itemSprite != null)
            {
                itemSprite.enabled = hasItem;
                if (hasItem && item.icon != null)
                    itemSprite.sprite = item.icon;
            }

            if (stackText != null)
            {
                stackText.text = hasItem && slot.amount > 1 ? "x" + slot.amount : "";
            }
        }

        public void SetHighlighted(bool highlighted)
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            canvasGroup.alpha = highlighted ? 1f : 0.55f;
        }
    }
}