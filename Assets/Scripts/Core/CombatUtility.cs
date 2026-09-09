using UnityEngine;
using Mirror;

namespace IsometricShooter.Core
{
    public static class CombatUtility
    {
        public const float ChestHeightOffset = 1.2f;
        public const float ChestForwardOffset = 0.2f;

        public static bool IsBlockedIgnoringSelf(Vector3 origin, Vector3 direction, float distance, LayerMask obstacleLayer, Transform selfRoot, RaycastHit[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
                return false;

            int count = Physics.RaycastNonAlloc(origin, direction, buffer, distance, obstacleLayer, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                if (buffer[i].collider == null)
                    continue;

                if (selfRoot == null || buffer[i].collider.transform.root != selfRoot.root)
                    return true;
            }

            return false;
        }

        public static bool IsChestBlocked(Transform self, Vector3 forward, float distance, LayerMask obstacleLayer, RaycastHit[] buffer)
        {
            if (self == null)
                return false;

            Vector3 origin = self.position + Vector3.up * ChestHeightOffset + self.forward * ChestForwardOffset;
            return IsBlockedIgnoringSelf(origin, forward, distance, obstacleLayer, self, buffer);
        }

        public static GameObject SpawnNetworkBullet(GameObject prefab, Vector3 spawnPos, Vector3 direction, float speed, GameObject owner, NetworkConnectionToClient conn, float damage = -1f)
        {
            if (prefab == null)
                return null;

            GameObject bullet = Object.Instantiate(prefab, spawnPos, Quaternion.LookRotation(direction, Vector3.up));

            Bullet bulletScript = bullet.GetComponent<Bullet>();
            if (bulletScript != null)
            {
                bulletScript.Initialize(direction, speed, owner, conn);
                if (damage >= 0f)
                    bulletScript.SetDamage(damage);
            }

            NetworkServer.Spawn(bullet, conn);
            return bullet;
        }
    }
}