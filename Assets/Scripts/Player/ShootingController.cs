using UnityEngine;
using Mirror;
using System.Collections;
using IsometricShooter.Core;

namespace IsometricShooter.Player
{
    public class ShootingController : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private LayerMask groundLayer;

        [Header("Weapon")]
        [SerializeField] private Transform weaponPivot;
        [SerializeField] private Transform weaponAimPoint;
        [SerializeField] private Transform bulletSpawnPoint;

        [Header("Weapon Positions")]
        [SerializeField] private Transform idlePosition;
        [SerializeField] private Transform aimPosition;
        [SerializeField] private float aimLerpSpeed = 12f;

        [Header("Weapon Aim")]
        [SerializeField] private float weaponAimSpeed = 720f;
        [SerializeField] private Vector3 aimRotationOffset;

        [Header("Shooting")]
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private float bulletSpeed = 40f;
        [SerializeField] private float fireRate = 0.15f;

        [Header("Muzzle Flash & Effects")]
        [SerializeField] private ParticleSystem muzzleFlash;
        [SerializeField] private Light muzzleLight;
        [SerializeField] private float lightDuration = 0.05f;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip shootSound;

        [Header("Bullet Spread")]
        [SerializeField] private float maxVerticalSpread = 2f;
        [SerializeField] private float maxHorizontalSpread = 0.5f;
        [SerializeField] private float spreadIncreaseTime = 1f;
        [SerializeField] private float spreadRecoveryTime = 0.5f;

        private float currentSpread;

        [Header("Gizmos")]
        [SerializeField] private bool showAimGizmos = true;

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

        private const float AimRayDistance = 1000f;
        private const float MinDirectionSqrMagnitude = 0.001f;
        private const float ChestHeightOffset = 1.2f;
        private const float ChestForwardOffset = 0.2f;

        private readonly RaycastHit[] wallCheckHits = new RaycastHit[32];

        private void Awake()
        {
            mainCamera = Camera.main;

            if (muzzleLight != null)
            {
                muzzleLight.enabled = false;
            }
        }

        private void Update()
        {
            if (isLocalPlayer)
            {
                UpdateAim();
                UpdateAimState();

                if (aimWorldPosition != lastSentAimPos || isAiming != lastSentIsAiming || hasAimTarget != lastSentHasTarget)
                {
                    lastSentAimPos = aimWorldPosition;
                    lastSentIsAiming = isAiming;
                    lastSentHasTarget = hasAimTarget;
                    CmdUpdateAimData(aimWorldPosition, isAiming, hasAimTarget);
                }
            }

            UpdateWeapon();

            if (isLocalPlayer)
            {
                UpdateSpread();

                if (Input.GetMouseButton(0) && !isBlockedByWall)
                {
                    if (Time.time >= nextFireTime)
                    {
                        nextFireTime = Time.time + fireRate;
                        Vector3 spawnPos = bulletSpawnPoint != null ? bulletSpawnPoint.position : transform.position;
                        Vector3 spreadDir = ApplySpread(bulletSpawnPoint != null ? bulletSpawnPoint.right : transform.forward);

                        PlayMuzzleFlash();

                        CmdShoot(spawnPos, spreadDir);
                    }
                }
            }
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

            if (bulletSpawnPoint != null)
            {
                Vector3 chestOrigin = transform.position + Vector3.up * ChestHeightOffset + transform.forward * ChestForwardOffset;
                bool isChestBlocked = CheckRaycastIgnoringSelf(chestOrigin, transform.forward, wallCheckDistance);
                isBlockedByWall = isChestBlocked;
            }
            else
            {
                isBlockedByWall = false;
            }

            if (isBlockedByWall)
            {
                isAiming = false;
            }
            else
            {
                isAiming = wantToAim;
            }
        }

        private bool CheckRaycastIgnoringSelf(Vector3 origin, Vector3 direction, float distance)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, wallCheckHits, distance, obstacleLayer, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                if (wallCheckHits[i].collider == null)
                    continue;

