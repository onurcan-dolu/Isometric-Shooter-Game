using UnityEngine;
using Mirror;
using System.Collections.Generic;
using IsometricShooter.Core;

namespace IsometricShooter.AI
{
    public class MachineGunController : NetworkBehaviour
    {
        [Header("Target Detection")]
        [SerializeField] private float detectionRange = 20f;
        [SerializeField] private float targetRefreshRate = 0.2f;

        [Header("Weapon")]
        [SerializeField] private Transform weaponPivot;
        [SerializeField] private Transform bulletSpawnPoint;

        [Header("Aim")]
        [SerializeField] private float rotationSpeed = 360f;
        [SerializeField] private Vector3 rotationOffset;

        [Header("Shooting")]
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private float bulletSpeed = 40f;
        [SerializeField] private float fireRate = 0.1f;

        [Header("Muzzle")]
        [SerializeField] private ParticleSystem muzzleFlash;

        private Transform currentTarget;

        private const float MinDirectionSqrMagnitude = 0.001f;

        private readonly List<Transform> playerBuffer = new List<Transform>();

        private float nextFireTime;
        private float nextTargetSearchTime;

        private void Update()
        {
            if (!isServer)
                return;

            FindTarget();

            if (currentTarget == null)
                return;

            AimAtTarget();
            Shoot();
        }

        private void FindTarget()
        {
            if (Time.time < nextTargetSearchTime)
                return;

            nextTargetSearchTime =
                Time.time + targetRefreshRate;

            PlayerRespawnManager.GetAlivePlayerTransforms(playerBuffer);

            Transform closestPlayer = null;

            float closestDistance =
                detectionRange * detectionRange;

            foreach (Transform player in playerBuffer)
            {
                if (player == null ||
                    player.gameObject == gameObject)
                    continue;

                float distance =
                    (player.position - transform.position)
                    .sqrMagnitude;

                if (distance > closestDistance)
                    continue;

                closestDistance = distance;
                closestPlayer = player;
            }

            currentTarget = closestPlayer;
        }

        private void AimAtTarget()
        {
            if (weaponPivot == null)
                return;

            Vector3 direction =
                currentTarget.position -
                weaponPivot.position;

            if (direction.sqrMagnitude < MinDirectionSqrMagnitude)
                return;

            Quaternion targetRotation =
                Quaternion.LookRotation(
                    direction.normalized,
                    Vector3.up
                );

            targetRotation *=
                Quaternion.Euler(rotationOffset);

            weaponPivot.rotation =
                Quaternion.RotateTowards(
                    weaponPivot.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
        }

        private void Shoot()
        {
            if (bulletPrefab == null || bulletSpawnPoint == null)
                return;

            if (Time.time < nextFireTime)
                return;

            nextFireTime = Time.time + fireRate;

            Vector3 direction = bulletSpawnPoint.right;
            if (direction.sqrMagnitude < MinDirectionSqrMagnitude)
                direction = transform.forward;

            direction.Normalize();

            RpcPlayMuzzleFlash();

            GameObject bullet = Instantiate(
                bulletPrefab,
                bulletSpawnPoint.position,
                Quaternion.LookRotation(direction, Vector3.up)
            );

            Bullet bulletScript = bullet.GetComponent<Bullet>();
            if (bulletScript != null)
            {
                bulletScript.Initialize(direction, bulletSpeed, gameObject, null);
            }

            NetworkServer.Spawn(bullet);
        }

        [ClientRpc]
        private void RpcPlayMuzzleFlash()
        {
            PlayMuzzleFlash();
        }

        private void PlayMuzzleFlash()
        {
            if (muzzleFlash == null)
                return;

            muzzleFlash.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );

            muzzleFlash.Play();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;

            Gizmos.DrawWireSphere(
                transform.position,
                detectionRange
            );

            if (currentTarget != null &&
                bulletSpawnPoint != null)
            {
                Gizmos.color = Color.yellow;

                Gizmos.DrawLine(
                    bulletSpawnPoint.position,
                    currentTarget.position
                );
            }
        }
    }
}
