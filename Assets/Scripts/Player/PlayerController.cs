using UnityEngine;
using Mirror;
using IsometricShooter.Core;

namespace IsometricShooter.Player
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : NetworkBehaviour
    {
        [Header("Movement Speed")]
        [SerializeField] private float crouchSpeed = 2.5f;
        [SerializeField] private float walkSpeed = 4f;
        [SerializeField] private float runSpeed = 7f;

        [Header("Interpolation Settings")]
        [SerializeField] private float syncPositionLerpSpeed = 20f;

        [Header("Rotation Settings")]
        [SerializeField] private float rotationSpeed = 20f;

        [Header("References")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private LayerMask groundLayer;

        private Rigidbody rb;
        private Vector2 moveInput;
        private Vector3 moveDirection;
        private float currentSpeed;
        private Quaternion targetRotation;

        private const float GroundRayDistance = 1000f;
        private const float MinRotationSqrMagnitude = 0.05f;
        private const float MinMoveSqrMagnitude = 0.01f;

        [SyncVar] private Vector3 syncPosition;
        [SyncVar(hook = nameof(OnRotationChanged))] private float syncRotationY;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();

            rb.freezeRotation = true;
        }


        public override void OnStartServer()
        {
            syncPosition = transform.position;
            syncRotationY = transform.rotation.eulerAngles.y;

            if (PlayerRespawnManager.Instance != null)
            {
                PlayerRespawnManager.RegisterPlayer(gameObject, connectionToClient);
            }
        }
        public override void OnStartClient()
        {
            base.OnStartClient();

            var uiController = GetComponent<PlayerUIController>();
            if (uiController != null)
                uiController.Initialize(isLocalPlayer);

            if (isLocalPlayer)
                return;


            var cameraController = GetComponent<TopDownCinemachineController>();
            if (cameraController != null)
            {
                cameraController.SetActive(false);
            }

            var crosshair = GetComponent<WorldCrosshair>();
            if (crosshair != null)
            {
                crosshair.Initialize(false);
            }
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            if (playerCamera != null)
                playerCamera.gameObject.SetActive(true);

            var ui = GetComponent<PlayerUIController>();
            if (ui != null) ui.isLocal = true;

            var crosshair = GetComponent<WorldCrosshair>();
            if (crosshair != null) crosshair.Initialize(true);

            var cam = GetComponent<TopDownCinemachineController>();
            if (cam != null) cam.SetActive(true);
        }

        private void Update()
        {
            if (!isLocalPlayer)
            {
                SyncTransform();
                return;
            }

            ReadInput();
            CalculateMoveDirection();
            CalculateSpeed();
            CalculateTargetRotation();

            ApplySmoothRotation();

            CmdUpdateTransform(transform.position, transform.rotation.eulerAngles.y);
        }

        private void FixedUpdate()
        {
            if (!isLocalPlayer)
                return;

            Move();
        }

        [Command]
        private void CmdUpdateTransform(Vector3 pos, float rotY)
        {
            syncPosition = pos;
            syncRotationY = rotY;
        }

        private void OnRotationChanged(float oldRot, float newRot)
        {
        }

        private void SyncTransform()
        {
            transform.position = Vector3.Lerp(transform.position, syncPosition, syncPositionLerpSpeed * Time.deltaTime);

            Quaternion target = Quaternion.Euler(0f, syncRotationY, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, rotationSpeed * Time.deltaTime);
        }

        private void ReadInput()
        {
            moveInput.x = Input.GetAxisRaw("Horizontal");
            moveInput.y = Input.GetAxisRaw("Vertical");

            moveInput = Vector2.ClampMagnitude(moveInput, 1f);
        }

        private void CalculateMoveDirection()
        {
            if (playerCamera == null)
                return;

            Vector3 cameraForward = playerCamera.transform.forward;
            Vector3 cameraRight = playerCamera.transform.right;

            cameraForward.y = 0f;
            cameraRight.y = 0f;

            cameraForward.Normalize();
            cameraRight.Normalize();

            moveDirection =
                cameraForward * moveInput.y +
                cameraRight * moveInput.x;

            moveDirection = Vector3.ClampMagnitude(moveDirection, 1f);
        }

        private void CalculateSpeed()
        {
            if (Input.GetKey(KeyCode.LeftControl))
            {
                currentSpeed = crouchSpeed;
            }
            else if (Input.GetKey(KeyCode.LeftShift) && moveInput.sqrMagnitude > 0f)
            {
                currentSpeed = runSpeed;
            }
            else
            {
                currentSpeed = walkSpeed;
            }
        }

        private void Move()
        {
            Vector3 velocity = moveDirection * currentSpeed;

            rb.velocity = new Vector3(
                velocity.x,
                rb.velocity.y,
                velocity.z
            );
        }

        private void CalculateTargetRotation()
        {
            if (playerCamera == null)
                return;

            Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, GroundRayDistance, groundLayer))
            {
                Vector3 lookDirection = hit.point - transform.position;
                lookDirection.y = 0f;

                if (lookDirection.sqrMagnitude > MinRotationSqrMagnitude)
                {
                    targetRotation = Quaternion.LookRotation(lookDirection);
                }
            }
        }

        private void ApplySmoothRotation()
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        public Vector3 GetMoveDirection() { return moveDirection; }
        public bool IsRunning() { return Input.GetKey(KeyCode.LeftShift) && moveInput.sqrMagnitude > MinMoveSqrMagnitude && !IsCrouching(); }
        public bool IsCrouching() { return Input.GetKey(KeyCode.LeftControl); }
    }
}
