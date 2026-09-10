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

        [Header("Locomotion")]
        [SerializeField] private float sprintRotationSpeed = 260f;
        [SerializeField] private TurnController turnController;

        [Header("Vehicle")]
        [SerializeField] private float vehicleEnterRadius = 2.5f;

        [Header("Server Validation")]
        [SerializeField] private float maxSpeedServer = 9f;

        [Header("References")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private LayerMask groundLayer;

        private Rigidbody rb;
        private TopDownCinemachineController topDownCamera;
        private Vector2 moveInput;
        private Vector3 moveDirection;
        private float currentSpeed;
        private Quaternion targetRotation;
        private Vector3 lastSentSyncPosition;
        private float lastSentSyncRotY;

        private const float GroundRayDistance = 1000f;
        private const float MinRotationSqrMagnitude = 0.05f;
        private const float MinMoveSqrMagnitude = 0.01f;
        private const float SendPositionThresholdSqr = 0.0001f;
        private const float SendRotationThreshold = 0.5f;
        private const float ServerMoveTolerance = 0.3f;

        private const byte InputMaskThrottle = 1 << 0;
        private const byte InputMaskReverse = 1 << 1;
        private const byte InputMaskLeft = 1 << 2;
        private const byte InputMaskRight = 1 << 3;
        private const byte InputMaskHandbrake = 1 << 4;

        [SyncVar] private Vector3 syncPosition;
        [SyncVar] private float syncRotationY;

        [SyncVar(hook = nameof(OnVehicleChanged))]
        private Vehicle vehicle;
        [SyncVar]
        private int seatIndex = -1;

        private byte lastSentVehicleInputMask;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            topDownCamera = GetComponent<TopDownCinemachineController>();

            if (turnController == null)
                turnController = GetComponent<TurnController>();

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

            lastSentSyncPosition = transform.position;
            lastSentSyncRotY = transform.rotation.eulerAngles.y;

            if (playerCamera != null)
            {
                playerCamera.gameObject.SetActive(true);

                if (playerCamera.transform.parent != null)
                {
                    playerCamera.transform.SetParent(null, true);
                }
            }

            var ui = GetComponent<PlayerUIController>();
            if (ui != null) ui.isLocal = true;

            var crosshair = GetComponent<WorldCrosshair>();
            if (crosshair != null) crosshair.Initialize(true);

            var cam = GetComponent<TopDownCinemachineController>();
            if (cam != null)
            {
                cam.SetCamera(playerCamera);
                cam.SetActive(true);
            }
        }

        private void OnDestroy()
        {
            if (playerCamera != null && playerCamera.transform.parent == null)
            {
                Destroy(playerCamera.gameObject);
            }
        }

        private void Update()
        {
            if (!isLocalPlayer)
            {
                SyncTransform();
                return;
            }

            if (IsInVehicle)
            {
                UpdateSeatedInputs();
                return;
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                TryEnterVehicle();
            }

            ReadInput();
            CalculateMoveDirection();
            CalculateSpeed();
            CalculateTargetRotation();

            ApplyRotation();

            SendTransformUpdate();
        }

        private void SendTransformUpdate()
        {
            float rotY = transform.rotation.eulerAngles.y;

            if (Vector3.SqrMagnitude(transform.position - lastSentSyncPosition) < SendPositionThresholdSqr &&
                Mathf.Abs(Mathf.DeltaAngle(lastSentSyncRotY, rotY)) < SendRotationThreshold)
            {
                return;
            }

            lastSentSyncPosition = transform.position;
            lastSentSyncRotY = rotY;

            CmdUpdateTransform(lastSentSyncPosition, lastSentSyncRotY);
        }

        private void FixedUpdate()
        {
            if (!isLocalPlayer)
                return;

            if (IsInVehicle)
                return;

            Move();
        }

        [Command(requiresAuthority = true, channel = Channels.Unreliable)]
        private void CmdUpdateTransform(Vector3 pos, float rotY)
        {
            if (IsInVehicle) return;

            float step = Vector3.Distance(pos, syncPosition);
            float maxStep = maxSpeedServer * Time.deltaTime + ServerMoveTolerance;

            if (step > maxStep)
                return;

            syncPosition = pos;
            syncRotationY = rotY;
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
            float cameraYaw = 0f;

            if (topDownCamera != null)
            {
                cameraYaw = topDownCamera.CurrentYaw;
            }
            else if (playerCamera != null)
            {
                cameraYaw = playerCamera.transform.eulerAngles.y;
            }

            Quaternion yawRotation = Quaternion.Euler(0f, cameraYaw, 0f);
            Vector3 cameraForward = yawRotation * Vector3.forward;
            Vector3 cameraRight = yawRotation * Vector3.right;

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

        private void ApplyRotation()
        {
            if (turnController != null && turnController.BlocksRotation)
                return;

            if (IsSprinting())
            {
                RotateTowardMoveDirection();
                return;
            }

            if (turnController != null && turnController.ManagesIdleRotation && IsIdleStanding())
                return;

            ApplySmoothRotation();
        }

        private void RotateTowardMoveDirection()
        {
            if (moveDirection.sqrMagnitude < MinMoveSqrMagnitude)
                return;

            Quaternion target = Quaternion.LookRotation(moveDirection);

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                target,
                sprintRotationSpeed * Time.deltaTime
            );
        }

        public Vector3 GetMoveDirection() { return moveDirection; }
        public bool IsMoving() { return moveInput.sqrMagnitude > MinMoveSqrMagnitude; }
        public bool IsRunning() { return IsSprinting(); }
        public bool IsSprinting() { return Input.GetKey(KeyCode.LeftShift) && moveInput.sqrMagnitude > MinMoveSqrMagnitude && !IsCrouching(); }
        public bool IsCrouching() { return Input.GetKey(KeyCode.LeftControl); }
        public bool IsIdleStanding() { return !IsInVehicle && !IsMoving() && !IsCrouching() && !IsSprinting(); }

        public Vehicle CurrentVehicle => vehicle;
        public bool IsInVehicle => vehicle != null;
        public int CurrentSeatIndex => seatIndex;
        public bool IsVehicleDriver => IsInVehicle && seatIndex == Vehicle.DriverSeatIndex;

        private void UpdateSeatedInputs()
        {
            if (Input.GetKeyDown(KeyCode.F))
            {
                CmdRequestExit();
                return;
            }

            if (!IsVehicleDriver)
                return;

            byte mask = 0;
            if (Input.GetKey(KeyCode.W)) mask |= InputMaskThrottle;
            if (Input.GetKey(KeyCode.S)) mask |= InputMaskReverse;
            if (Input.GetKey(KeyCode.A)) mask |= InputMaskLeft;
            if (Input.GetKey(KeyCode.D)) mask |= InputMaskRight;
            if (Input.GetKey(KeyCode.Space)) mask |= InputMaskHandbrake;

            if (mask != lastSentVehicleInputMask)
            {
                lastSentVehicleInputMask = mask;
                CmdSetCarInput(vehicle, mask);
            }
        }

        private void LateUpdate()
        {
            if (!isLocalPlayer || !IsInVehicle)
                return;

            Transform seat = vehicle != null ? vehicle.GetSeatTransform(seatIndex) : null;
            if (seat == null)
                return;

            transform.position = seat.position;
            transform.rotation = vehicle.transform.rotation;
        }

        private void TryEnterVehicle()
        {
            if (IsInVehicle)
                return;

            Collider[] hits = Physics.OverlapSphere(transform.position, vehicleEnterRadius);
            Vehicle best = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                Vehicle candidate = hits[i].GetComponentInParent<Vehicle>();
                if (candidate == null)
                    continue;

                float sqr = (candidate.transform.position - transform.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = candidate;
                }
            }

            if (best != null)
            {
                CmdRequestEnter(best);
            }
        }

        [Command(requiresAuthority = true)]
        private void CmdRequestEnter(Vehicle target)
        {
            ServerRequestEnter(target);
        }

        [Command(requiresAuthority = true)]
        private void CmdRequestExit()
        {
            ServerRequestExit();
        }

        [Command(requiresAuthority = true)]
        private void CmdSetCarInput(Vehicle target, byte mask)
        {
            if (target == null) return;
            if (vehicle != target) return;
            if (!IsVehicleDriver) return;

            target.ServerSetDriverInput(
                (mask & InputMaskThrottle) != 0,
                (mask & InputMaskReverse) != 0,
                (mask & InputMaskLeft) != 0,
                (mask & InputMaskRight) != 0,
                (mask & InputMaskHandbrake) != 0);
        }

        public void ServerRequestEnter(Vehicle target)
        {
            if (target == null) return;
            if (IsInVehicle) return;
            if (Health.IsDead(gameObject)) return;

            float distance = Vector3.Distance(transform.position, target.transform.position);
            if (distance > target.EnterRange) return;

            target.ServerTryEnter(this, out _);
        }

        public void ServerRequestExit()
        {
            if (vehicle == null) return;

            vehicle.ServerExit(this);
        }

        public void ServerSetVehicle(Vehicle vehicle, int seatIndex)
        {
            this.vehicle = vehicle;
            this.seatIndex = seatIndex;

            Transform seat = vehicle != null ? vehicle.GetSeatTransform(seatIndex) : null;
            if (seat != null)
            {
                syncPosition = seat.position;
                syncRotationY = vehicle.transform.eulerAngles.y;
            }
        }

        public void ServerExitVehicle(Vector3 exitPos, float exitYaw)
        {
            vehicle = null;
            seatIndex = -1;

            syncPosition = exitPos;
            syncRotationY = exitYaw;

            TargetSnapExit(exitPos, exitYaw);
        }

        [TargetRpc]
        private void TargetSnapExit(Vector3 position, float rotationY)
        {
            if (!isLocalPlayer)
                return;

            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
        }

        private void OnVehicleChanged(Vehicle oldVehicle, Vehicle newVehicle)
        {
            SetSeatedVisuals(newVehicle != null);
        }

        private void SetSeatedVisuals(bool seated)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = !seated;
            }

            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = !seated;
            }

            rb.isKinematic = seated;
            rb.velocity = Vector3.zero;
        }
    }
}