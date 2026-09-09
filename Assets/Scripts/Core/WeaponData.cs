using UnityEngine;

namespace IsometricShooter.Core
{
    [CreateAssetMenu(fileName = "NewWeapon", menuName = "Isometric Shooter/Items/Weapon")]
    public class WeaponData : ItemData
    {
        [Header("Prefab")]
        public GameObject bulletPrefab;

        [Header("Fire Stats")]
        public float fireRate = 0.15f;
        public float damage = 25f;
        public float bulletSpeed = 40f;
        public float maxVerticalSpread = 2f;
        public float maxHorizontalSpread = 0.5f;

        [Header("Ammo")]
        public int magazineSize = 30;
        public int maxReserveAmmo = 120;
        public float reloadTime = 2f;

        [Header("Visuals")]
        public GameObject weaponModelPrefab;

        [Header("Hold Pose")]
        public HoldPose holdPose = HoldPose.TwoHanded;

        [Header("Equipped Pose (local; parent: Weapon Mount / 'Weapon Idle pos')")]
        [Tooltip("Silah eldeyken (idle) mount'a gore yerel pozisyon.")]
        public Vector3 idlePosition = Vector3.zero;
        [Tooltip("Silah eldeyken (idle) yerel rotasyon (Euler).")]
        public Vector3 idleRotation = Vector3.zero;
        [Tooltip("Sag tik basililiyken (aim/ADS) yerel pozisyon.")]
        public Vector3 aimPosition = Vector3.zero;
        [Tooltip("Sag tik basililiyken (aim/ADS) yerel rotasyon (Euler).")]
        public Vector3 aimRotation = Vector3.zero;
        [Tooltip("Idle <-> aim arasi gecis hizi (ne kadar buyukse o kadar hizli).")]
        public float aimTransitionSpeed = 12f;

        public override GameObject GetEquipModelPrefab()
        {
            return weaponModelPrefab;
        }

        public override GameObject GetWorldModelPrefab()
        {
            return weaponModelPrefab;
        }

        public override HoldPose GetEquipHoldPose()
        {
            return holdPose;
        }
    }
}