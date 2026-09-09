using UnityEngine;

namespace IsometricShooter.Core
{
    [CreateAssetMenu(fileName = "NewMelee", menuName = "Isometric Shooter/Items/Melee")]
    public class MeleeItemData : ItemData
    {
        [Header("Melee")]
        public float damage = 40f;
        public float range = 2f;
        public float cooldown = 0.7f;
        public float hitArc = 90f;
        [Tooltip("Vuruş anında hedefe uygulanan impact kuvveti (ragdoll).")]
        public float impactForce = 400f;

        [Header("Swing Hit")]
        [Tooltip("Animasyon başladıktan bu saniye sonra damage tespiti başlar (wind-up/el kaldırma).")]
        public float hitStartTime = 0.2f;
        [Tooltip("Animasyon başladıktan bu saniyede damage tespiti biter (savurma sonu).")]
        public float hitEndTime = 0.5f;
        [Tooltip("Temas kontrol noktasının çapı (hit box).")]
        public float hitRadius = 0.4f;
        [Tooltip("Kontrol noktasının model üzerindeki lokal ofseti (örn. kesici uç).")]
        public Vector3 hitPointOffset = new Vector3(0f, 0f, 0.5f);

        [Header("Visuals")]
        public GameObject meleeModelPrefab;

        [Header("Hand Grip")]
        public Vector3 handPositionOffset = Vector3.zero;
        public Vector3 handRotationOffset = Vector3.zero;

        [Header("Hold Pose")]
        public HoldPose holdPose = HoldPose.OneHanded;

        public override GameObject GetEquipModelPrefab()
        {
            return meleeModelPrefab;
        }

        public override GameObject GetWorldModelPrefab()
        {
            return meleeModelPrefab;
        }

        public override HoldPose GetEquipHoldPose()
        {
            return holdPose;
        }
    }
}