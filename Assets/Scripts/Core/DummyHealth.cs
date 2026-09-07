using UnityEngine;
using System;
using System.Collections;
using Mirror;

namespace IsometricShooter.Core
{
    public class DummyHealth : NetworkBehaviour, IDamageable
    {
        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float respawnTime = 3f;
        [SerializeField] private float impactMultiplier = 15f;

        [SyncVar(hook = nameof(OnHealthChangedSync))]
        private float currentHealth;

        [SyncVar]
        private bool isDead;

        private RagdollController ragdollController;
        private Collider mainCollider;
        private Rigidbody mainRigidbody;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;

        public event Action<float, float> OnHealthChanged;

        private void Awake()
        {
            ragdollController = GetComponent<RagdollController>();
            mainCollider = GetComponent<Collider>();
            mainRigidbody = GetComponent<Rigidbody>();

            spawnPosition = transform.position;
            spawnRotation = transform.rotation;

            currentHealth = maxHealth;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            currentHealth = maxHealth;
            isDead = false;
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
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

            StartCoroutine(RespawnRoutine());
        }

        private void OnHealthChangedSync(float oldHealth, float newHealth)
        {
            OnHealthChanged?.Invoke(oldHealth, newHealth);
        }

        private IEnumerator RespawnRoutine()
        {
            yield return new WaitForSeconds(respawnTime);

            currentHealth = maxHealth;
            isDead = false;

            if (ragdollController != null)
            {
                ragdollController.ExitRagdoll();
            }

            RpcRespawn(spawnPosition, spawnRotation);
        }

        [ClientRpc]
        private void RpcRespawn(Vector3 pos, Quaternion rot)
        {
            transform.position = pos;
            transform.rotation = rot;

            if (mainCollider != null)
                mainCollider.enabled = true;

            if (mainRigidbody != null)
            {
                mainRigidbody.isKinematic = false;
                mainRigidbody.velocity = Vector3.zero;
                mainRigidbody.angularVelocity = Vector3.zero;
            }
        }

        public float GetHealth() => currentHealth;
        public float GetMaxHealth() => maxHealth;
        public bool IsDead() => isDead;
    }
}
