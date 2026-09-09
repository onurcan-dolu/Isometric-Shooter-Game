using UnityEngine;

namespace IsometricShooter.Core
{
    [CreateAssetMenu(fileName = "NewMedkit", menuName = "Isometric Shooter/Items/Medkit")]
    public class MedItemData : ItemData
    {
        [Header("Healing")]
        public float healAmount = 25f;
        public float useTime = 1.5f;
    }
}