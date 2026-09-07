using UnityEngine;
using UnityEngine.Animations;
using Mirror;
using System.Collections;
using System.Collections.Generic;
using IsometricShooter.Player;

namespace IsometricShooter.Core
{
    public class RagdollController : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private Rigidbody mainRigidbody;
        [SerializeField] private Collider mainCollider;

        [Header("Ragdoll")]
        [SerializeField] private Rigidbody[] ragdollRigidbodies;
        [SerializeField] private Collider[] ragdollColliders;

        [Header("Layers")]
        [SerializeField] private string aliveLayerName = "BulletBodyPart";
        [SerializeField] private string deadLayerName = "Player";
        private int aliveLayerInt;
        private int deadLayerInt;

        [Header("Weapon")]
        [SerializeField] private ParentConstraint weaponParentConstraint;

        [Header("Damage Impact")]
        [SerializeField] private Transform impactPoint;
        [SerializeField] private float impactForce = 8f;
        [SerializeField] private float upwardForce = 2f;
        [SerializeField] private float forceRadius = 0.6f;

        [SyncVar(hook = nameof(OnRagdollStateChanged))]
        private bool isRagdoll;

        private PlayerController cachedPlayerController;
        private ShootingController cachedShootingController;
        private HashSet<Rigidbody> ragdollRbSet;

        private const float MinImpactSqrMagnitude = 0.001f;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();

            if (mainRigidbody == null)
                mainRigidbody = GetComponent<Rigidbody>();

            if (mainCollider == null)
                mainCollider = GetComponent<Collider>();

            cachedPlayerController = GetComponent<PlayerController>();
            cachedShootingController = GetComponent<ShootingController>();

            if (ragdollRigidbodies != null)
            {
                ragdollRbSet = new HashSet<Rigidbody>(ragdollRigidbodies);
            }

            aliveLayerInt = LayerMask.NameToLayer(aliveLayerName);
            deadLayerInt = LayerMask.NameToLayer(deadLayerName);

            if (aliveLayerInt == -1) Debug.LogWarning($"'{aliveLayerName}' layer is not found in Project Settings!");
            if (deadLayerInt == -1) Debug.LogWarning($"'{deadLayerName}' layer is not found in Project Settings!");

            SetRagdoll(false);

            if (weaponParentConstraint != null)
                weaponParentConstraint.enabled = false;
        }

        public void EnterRagdoll(Vector3 hitDirection, Vector3 hitPosition, float force)
        {
            if (!isServer || isRagdoll)
                return;

            isRagdoll = true;
            RpcApplyImpact(hitDirection, hitPosition, force);
        }

        public void EnterRagdoll()
        {
            if (!isServer || isRagdoll)
                return;

            isRagdoll = true;
            ApplyImpactForce();
        }

        public void ExitRagdoll()
        {
            if (!isServer || !isRagdoll)
                return;

            isRagdoll = false;
        }

        [ClientRpc]
        private void RpcApplyImpact(Vector3 hitDirection, Vector3 hitPosition, float force)
        {
            StartCoroutine(ApplyImpactRoutine(hitDirection, hitPosition, force));
        }

        private IEnumerator ApplyImpactRoutine(Vector3 hitDirection, Vector3 hitPosition, float force)
        {
            yield return null;
            ApplyImpactForce(hitDirection, hitPosition, force);
        }

        private void OnRagdollStateChanged(bool oldState, bool newState)
        {
            isRagdoll = newState;

            if (newState)
            {
                if (cachedPlayerController != null) cachedPlayerController.enabled = false;

                if (cachedShootingController != null) cachedShootingController.enabled = false;

                if (animator != null) animator.enabled = false;

                if (mainRigidbody != null)
                {
                    mainRigidbody.velocity = Vector3.zero;
                    mainRigidbody.angularVelocity = Vector3.zero;
                    mainRigidbody.isKinematic = true;
                }

                if (mainCollider != null)
                    mainCollider.enabled = false;

                SetRagdoll(true);

                if (weaponParentConstraint != null)
                    weaponParentConstraint.enabled = true;
            }
            else
            {
                SetRagdoll(false);

                if (animator != null)
                    animator.enabled = true;

                if (cachedPlayerController != null) cachedPlayerController.enabled = true;

                if (cachedShootingController != null) cachedShootingController.enabled = true;

                if (mainRigidbody != null)
                {
                    mainRigidbody.isKinematic = false;
                    mainRigidbody.velocity = Vector3.zero;
                    mainRigidbody.angularVelocity = Vector3.zero;
                }

                if (mainCollider != null)
                    mainCollider.enabled = true;

                if (weaponParentConstraint != null)
                    weaponParentConstraint.enabled = false;
            }
        }

        private void SetRagdoll(bool active)
        {
            int targetLayer = active ? deadLayerInt : aliveLayerInt;

            if (ragdollRigidbodies != null)
            {
                foreach (Rigidbody rb in ragdollRigidbodies)
                {
                    if (rb == null)
                        continue;

                    if (!active && !rb.isKinematic)
                    {
                        rb.velocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }

                    rb.isKinematic = !active;
                    rb.useGravity = active;

                    if (targetLayer != -1)
                    {
                        rb.gameObject.layer = targetLayer;
                    }
                }
            }

            if (ragdollColliders != null)
            {
                foreach (Collider collider in ragdollColliders)
                {
                    if (collider == null)
                        continue;

                    collider.enabled = true;
                    collider.isTrigger = !active;

                    if (targetLayer != -1)
                    {
                        collider.gameObject.layer = targetLayer;
                    }
                }
            }
        }

        private void ApplyImpactForce(Vector3 hitDirection, Vector3 hitPosition, float force)
        {
            Collider[] colliders = Physics.OverlapSphere(hitPosition, forceRadius);
            Vector3 direction = hitDirection.normalized;

            foreach (Collider collider in colliders)
            {
                Rigidbody rb = collider.attachedRigidbody;

                if (rb == null || !IsRagdollRigidbody(rb))
                    continue;

                rb.AddForceAtPosition(direction * force, hitPosition, ForceMode.Impulse);
            }
        }

        private void ApplyImpactForce()
        {
            if (impactPoint == null || ragdollRigidbodies == null || ragdollRigidbodies.Length == 0)
                return;

            Vector3 direction = transform.position - impactPoint.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < MinImpactSqrMagnitude)
                direction = -transform.forward;
            else
                direction.Normalize();

            direction += Vector3.up * upwardForce;
            direction.Normalize();

            Rigidbody closestBody = null;
            float closestDistance = float.MaxValue;

            foreach (Rigidbody rb in ragdollRigidbodies)
            {
                if (rb == null) continue;

                float distance = Vector3.Distance(rb.worldCenterOfMass, impactPoint.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestBody = rb;
                }
            }

            if (closestBody != null)
            {
                closestBody.AddForceAtPosition(direction * impactForce, impactPoint.position, ForceMode.Impulse);
            }
        }

        private bool IsRagdollRigidbody(Rigidbody rb)
        {
            if (ragdollRbSet == null) return false;
            return ragdollRbSet.Contains(rb);
        }

        public bool IsRagdoll() => isRagdoll;
    }
}
