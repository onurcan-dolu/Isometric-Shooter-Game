using UnityEngine;
using TMPro;
using IsometricShooter.Core;

namespace IsometricShooter.Player
{
    public class PlayerUIController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Health playerHealth;
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private GameObject playerCanvas;

        public bool isLocal = false;

        public void Initialize(bool isLocalPlayer)
        {
            isLocal = isLocalPlayer;

            if (playerHealth == null)
                playerHealth = GetComponentInParent<Health>();

            if (playerCanvas != null)
            {
                playerCanvas.SetActive(isLocal);
            }

            if (isLocal)
            {
                if (playerHealth != null)
                {
                    playerHealth.OnHealthChanged += OnHealthChangedCallback;
                }

                UpdateHealthUI();
            }
        }

        private void OnHealthChangedCallback(float previousHealth, float currentHealth)
        {
            UpdateHealthUI();
        }

        private void UpdateHealthUI()
        {
            if (playerHealth == null || healthText == null)
                return;

            int currentHealth = Mathf.CeilToInt(playerHealth.GetHealth());
            int maxHealth = Mathf.CeilToInt(playerHealth.GetMaxHealth());

            healthText.text = currentHealth + " / " + maxHealth;
        }
    }
}
