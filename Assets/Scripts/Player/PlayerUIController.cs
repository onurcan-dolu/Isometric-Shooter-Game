using UnityEngine;
using TMPro;
using IsometricShooter.Core;
using UnityEngine.UI;

namespace IsometricShooter.Player
{
    public class PlayerUIController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Health playerHealth;
        [SerializeField] private TMP_Text healthText; 
        [SerializeField] private Image healthFiller; 
        [SerializeField] private GameObject playerCanvas;

        [Header("Weapon UI")]
        [SerializeField] private TMP_Text ammoText;
        [SerializeField] private TMP_Text weaponNameText;
        [SerializeField] private GameObject reloadActiveText;

        private WeaponController weaponController;

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

                weaponController = GetComponent<WeaponController>();
                if (weaponController != null)
                {
                    weaponController.OnAmmoUpdated += OnAmmoUpdatedCallback;
                    weaponController.OnItemChanged += OnItemChangedCallback;
                    weaponController.OnReloadStateChanged += OnReloadStateChangedCallback;
                }

                UpdateHealthUI();
                UpdateWeaponUI(weaponController != null ? weaponController.CurrentAmmo : 0, weaponController != null ? weaponController.ReserveAmmo : 0);
                UpdateWeaponNameUI();
                UpdateReloadUI(false);
            }
        }

        private void OnDestroy()
        {
            if (!isLocal) return;

            if (playerHealth != null)
                playerHealth.OnHealthChanged -= OnHealthChangedCallback;

            if (weaponController != null)
            {
                weaponController.OnAmmoUpdated -= OnAmmoUpdatedCallback;
                weaponController.OnItemChanged -= OnItemChangedCallback;
                weaponController.OnReloadStateChanged -= OnReloadStateChangedCallback;
            }
        }

        private void OnHealthChangedCallback(float previousHealth, float currentHealth)
        {
            UpdateHealthUI();
        }

        private void OnAmmoUpdatedCallback(int currentAmmo, int reserveAmmo)
        {
            UpdateWeaponUI(currentAmmo, reserveAmmo);
        }

        private void OnItemChangedCallback()
        {
            OnAmmoUpdatedCallback(weaponController != null ? weaponController.CurrentAmmo : 0, weaponController != null ? weaponController.ReserveAmmo : 0);
            UpdateWeaponNameUI();
        }

        private void OnReloadStateChangedCallback(bool isReloading)
        {
            UpdateReloadUI(isReloading);
        }

        private void UpdateHealthUI()
        {
            if (playerHealth == null || healthText == null)
                return;

            int currentHealth = Mathf.CeilToInt(playerHealth.GetHealth());
            int maxHealth = Mathf.CeilToInt(playerHealth.GetMaxHealth());
            healthFiller.fillAmount = (float)currentHealth / (float)maxHealth;
            healthText.text = currentHealth + " / " + maxHealth;
        }

        private void UpdateWeaponUI(int currentAmmo, int reserveAmmo)
        {
            if (ammoText == null)
                return;

            if (weaponController != null && weaponController.CurrentItem == null)
            {
                ammoText.text = "-- / --";
                return;
            }

            if (weaponController != null && weaponController.CurrentMelee != null)
            {
                ammoText.text = "-- / --";
                return;
            }

            ammoText.text = currentAmmo + " / " + reserveAmmo;
        }

        private void UpdateWeaponNameUI()
        {
            if (weaponNameText == null)
                return;

            if (weaponController != null && weaponController.CurrentItem != null)
            {
                weaponNameText.text = weaponController.CurrentItem.itemName;
            }
            else
            {
                weaponNameText.text = "";
            }
        }

        private void UpdateReloadUI(bool isReloading)
        {
            if (reloadActiveText == null)
                return;

            reloadActiveText.SetActive(isReloading);
        }
    }
}