                if (wallCheckHits[i].collider.transform.root != transform.root)
                {
                    return true;
                }
            }
            return false;
        }

        private void UpdateWeapon()
        {
            if (weaponPivot == null)
                return;

            bool currentAiming = isLocalPlayer ? isAiming : syncIsAiming;
            Transform target = currentAiming ? aimPosition : idlePosition;

            if (target == null)
                return;

            weaponPivot.position = Vector3.Lerp(
                weaponPivot.position,
                target.position,
                aimLerpSpeed * Time.deltaTime
            );

            if (!currentAiming)
            {
                weaponPivot.rotation = Quaternion.Slerp(
                    weaponPivot.rotation,
                    target.rotation,
                    aimLerpSpeed * Time.deltaTime
                );

                return;
            }

            AimWeapon();
        }

        private void AimWeapon()
        {
            bool currentHasTarget = isLocalPlayer ? hasAimTarget : syncHasAimTarget;
            if (!currentHasTarget)
                return;

            if (weaponAimPoint == null)
                return;

            Vector3 targetWorldPos = isLocalPlayer ? aimWorldPosition : syncAimWorldPosition;
            Vector3 direction = targetWorldPos - weaponAimPoint.position;

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

        [Command]
        private void CmdShoot(Vector3 spawnPos, Vector3 spreadDirection)
        {
            RpcPlayMuzzleFlash();

            if (bulletPrefab != null)
            {
                GameObject bullet = Instantiate(
                    bulletPrefab,
                    spawnPos,
                    Quaternion.LookRotation(spreadDirection, Vector3.up)
                );

                Bullet bulletScript = bullet.GetComponent<Bullet>();
                if (bulletScript != null)
                {
                    bulletScript.Initialize(spreadDirection, bulletSpeed, gameObject, connectionToClient);
                }

                NetworkServer.Spawn(bullet, connectionToClient);
            }
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
            if (muzzleLight != null)
            {
                StopAllCoroutines();
                StartCoroutine(FlashLightRoutine());
            }

            if (audioSource != null && shootSound != null)
            {
                audioSource.PlayOneShot(shootSound);
            }
        }

        private IEnumerator FlashLightRoutine()
        {
            if (muzzleFlash != null)
            {
                muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                muzzleFlash.Play();
            }
            muzzleLight.enabled = true;
            yield return new WaitForSeconds(lightDuration);
            muzzleLight.enabled = false;
        }

        private Vector3 ApplySpread(Vector3 direction)
        {
            if (currentSpread <= 0f)
                return direction.normalized;

            direction.Normalize();

            Vector3 right = Vector3.Cross(direction, Vector3.up).normalized;
            if (right.sqrMagnitude < MinDirectionSqrMagnitude)
            {
                right = Vector3.right;
            }

            Vector3 up = Vector3.Cross(right, direction).normalized;

            float horizontal = Random.Range(-maxHorizontalSpread, maxHorizontalSpread) * currentSpread;
            float vertical = Random.Range(0f, maxVerticalSpread) * currentSpread;

            Vector3 spreadDirection = direction + right * horizontal + up * vertical;

            return spreadDirection.normalized;
        }

        public Vector3 GetAimDirection() => aimDirection;
        public Vector3 GetAimWorldPosition() => aimWorldPosition;
        public bool IsAiming() => isAiming;

        public void SetAICombatState(bool aim, bool shoot)
        {
            if (!isLocalPlayer && isServer)
            {
                isAiming = aim;
                if (shoot && !isBlockedByWall)
                {
                    if (Time.time >= nextFireTime)
                    {
                        nextFireTime = Time.time + fireRate;
                        Vector3 spawnPos = bulletSpawnPoint != null ? bulletSpawnPoint.position : transform.position;
                        Vector3 spreadDir = ApplySpread(bulletSpawnPoint != null ? bulletSpawnPoint.right : transform.forward);

                        PlayMuzzleFlash();
                        CmdShoot(spawnPos, spreadDir);
                    }
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (!showAimGizmos)
                return;

            if (bulletSpawnPoint != null)
            {
                Vector3 chestOrigin = transform.position + Vector3.up * ChestHeightOffset + transform.forward * ChestForwardOffset;
                float chestCheckDistance = 0.8f;

                bool isChestBlocked = CheckRaycastIgnoringSelf(chestOrigin, transform.forward, chestCheckDistance);
                Gizmos.color = isChestBlocked ? Color.red : Color.green;
                Gizmos.DrawLine(chestOrigin, chestOrigin + transform.forward * chestCheckDistance);

                bool isMuzzleStuck = CheckRaycastIgnoringSelf(bulletSpawnPoint.position, bulletSpawnPoint.forward, wallCheckDistance * 0.5f);
                Gizmos.color = isMuzzleStuck ? Color.red : Color.cyan;
                Gizmos.DrawLine(bulletSpawnPoint.position, bulletSpawnPoint.position + bulletSpawnPoint.forward * (wallCheckDistance * 0.5f));
            }
        }
    }
}
