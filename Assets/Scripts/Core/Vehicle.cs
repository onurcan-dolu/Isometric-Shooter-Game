using UnityEngine;
using Mirror;
using IsometricShooter.Player;

namespace IsometricShooter.Core
{
    [RequireComponent(typeof(NetworkIdentity))]
    public class Vehicle : NetworkBehaviour, IInteractable
    {
        public const int DriverSeatIndex = 0;
        public const int WheelCount = 4;

        [Header("Seats")]
        [Tooltip("Car seat transforms. Index 0 is always the driver seat.")]
        [SerializeField] private Transform[] seats = new Transform[0];
        [SerializeField] private float interactionRange = 2.5f;
        [SerializeField] private float exitOffsetDistance = 2f;

        [Header("Driver Controller")]
        [SerializeField] private PrometeoCarController prometeo;

        public readonly SyncList<uint> occupants = new SyncList<uint>();

        private bool prometeoExternalInputMode;
        private bool driverThrottle;
        private bool driverReverse;
        private bool driverLeft;
        private bool driverRight;
        private bool driverHandbrake;

        private const uint EmptySeat = 0;

        [Header("Root Sync")]
        [Tooltip("Kök transform, Mirror'un NetworkTransformReliable bileseni tarafindan senkronlanir (client'ta kendi snapshot interpolasyonu ile yumusatilir). Vehicle kod konumu yazmaz.")]
        [SerializeField] private bool requireNetworkTransform = true;

        [Header("Wheels")]
        [Tooltip("Tekerlek mesh transform'lari (SI-RA: FL, FR, RL, RR). Bos ise teker animasyonu kapali.")]
        [SerializeField] private Transform[] wheelMeshes = new Transform[0];
        [Tooltip("Client tekerlek donus takip hizi.")]
        [SerializeField] private float wheelRotationSmoothing = 8f;
        [Tooltip("Sunucu tekerlek gonderim toleransi (derece).")]
        [SerializeField] private float wheelSyncDeadband = 0.25f;

        [Header("Particles")]
        [Tooltip("Client'ta drift dumani olarak oynatilacak partikuller.")]
        [SerializeField] private ParticleSystem[] wheelSmokeParticles = new ParticleSystem[0];

        [SyncVar] private Quaternion syncWheel0;
        [SyncVar] private Quaternion syncWheel1;
        [SyncVar] private Quaternion syncWheel2;
        [SyncVar] private Quaternion syncWheel3;
        [SyncVar(hook = nameof(OnDriftingChanged))] private bool syncDrifting;

        public float EnterRange => interactionRange;
        public int SeatCount => seats.Length;
        public string InteractionPrompt => "F - Araca Bin";

        public override void OnStartServer()
        {
            base.OnStartServer();

            EnsureVisualReferences();
            VerifyNetworkTransform();

            occupants.Clear();
            for (int i = 0; i < seats.Length; i++)
            {
                occupants.Add(EmptySeat);
            }

            if (prometeo != null)
            {
                prometeo.useTouchControls = false;
                prometeo.useExternalInput = true;
                prometeoExternalInputMode = true;
            }

            Debug.Log($"[Vehicle] Sunucu tarafinda aktif (netId={netId}), konum={transform.position}");

            for (int i = 0; i < wheelMeshes.Length && i < WheelCount; i++)
            {
                if (wheelMeshes[i] != null)
                {
                    SetWheelSync(i, wheelMeshes[i].rotation);
                }
            }

            syncDrifting = prometeo != null && prometeo.isDrifting;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            EnsureVisualReferences();
            VerifyNetworkTransform();

            if (isServer) return;

            if (prometeo != null)
            {
                prometeo.enabled = false;
                prometeo.useExternalInput = false;
            }

            foreach (Rigidbody rigidbody in GetComponentsInChildren<Rigidbody>(true))
            {
                rigidbody.isKinematic = true;
            }

            foreach (WheelCollider wheelCollider in GetComponentsInChildren<WheelCollider>(true))
            {
                wheelCollider.enabled = false;
            }

            NetworkTransformBase networkTransform = GetComponentInChildren<NetworkTransformBase>(true);
            if (networkTransform != null)
            {
                networkTransform.interpolatePosition = true;
                networkTransform.interpolateRotation = true;
            }

            Debug.Log($"[Vehicle] Client tarafinda aktif (netId={netId}), konum={transform.position}");
        }

        public bool HasFreeSeat(out int seatIndex)
        {
            for (int i = 0; i < occupants.Count; i++)
            {
                if (occupants[i] == EmptySeat)
                {
                    seatIndex = i;
                    return true;
                }
            }

            seatIndex = -1;
            return false;
        }

        public Transform GetSeatTransform(int seatIndex)
        {
            if (seatIndex < 0 || seatIndex >= seats.Length)
                return null;

            return seats[seatIndex];
        }

        public bool IsPlayerSeated(PlayerController player)
        {
            return player != null && GetSeatIndexOf(player) >= 0;
        }

        public bool IsDriver(PlayerController player)
        {
            return player != null && GetSeatIndexOf(player) == DriverSeatIndex;
        }

        public int GetSeatIndexOf(PlayerController player)
        {
            if (player == null)
                return -1;

            for (int i = 0; i < occupants.Count; i++)
            {
                if (occupants[i] == player.netId)
                    return i;
            }

            return -1;
        }

        [Server]
        public bool ServerTryEnter(PlayerController player, out int seatIndex)
        {
            seatIndex = -1;

            if (player == null) return false;
            if (player.IsInVehicle) return false;
            if (IsPlayerSeated(player)) return false;
            if (!HasFreeSeat(out seatIndex)) return false;

            occupants[seatIndex] = player.netId;
            player.ServerSetVehicle(this, seatIndex);
            return true;
        }

