using UnityEngine;

namespace IsometricShooter.Core
{
    public static class WeaponDropCalculator
    {
        private const float Gravity = 9.81f;
        private const float DefaultDropForwardSpeed = 2f;

        public static Vector3 ComputeSpawnPosition(Transform owner, float heightOffset, float forwardDistance)
        {
            if (owner == null)
                return Vector3.zero;

            Vector3 origin = owner.position;
            origin.y += heightOffset;
            return origin + owner.forward * forwardDistance;
        }

        public static Vector3 ComputeDropVelocity(Transform owner, float throwHeight)
        {
            float forwardXZ = owner != null ? DefaultDropForwardSpeed : 0f;
            return ComputeDropVelocity(owner, throwHeight, forwardXZ);
        }

        public static Vector3 ComputeDropVelocity(Transform owner, float throwHeight, float forwardSpeed)
        {
            Vector3 forward = owner != null ? owner.forward : Vector3.forward;
            float upwardSpeed = Mathf.Sqrt(2f * Mathf.Max(0f, throwHeight) * Gravity);

            return forward * forwardSpeed + Vector3.up * upwardSpeed;
        }
    }
}