using UnityEngine;
using Mirror;
using IsometricShooter.Core;

namespace IsometricShooter.Player
{
    public class ShootingController : NetworkBehaviour
{
        [Header("References")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private WeaponController weaponController;
        [SerializeField] private WeaponReferancer weaponReferancer;

        [Header("Weapon Positions")]
        [SerializeField] private Transform idlePosition;
        [SerializeField] private Transform aimPosition;
        [SerializeField] private float aimLerpSpeed = 12f;

        [Header("Weapon Aim")]
        [SerializeField] private float weaponAimSpeed = 720f;
        [SerializeField] private Vector3 aimRotationOffset;

        [Header("Bullet Spread")]
        [SerializeField] private float spreadIncreaseTime = 1f;
        [SerializeField] private float spreadRecoveryTime = 0.5f;

        private float currentSpread;

        [Header("Gizmos")]
        [SerializeField] private bool showAimGizmos = true;

        public Vector3 AimWorldPosition => aimWorldPosition;

        private Vector3 aimWorldPosition;


        private Vector3 aimDirection;

        private Camera mainCamera;
        private bool hasAimTarget;
        private bool isAiming;

        private float nextFireTime;

        private Vector3 lastSentAimPos;
        private bool lastSentIsAiming;
        private bool lastSentHasTarget;

        [SyncVar] private Vector3 syncAimWorldPosition;
        [SyncVar] private bool syncIsAiming;
        [SyncVar] private bool syncHasAimTarget;

        [Header("Wall Obstacle Settings")]
        [SerializeField] private LayerMask obstacleLayer;
        [SerializeField] private float wallCheckDistance = 0.5f;

        private bool isBlockedByWall;

        private PlayerController playerController;

        private const float AimRayDistance = 1000f;
        private const float MinDirectionSqrMagnitude = 0.001f;

        private float serverNextFireTime;

        private readonly RaycastHit[] wallCheckHits = new RaycastHit[32];

private void Awake()
        {
            mainCamera = Camera.main;

            playerController = GetComponent<PlayerController>();

            if (weaponController == null)
                weaponController = GetComponent<WeaponController>();

            if (weaponReferancer == null)
                weaponReferancer = GetComponentInChildren<WeaponReferancer>(true);
        }

        public void SetWeaponReferancer(WeaponReferancer referancer)
        {
            weaponReferancer = referancer;

            if (weaponReferancer != null && weaponReferancer.MuzzleLight != null)
            {
                weaponReferancer.MuzzleLight.enabled = false;
            }
        }

        private bool IsRangedWeaponEquipped()
        {
            return weaponController != null && weaponController.CurrentWeapon != null;
        }

        private void Update()
        {
            bool rangedActive = IsRangedWeaponEquipped();

            if (isLocalPlayer)
            {
                UpdateAim();

                if (rangedActive)
                {
                    UpdateAimState();

                    if (aimWorldPosition != lastSentAimPos || isAiming != lastSentIsAiming || hasAimTarget != lastSentHasTarget)
                    {
                        lastSentAimPos = aimWorldPosition;
                        lastSentIsAiming = isAiming;
                        lastSentHasTarget = hasAimTarget;
                        CmdUpdateAimData(aimWorldPosition, isAiming, hasAimTarget);
                    }
                }
                else
                {
                    isAiming = false;
                    hasAimTarget = false;
                }
            }

            if (isLocalPlayer && rangedActive)
            {
                UpdateSpread();
            }
        }

        public void PerformFire(WeaponData weaponData)
        {
            if (weaponData == null) return;

            Transform firePoint = GetFirePoint();
            Vector3 direction = firePoint != null ? firePoint.right : transform.forward;
            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
            Vector3 spreadDir = ApplySpread(direction, weaponData);

            PlayMuzzleFlash();

            CmdShoot(spawnPos, spreadDir, weaponData.itemId);
        }

        public void ServerPerformFire(WeaponData weaponData)
        {
            if (weaponData == null) return;

            Transform firePoint = GetFirePoint();
            Vector3 direction = firePoint != null ? firePoint.right : transform.forward;
            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;

            CombatUtility.SpawnNetworkBullet(weaponData.bulletPrefab, spawnPos, direction, weaponData.bulletSpeed, gameObject, connectionToClient, weaponData.damage);
        }

        [Command]
        private void CmdUpdateAimData(Vector3 worldPos, bool aiming, bool hasTarget)
        {
            syncAimWorldPosition = worldPos;
            syncIsAiming = aiming;
            syncHasAimTarget = hasTarget;
        }

        private void UpdateSpread()
        {
            if (Input.GetMouseButton(0))
            {
                currentSpread = Mathf.MoveTowards(
                    currentSpread,
                    1f,
                    Time.deltaTime / spreadIncreaseTime
                );
            }
            else
            {
                currentSpread = Mathf.MoveTowards(
                    currentSpread,
                    0f,
                    Time.deltaTime / spreadRecoveryTime
                );
            }
        }

        private void UpdateAim()
        {
            if (playerCamera == null)
                return;

            Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);

            if (!Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    AimRayDistance,
                    groundLayer,
                    QueryTriggerInteraction.Ignore))
            {
                hasAimTarget = false;
                return;
            }

