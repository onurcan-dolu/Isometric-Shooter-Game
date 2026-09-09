using UnityEngine;

namespace IsometricShooter.Core
{
    [CreateAssetMenu(fileName = "NewGrenade", menuName = "Isometric Shooter/Items/Grenade")]
    public class GrenadeItemData : ItemData
    {
        [Header("Explosion")]
        public float damage = 75f;
        public float radius = 6f;
        public float fuseTime = 2f;
        public GameObject grenadePrefab;

        public override GameObject GetWorldModelPrefab()
        {
            return grenadePrefab;
        }
    }
}