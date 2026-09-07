using UnityEngine;

namespace IsometricShooter.Player
{
    public class WorldCrosshair : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private Transform muzzlePoint;
        [SerializeField] private float maxDistance = 100f;
        [SerializeField] private LayerMask hitLayer = ~0;

        [Header("Visual Object")]
        [SerializeField] private Transform crosshairVisual;

        [Header("Scale Settings")]
        [SerializeField] private float minScale = 0.5f;
        [SerializeField] private float maxScale = 3f;
        [SerializeField] private float referenceDistance = 15f;

        [Header("Rotation Offset")]
        [SerializeField] private Vector3 rotationOffset = Vector3.zero;

        private const float MaxSanityDistance = 10000f;

        private readonly RaycastHit[] raycastBuffer = new RaycastHit[64];

        private Vector3 currentHitPoint;
        private float currentDistance;
        private bool lastHitValid;

        [Header("Distance Offset Settings")]
        [SerializeField] private float minOffset = 0.1f;
        [SerializeField] private float maxOffset = 0.5f;
        [SerializeField] private float offsetReferenceDistance = 15f;

        private bool isLocal;

        public void Initialize(bool isLocalPlayer)
        {
            isLocal = isLocalPlayer;

            if (!isLocal)
            {
                if (crosshairVisual != null) crosshairVisual.gameObject.SetActive(false);
                enabled = false;
                return;
            }

            if (muzzlePoint == null)
                muzzlePoint = transform;

            if (crosshairVisual != null)
            {
                crosshairVisual.gameObject.SetActive(true);
                crosshairVisual.SetParent(null);
            }
        }

        private void OnDestroy()
        {
            if (crosshairVisual != null && isLocal)
            {
                Destroy(crosshairVisual.gameObject);
            }
        }

        private void LateUpdate()
        {
            if (!isLocal || muzzlePoint == null || crosshairVisual == null)
                return;

            UpdateCrosshair();
        }

        private void UpdateCrosshair()
        {
            Vector3 rayOrigin = muzzlePoint.position;
            Vector3 rayDirection = -muzzlePoint.forward;

            int count = Physics.RaycastNonAlloc(rayOrigin, rayDirection, raycastBuffer, maxDistance, hitLayer, QueryTriggerInteraction.Collide);

            lastHitValid = false;
            RaycastHit closestHit = default;
            float closestHitDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = raycastBuffer[i];

                if (hit.collider == null ||
                    hit.collider.transform.root == transform.root)
                    continue;

                if (hit.distance < closestHitDistance)
                {
                    closestHitDistance = hit.distance;
                    closestHit = hit;
                    lastHitValid = true;
                }
            }

            if (lastHitValid)
            {
                currentHitPoint = closestHit.point;
                currentDistance = closestHit.distance;
            }

            if (!lastHitValid)
            {
                currentHitPoint = rayOrigin + (rayDirection * maxDistance);
                currentDistance = maxDistance;
            }

            if (float.IsNaN(currentHitPoint.x) || Mathf.Abs(currentHitPoint.x) > MaxSanityDistance)
                return;

            if (crosshairVisual != null)
            {
                float offsetFactor = Mathf.Clamp01(currentDistance / offsetReferenceDistance);
                float currentOffset = Mathf.Lerp(minOffset, maxOffset, offsetFactor);

                Vector3 directionToMuzzle = (muzzlePoint.position - currentHitPoint).normalized;
                crosshairVisual.position = currentHitPoint + (directionToMuzzle * currentOffset);

                Vector3 dirMuzzle = (muzzlePoint.position - crosshairVisual.position).normalized;
                if (dirMuzzle != Vector3.zero)
                {
                    Quaternion baseRotation = Quaternion.LookRotation(dirMuzzle);
                    crosshairVisual.rotation = baseRotation * Quaternion.Euler(rotationOffset);
                }

                float scaleFactor = Mathf.Clamp(currentDistance / referenceDistance, minScale, maxScale);
                crosshairVisual.localScale = Vector3.one * scaleFactor;
            }
        }
    }
}