            aimWorldPosition = hit.point;
            hasAimTarget = true;

            Vector3 direction = aimWorldPosition - transform.position;

            if (direction.sqrMagnitude < MinDirectionSqrMagnitude)
                return;

            aimDirection = direction.normalized;
}

        private void UpdateAimState()
        {
            bool wantToAim = Input.GetMouseButton(1);

            isBlockedByWall = GetFirePoint() != null &&
                              CombatUtility.IsChestBlocked(transform, transform.forward, wallCheckDistance, obstacleLayer, wallCheckHits);

            isAiming = (isBlockedByWall || (playerController != null && playerController.IsSprinting())) ? false : wantToAim;
        }

        [Command]
        private void CmdShoot(Vector3 spawnPos, Vector3 spreadDirection, string weaponId)
        {
            if (Health.IsDead(gameObject)) return;

            PlayerController playerController = GetComponent<PlayerController>();
            if (playerController != null && playerController.IsInVehicle)
                return;

            if (weaponController == null || weaponController.CurrentWeapon == null ||
                weaponController.CurrentWeapon.itemId != weaponId)
                return;

            WeaponData weaponData = weaponController.CurrentWeapon;
            if (weaponData.bulletPrefab == null)
                return;

            if (Time.time < serverNextFireTime)
                return;
            serverNextFireTime = Time.time + weaponData.fireRate;

            if (weaponController.CurrentAmmo <= 0)
                return;
            weaponController.ConsumeServerAmmo();

            RpcPlayMuzzleFlash();

            CombatUtility.SpawnNetworkBullet(weaponData.bulletPrefab, spawnPos, spreadDirection, weaponData.bulletSpeed, gameObject, connectionToClient, weaponData.damage);
        }

        [ClientRpc]
        private void RpcPlayMuzzleFlash()
        {
            if (!isLocalPlayer)
            {
                PlayMuzzleFlash();
            }
}

        private void PlayMuzzleFlash()
        {
            if (weaponReferancer != null)
            {
                weaponReferancer.PlayMuzzleFlash();
                weaponReferancer.PlayShootSound();
            }
        }

        private Vector3 ApplySpread(Vector3 direction, WeaponData weaponData)
        {
            if (currentSpread <= 0f)
                return direction.normalized;

            if (weaponData == null)
                return direction.normalized;

            direction.Normalize();

            Vector3 right = Vector3.Cross(direction, Vector3.up).normalized;
            if (right.sqrMagnitude < MinDirectionSqrMagnitude)
            {
                right = Vector3.right;
            }

            Vector3 up = Vector3.Cross(right, direction).normalized;

            float horizontal = Random.Range(-weaponData.maxHorizontalSpread, weaponData.maxHorizontalSpread) * currentSpread;
            float vertical = Random.Range(0f, weaponData.maxVerticalSpread) * currentSpread;

            Vector3 spreadDirection = direction + right * horizontal + up * vertical;

            return spreadDirection.normalized;
        }

        public Vector3 GetAimDirection() => aimDirection;
        public Vector3 GetAimWorldPosition() => aimWorldPosition;
        public Vector3 GetSyncedAimWorldPosition() => syncAimWorldPosition;
        public bool IsAiming() => isAiming;
        public bool IsAimingVisual => isLocalPlayer ? isAiming : syncIsAiming;

        private Transform GetAimPoint()
        {
            return weaponReferancer != null ? weaponReferancer.AimPoint : null;
        }

        private Transform GetFirePoint()
        {
            return weaponReferancer != null ? weaponReferancer.FirePoint : null;
        }

        public void SetAICombatState(bool aim, bool shoot)
        {
            if (!isLocalPlayer && isServer)
            {
                isAiming = aim;
                if (shoot && !isBlockedByWall)
                {
                    WeaponController weaponController = GetComponent<WeaponController>();
                    if (weaponController != null)
                    {
                        weaponController.ServerFireFromAI();
                    }
                }
            }
}

        private void OnDrawGizmos()
        {
            if (!showAimGizmos)
                return;

            Transform firePoint = GetFirePoint();
            if (firePoint != null)
            {
                float chestCheckDistance = 0.8f;

                bool isChestBlocked = CombatUtility.IsChestBlocked(transform, transform.forward, chestCheckDistance, obstacleLayer, wallCheckHits);
                Gizmos.color = isChestBlocked ? Color.red : Color.green;
                Gizmos.DrawLine(transform.position + Vector3.up * CombatUtility.ChestHeightOffset + transform.forward * CombatUtility.ChestForwardOffset,
                    transform.position + Vector3.up * CombatUtility.ChestHeightOffset + transform.forward * CombatUtility.ChestForwardOffset + transform.forward * chestCheckDistance);

                bool isMuzzleStuck = CombatUtility.IsBlockedIgnoringSelf(firePoint.position, firePoint.forward, wallCheckDistance * 0.5f, obstacleLayer, firePoint, wallCheckHits);
                Gizmos.color = isMuzzleStuck ? Color.red : Color.cyan;
                Gizmos.DrawLine(firePoint.position, firePoint.position + firePoint.forward * (wallCheckDistance * 0.5f));
            }
        }
    }
}
