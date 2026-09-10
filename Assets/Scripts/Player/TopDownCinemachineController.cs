using IsometricShooter.Core;
using UnityEngine;

namespace IsometricShooter.Player
{
    public class TopDownCinemachineController : MonoBehaviour
    {
        [Header("Camera Reference")]
        [Tooltip("Surulen ana kamera. PlayerController bunu OnStartLocalPlayer'da set eder.")]
        [SerializeField] private Camera targetCamera;

        [Header("Camera Settings")]
        [Tooltip("Kameranin merkez noktasina gore ofseti (yukseklik ve geri cekilme mesafesi).")]
        [SerializeField] private Vector3 followOffset = new Vector3(0f, 12f, -8f);
        [Tooltip("ARACTAYKEN kameranin merkez noktasina gore ofseti (yukseklik ve geri cekilme mesafesi).")]
        [SerializeField] private Vector3 vehicleFollowOffset = new Vector3(0f, 16f, -12f);
        [Tooltip("Hedefin (player/arac) merkez / fare noktasi etrafinda ekran merkezinden kayabilecegi maksimum mesafe (metre).")]
        [SerializeField] private float maxAnchorOffset = 5f;
        [Tooltip("ARACTAYKEN hedefin kayabilecegi maksimum mesafe (metre).")]
        [SerializeField] private float vehicleMaxAnchorOffset = 8f;
        [Tooltip("Fare imlecinin ekran kenarina gitmesiyle nisan noktasinin hedef etrafinda kayabilecegi en uzak mesafe (metre).")]
        [SerializeField] private float maxAimLookDistance = 25f;
        [Tooltip("ARACTAYKEN fare nisan noktasinin kayabilecegi en uzak mesafe.")]
        [SerializeField] private float vehicleMaxAimLookDistance = 50f;
        [Tooltip("Q/E ile yatay kacis donusu hizi (derece/sn).")]
        [SerializeField] private float rotationSpeed = 120f;

        private const float LookHeightOffset = 1.5f;

        private float currentYaw;
        private bool isLocal;

        public void SetActive(bool isLocalPlayer)
        {
            isLocal = isLocalPlayer;
        }

        public void SetCamera(Camera camera)
        {
            targetCamera = camera;
        }

        public float CurrentYaw => currentYaw;

        private void LateUpdate()
        {
            if (!isLocal || targetCamera == null)
                return;

            HandleCameraRotation();

            bool inVehicle = false;
            Vector3 targetPosition = GetTargetPosition(ref inVehicle);

            float maxAimLook = inVehicle ? vehicleMaxAimLookDistance : maxAimLookDistance;
            float maxAnchor = inVehicle ? vehicleMaxAnchorOffset : maxAnchorOffset;
            Vector3 offset = inVehicle ? vehicleFollowOffset : followOffset;

            Vector3 aimPoint = GetAimPoint(targetPosition, maxAimLook);
            Vector3 centerPoint = ClampAnchor(aimPoint, targetPosition, maxAnchor);

            Quaternion rotation = Quaternion.Euler(0f, currentYaw, 0f);
            Vector3 desiredPosition = centerPoint + (rotation * offset);

            targetCamera.transform.position = desiredPosition;

            Vector3 lookDirection = (centerPoint + Vector3.up * LookHeightOffset) - desiredPosition;
            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                targetCamera.transform.rotation = Quaternion.LookRotation(lookDirection);
            }
        }

        private Vector3 GetTargetPosition(ref bool inVehicle)
        {
            PlayerController player = GetComponent<PlayerController>();
            if (player != null)
            {
                Vehicle vehicle = player.CurrentVehicle;
                if (vehicle != null)
                {
                    inVehicle = true;
                    return vehicle.transform.position;
                }
            }

            return transform.position;
        }

        private Vector3 GetAimPoint(Vector3 targetPosition, float maxLookDistance)
        {
            Vector3 viewport = targetCamera.ScreenToViewportPoint(Input.mousePosition);
            viewport.x = Mathf.Clamp01(viewport.x);
            viewport.y = Mathf.Clamp01(viewport.y);

            float offsetX = (viewport.x - 0.5f) * 2f;
            float offsetY = (viewport.y - 0.5f) * 2f;

            Quaternion yawRotation = Quaternion.Euler(0f, currentYaw, 0f);
            Vector3 basisRight = yawRotation * Vector3.right;
            Vector3 basisForward = yawRotation * Vector3.forward;

            Vector3 aimDirection = basisRight * offsetX + basisForward * offsetY;
            return targetPosition + (aimDirection * maxLookDistance);
        }

        private static Vector3 ClampAnchor(Vector3 aimPoint, Vector3 targetPosition, float maxOffset)
        {
            Vector3 flat = targetPosition - aimPoint;
            flat.y = 0f;

            float sqrMagnitude = flat.sqrMagnitude;
            float maxSqr = maxOffset * maxOffset;
            if (sqrMagnitude > maxSqr)
            {
                flat = flat.normalized * maxOffset;
            }

            return aimPoint + flat;
        }

        private void HandleCameraRotation()
        {
            float rotationInput = 0f;

            if (Input.GetKey(KeyCode.Q))
            {
                rotationInput = -1f;
            }
            else if (Input.GetKey(KeyCode.E))
            {
                rotationInput = 1f;
            }

            if (rotationInput != 0f)
            {
                currentYaw += rotationInput * rotationSpeed * Time.deltaTime;
            }
        }
    }
}
