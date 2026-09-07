using Mirror;
using UnityEngine;
using IsometricShooter.Core;

namespace IsometricShooter.AI
{
    public class AIShootingController : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform weaponPivot;
        [SerializeField] private Transform weaponAimPoint;
        [SerializeField] private Transform bulletSpawnPoint;
        [SerializeField] private LayerMask obstacleLayer;

        [Header("Weapon Positions")]
        [SerializeField] private Transform idlePosition;
        [SerializeField] private Transform aimPosition;
        [SerializeField] private float aimLerpSpeed = 12f;

        [Header("Weapon Aim")]
        [SerializeField] private float weaponAimSpeed = 720f;
        [SerializeField] private Vector3 aimRotationOffset;

        [Header("Shooting Stats")]
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private float bulletSpeed = 40f;
        [SerializeField] private float fireRate = 0.15f;
        [SerializeField] private float wallCheckDistance = 0.5f;

        private float nextFireTime;
        private bool isBlockedByWall;
        private bool isAiming;
        private Vector3 currentTargetWorldPosition;

        private const float ChestHeightOffset = 1.2f;
        private const float MinDirectionSqrMagnitude = 0.001f;

        private readonly RaycastHit[] wallCheckHits = new RaycastHit[32];

        private void Update()
        {
            CheckWallObstacle();
            UpdateWeaponVisuals();
        }

        private void CheckWallObstacle()
        {
            if (bulletSpawnPoint != null)
            {
                Vector3 origin = transform.position + Vector3.up * ChestHeightOffset;
                Vector3 dir = transform.forward;

                int count = Physics.RaycastNonAlloc(origin, dir, wallCheckHits, wallCheckDistance, obstacleLayer, QueryTriggerInteraction.Ignore);
                bool blocked = false;

                for (int i = 0; i < count; i++)
                {
                    if (wallCheckHits[i].collider == null)
                        continue;

                    if (wallCheckHits[i].collider.transform.root != transform.root)
                    {
                        blocked = true;
                        break;
                    }
                }
                isBlockedByWall = blocked;
            }
        }

        public void SetAimState(bool aiming, Vector3 targetWorldPosition)
        {
            if (isBlockedByWall)
            {
                isAiming = false;
                return;
            }

            isAiming = aiming;
            currentTargetWorldPosition = targetWorldPosition;
        }

        private void UpdateWeaponVisuals()
        {
            if (weaponPivot == null)
                return;

            Transform targetPosTransform = isAiming ? aimPosition : idlePosition;
            if (targetPosTransform == null)
                return;

            weaponPivot.position = Vector3.Lerp(
                weaponPivot.position,
                targetPosTransform.position,
                aimLerpSpeed * Time.deltaTime
            );

            if (!isAiming)
            {
                weaponPivot.rotation = Quaternion.Slerp(
                    weaponPivot.rotation,
                    targetPosTransform.rotation,
                    aimLerpSpeed * Time.deltaTime
                );
            }
            else
            {
                weaponPivot.rotation = Quaternion.Slerp(
                    weaponPivot.rotation,
                    targetPosTransform.rotation,
                    aimLerpSpeed * Time.deltaTime
                );

                AimWeapon(currentTargetWorldPosition);
            }
        }

        private void AimWeapon(Vector3 targetWorldPosition)
        {
            if (weaponAimPoint == null)
                return;

            Vector3 direction = targetWorldPosition - weaponAimPoint.position;
            if (direction.sqrMagnitude < MinDirectionSqrMagnitude)
                return;

            direction.Normalize();

            Quaternion lookRotation = Quaternion.LookRotation(direction, Vector3.up);
            Quaternion offset = Quaternion.Euler(aimRotationOffset);
            Quaternion targetRotation = lookRotation * offset;

            weaponPivot.rotation = Quaternion.RotateTowards(
                weaponPivot.rotation,
                targetRotation,
                weaponAimSpeed * Time.deltaTime
            );
        }

        public void TryShoot(Vector3 targetDirection)
        {
            if (!isServer) return;

            if (isBlockedByWall)
                return;

            if (Time.time >= nextFireTime)
            {
                nextFireTime = Time.time + fireRate;

                if (bulletPrefab != null && bulletSpawnPoint != null)
                {
                    Vector3 spawnPos = bulletSpawnPoint.position;
                    GameObject bullet = Instantiate(
                        bulletPrefab,
                        spawnPos,
                        Quaternion.LookRotation(targetDirection, Vector3.up)
                    );

                    NetworkServer.Spawn(bullet);

                    Bullet bulletScript = bullet.GetComponent<Bullet>();
                    if (bulletScript != null)
                    {
                        bulletScript.Initialize(targetDirection, bulletSpeed, gameObject, connectionToClient);
                    }
                }
            }
        }

        public bool IsBlockedByWall() => isBlockedByWall;
    }
}
