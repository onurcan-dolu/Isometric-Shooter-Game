using UnityEngine;
using Mirror;
using System;

namespace IsometricShooter.Core
{
    public class Health : NetworkBehaviour, IDamageable
    {
        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float impactMultiplier = 15f;

        [SyncVar(hook = nameof(OnHealthChangedSync))]
        private float currentHealth;

        [SyncVar(hook = nameof(OnDeathStateSync))]
        private bool isDead;

        private RagdollController ragdollController;

        public event Action OnDeath;
        public event Action<float, float> OnHealthChanged;

        private void Awake()
        {
            ragdollController = GetComponent<RagdollController>();
            currentHealth = maxHealth;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            currentHealth = maxHealth;
            isDead = false;
        }

        public void TakeDamage(float damage, Vector3 hitDirection, Vector3 hitPosition, float impactForce = 20f)
        {
            if (!isServer || isDead || damage <= 0f)
                return;

            currentHealth -= damage;
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

            if (currentHealth <= 0f)
            {
                Die(hitDirection, hitPosition, impactForce);
            }
        }

        private void Die(Vector3 hitDirection, Vector3 hitPosition, float force)
        {
            if (isDead)
                return;

            currentHealth = 0f;
            isDead = true;

            if (ragdollController != null)
            {
                ragdollController.EnterRagdoll(hitDirection, hitPosition, force * impactMultiplier);
            }
        }

        private void OnHealthChangedSync(float oldHealth, float newHealth)
        {
            OnHealthChanged?.Invoke(oldHealth, newHealth);
        }

        private void OnDeathStateSync(bool oldState, bool newState)
        {
            if (newState)
            {
                OnDeath?.Invoke();
            }
        }

        public float GetHealth() => currentHealth;
        public float GetMaxHealth() => maxHealth;
        public bool IsDead() => isDead;
    }
}
