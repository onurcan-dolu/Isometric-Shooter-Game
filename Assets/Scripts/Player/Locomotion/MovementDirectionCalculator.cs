using UnityEngine;

namespace IsometricShooter.Player
{
    public static class MovementDirectionCalculator
    {
        public static Vector2 CalculateLocalMove(Vector3 moveDirection, Transform character)
        {
            Vector3 local = character.InverseTransformDirection(moveDirection);

            return new Vector2(
                Mathf.Clamp(local.x, -1f, 1f),
                Mathf.Clamp(local.z, -1f, 1f)
            );
        }
    }
}