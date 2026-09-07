using UnityEngine;
using Cinemachine;

namespace IsometricShooter.Player
{
    public class TopDownCinemachineController : MonoBehaviour
    {
        [Header("Cinemachine Reference")]
        [SerializeField] private CinemachineVirtualCamera virtualCamera;

        [Header("Camera Settings")]
        [SerializeField] private Vector3 followOffset = new Vector3(0f, 12f, -8f);
        [SerializeField] private float rotationSpeed = 120f;

        private const float CameraFollowSpeed = 20f;
        private const float LookHeightOffset = 1.5f;

        private float currentYaw;
        private bool isLocal;

        private void Awake()
        {
            if (virtualCamera == null)
            {
                virtualCamera = GetComponentInChildren<CinemachineVirtualCamera>();
            }

            if (virtualCamera != null)
            {
                virtualCamera.gameObject.SetActive(false);
            }
        }

        public void SetActive(bool isLocalPlayer)
        {
            isLocal = isLocalPlayer;

            if (virtualCamera == null)
            {
                virtualCamera = GetComponentInChildren<CinemachineVirtualCamera>();
            }

            if (!isLocal)
            {
                if (virtualCamera != null)
                {
                    virtualCamera.gameObject.SetActive(false);
                }
                return;
            }

            if (virtualCamera != null)
            {
                virtualCamera.gameObject.SetActive(true);

                virtualCamera.Follow = transform;
                virtualCamera.LookAt = transform;

                var transposer = virtualCamera.GetCinemachineComponent<CinemachineTransposer>();
                if (transposer != null)
                {
                    transposer.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
                    transposer.m_FollowOffset = followOffset;
                }
            }
        }

        private void LateUpdate()
        {
            if (!isLocal || virtualCamera == null)
                return;

            HandleCameraRotation();

            Quaternion rotation = Quaternion.Euler(0f, currentYaw, 0f);
            Vector3 targetCameraPosition = transform.position + (rotation * followOffset);

            virtualCamera.transform.position = Vector3.Lerp(virtualCamera.transform.position, targetCameraPosition, Time.deltaTime * CameraFollowSpeed);

            virtualCamera.transform.LookAt(transform.position + Vector3.up * LookHeightOffset);
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

                var transposer = virtualCamera.GetCinemachineComponent<CinemachineTransposer>();
                if (transposer != null)
                {
                    Quaternion rotation = Quaternion.Euler(0f, currentYaw, 0f);
                    Vector3 flatOffset = new Vector3(0f, followOffset.y, followOffset.z);
                    transposer.m_FollowOffset = rotation * flatOffset;
                }
            }
        }
    }
}
