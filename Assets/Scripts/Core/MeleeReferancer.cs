using UnityEngine;

namespace IsometricShooter.Core
{
    public class MeleeReferancer : MonoBehaviour
    {
        [Header("Hit Point")]
        [Tooltip("Melee'de damage taramasının yapılacağı nokta. Boş bırakılırsa 'HitPoint' adında bir çocuk transform aranır.")]
        [SerializeField] private Transform hitPoint;

        public Transform HitPoint
        {
            get
            {
                if (hitPoint == null)
                {
                    hitPoint = FindDeepChild(transform, "HitPoint");
                    if (hitPoint == null)
                        hitPoint = transform;
                }

                return hitPoint;
            }
        }

        private void Awake()
        {
            if (hitPoint == null)
            {
                hitPoint = FindDeepChild(transform, "HitPoint");
                if (hitPoint == null)
                    hitPoint = transform;
            }
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            if (root == null)
                return null;

            if (root.name == name)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindDeepChild(root.GetChild(i), name);
                if (result != null)
                    return result;
            }

            return null;
        }

        [ContextMenu("Auto Fill")]
        private void AutoFill()
        {
            hitPoint = FindDeepChild(transform, "HitPoint");
            if (hitPoint == null)
                hitPoint = transform;
        }
    }
}