        [Server]
        public bool ServerExit(PlayerController player)
        {
            if (player == null) return false;

            int index = GetSeatIndexOf(player);
            if (index < 0) return false;

            occupants[index] = EmptySeat;

            if (index == DriverSeatIndex)
            {
                ServerSetDriverInput(false, false, false, false, false);
            }

            float exitYaw = transform.eulerAngles.y;
            Vector3 exitPos = transform.position + transform.right * exitOffsetDistance;

            player.ServerExitVehicle(exitPos, exitYaw);
            return true;
        }

        public void ServerSetDriverInput(bool throttle, bool reverse, bool left, bool right, bool handbrake)
        {
            driverThrottle = throttle;
            driverReverse = reverse;
            driverLeft = left;
            driverRight = right;
            driverHandbrake = handbrake;
        }

        public void Interact(GameObject interactor)
        {
            if (!isServer) return;

            PlayerController player = interactor != null ? interactor.GetComponent<PlayerController>() : null;
            if (player == null) return;

            if (player.IsInVehicle)
            {
                player.ServerRequestExit();
            }
            else
            {
                player.ServerRequestEnter(this);
            }
        }

        private void Update()
        {
            if (isServer)
            {
                ApplyDriverInput();
                EjectDeadOccupants();
            }
            else
            {
                SmoothClientTransform();
            }
        }

        private void FixedUpdate()
        {
            if (!isServer) return;

            for (int i = 0; i < wheelMeshes.Length && i < WheelCount; i++)
            {
                Transform wheel = wheelMeshes[i];
                if (wheel == null) continue;

                if (Quaternion.Angle(GetWheelSync(i), wheel.rotation) > wheelSyncDeadband)
                {
                    SetWheelSync(i, wheel.rotation);
                }
            }

            if (prometeo != null && prometeo.isDrifting != syncDrifting)
            {
                syncDrifting = prometeo.isDrifting;
            }
        }

        private void SmoothClientTransform()
        {
            for (int i = 0; i < wheelMeshes.Length && i < WheelCount; i++)
            {
                Transform wheel = wheelMeshes[i];
                if (wheel == null) continue;

                wheel.rotation = Quaternion.Slerp(wheel.rotation, GetWheelSync(i), Time.deltaTime * wheelRotationSmoothing);
            }
        }

        private void OnDriftingChanged(bool oldValue, bool newValue)
        {
            if (isServer) return;

            for (int i = 0; i < wheelSmokeParticles.Length; i++)
            {
                ParticleSystem particle = wheelSmokeParticles[i];
                if (particle == null) continue;

                if (newValue) particle.Play();
                else particle.Stop();
            }
        }

        private Quaternion GetWheelSync(int index)
        {
            switch (index)
            {
                case 0: return syncWheel0;
                case 1: return syncWheel1;
                case 2: return syncWheel2;
                default: return syncWheel3;
            }
        }

        private void SetWheelSync(int index, Quaternion rotation)
        {
            switch (index)
            {
                case 0: syncWheel0 = rotation; break;
                case 1: syncWheel1 = rotation; break;
                case 2: syncWheel2 = rotation; break;
                default: syncWheel3 = rotation; break;
            }
        }

        private void EnsureVisualReferences()
        {
            if (prometeo == null) return;

            if (wheelMeshes == null || wheelMeshes.Length == 0)
            {
                wheelMeshes = new Transform[WheelCount];
                if (prometeo.frontLeftMesh != null) wheelMeshes[0] = prometeo.frontLeftMesh.transform;
                if (prometeo.frontRightMesh != null) wheelMeshes[1] = prometeo.frontRightMesh.transform;
                if (prometeo.rearLeftMesh != null) wheelMeshes[2] = prometeo.rearLeftMesh.transform;
                if (prometeo.rearRightMesh != null) wheelMeshes[3] = prometeo.rearRightMesh.transform;
            }

            if (wheelSmokeParticles == null || wheelSmokeParticles.Length == 0)
            {
                wheelSmokeParticles = new ParticleSystem[2];
                wheelSmokeParticles[0] = prometeo.RLWParticleSystem;
                wheelSmokeParticles[1] = prometeo.RRWParticleSystem;
            }
        }

        private void VerifyNetworkTransform()
        {
            if (!requireNetworkTransform) return;

            if (GetComponentInChildren<NetworkTransformBase>(true) == null)
            {
                Debug.LogWarning("[Vehicle] ROOT'A NetworkTransformReliable eklenmemis: client'in araç transform'u asla gelmeyecek! (Prefab root > Add Component > NetworkTransformReliable)");
            }
        }

        private void OnValidate()
        {
            EnsureVisualReferences();
        }

        private void ApplyDriverInput()
        {
            if (prometeo == null || !prometeoExternalInputMode)
                return;

            prometeo.extThrottle = driverThrottle;
            prometeo.extReverse = driverReverse;
            prometeo.extLeft = driverLeft;
            prometeo.extRight = driverRight;
            prometeo.extHandbrake = driverHandbrake;
        }

        private void EjectDeadOccupants()
        {
            for (int i = occupants.Count - 1; i >= 0; i--)
            {
                if (occupants[i] == EmptySeat)
                    continue;

                if (!NetworkServer.spawned.TryGetValue(occupants[i], out NetworkIdentity identity) || identity == null)
                {
                    occupants[i] = EmptySeat;
                    continue;
                }

                PlayerController player = identity.GetComponent<PlayerController>();
                if (player != null && Health.IsDead(player.gameObject))
                {
                    ServerExit(player);
                }
            }
        }
    }
}