using UnityEngine;
using Mirror;

namespace IsometricShooter.Core
{
    public class ItemPickup : NetworkBehaviour, IInteractable
    {
        [Header("Item")]
        [SerializeField] private ItemData itemData;
        [SerializeField] private int amount = 1;

        [Header("Layer")]
        [SerializeField] private string lootLayerName = "Loot";

        [SyncVar] private string syncItemId;
        [SyncVar] private Vector3 netPosition;
        [SyncVar] private Quaternion netRotation;
        [SyncVar(hook = nameof(OnNetThrownChanged))] private bool netThrown;

        private Rigidbody rigidbodyCache;
        private Transform equippedMount;
        private int savedAmmo = -1;
        private int savedReserveAmmo = -1;
        private float lastDropWatchLog;

        public string InteractionPrompt => ResolveItemData() != null ? ResolveItemData().itemName : "Item";

        private void Update()
        {
            if (isServer)
            {
                if (netThrown)
                {
                    if (rigidbodyCache != null && rigidbodyCache.isKinematic)
                    {
                        rigidbodyCache.isKinematic = false;
                        rigidbodyCache.useGravity = true;
                        rigidbodyCache.WakeUp();

                        if (Time.time - lastDropWatchLog > 1f)
                        {
                            lastDropWatchLog = Time.time;
                            Debug.Log($"[PickupDrop][Watch] force released netId={netId} pos={transform.position}");
                        }
                    }

                    netPosition = transform.position;
                    netRotation = transform.rotation;
                }

                return;
            }

            if (netThrown)
            {
                transform.SetPositionAndRotation(netPosition, netRotation);
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (string.IsNullOrEmpty(syncItemId) && itemData != null)
            {
                syncItemId = itemData.itemId;
            }

            rigidbodyCache = GetComponent<Rigidbody>();
            if (rigidbodyCache != null)
            {
                rigidbodyCache.isKinematic = true;
            }

            ApplyLootLayer();
        }

        private void ApplyLootLayer()
        {
            int layer = LayerMask.NameToLayer(lootLayerName);
            if (layer < 0)
                return;

            SetLayerRecursively(gameObject, layer);
            RpcApplyLootLayer(layer);
        }

        [ClientRpc]
        private void RpcApplyLootLayer(int layer)
        {
            if (isServer)
                return;

            SetLayerRecursively(gameObject, layer);
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                transforms[i].gameObject.layer = layer;
            }
        }

        public ItemData ResolveItemData()
        {
            if (itemData != null)
                return itemData;

            return ItemData.GetItem(syncItemId);
        }

        [Server]
        public void Initialize(ItemData data, int dropAmount, int ammo = -1, int reserveAmmo = -1)
        {
            itemData = data;
            amount = dropAmount;
            syncItemId = data != null ? data.itemId : string.Empty;
            savedAmmo = ammo;
            savedReserveAmmo = reserveAmmo;
        }

        [Server]
        public void DropFromThrow(Vector3 velocity, float angularVelocity)
        {
            transform.SetParent(null, true);

            rigidbodyCache = GetComponent<Rigidbody>();
            if (rigidbodyCache == null)
            {
                rigidbodyCache = gameObject.AddComponent<Rigidbody>();
                rigidbodyCache.mass = 1f;
                rigidbodyCache.drag = 0.5f;
                rigidbodyCache.angularDrag = 0.5f;
            }

            rigidbodyCache.isKinematic = false;
            rigidbodyCache.useGravity = true;
            rigidbodyCache.velocity = velocity;
            rigidbodyCache.angularVelocity = Random.onUnitSphere * angularVelocity;

            netPosition = transform.position;
            netRotation = transform.rotation;
            netThrown = true;
            isPickedUp = false;

            if (isServer)
            {
                Debug.Log($"[PickupDrop][Throw] netId={netId} kinematic={rigidbodyCache.isKinematic} gravity={rigidbodyCache.useGravity} vel={rigidbodyCache.velocity.magnitude} pos={transform.position}");
            }
        }

        private void OnNetThrownChanged(bool oldThrown, bool newThrown)
        {
            if (newThrown && !isServer)
            {
                transform.SetParent(null, true);
            }
        }

        [Server]
        public void EquipTo(Transform mount)
        {
            equippedMount = mount;

            if (netThrown)
            {
                rigidbodyCache = GetComponent<Rigidbody>();
                if (rigidbodyCache != null)
                {
                    rigidbodyCache.isKinematic = true;
                    rigidbodyCache.velocity = Vector3.zero;
                    rigidbodyCache.angularVelocity = Vector3.zero;
                }
                netThrown = false;
            }

            if (mount != null)
            {
                transform.SetParent(mount, true);
                UpdateEquippedPose();
            }

            SetPickupPhysics(false);
            netPosition = transform.position;
            netRotation = transform.rotation;
            SetVisualVisible(true);
            RpcSetVisible(true);
        }

        private void UpdateEquippedPose()
        {
            if (equippedMount == null)
                return;

            ItemData data = ResolveItemData();
            if (data is MeleeItemData melee)
            {
                transform.SetPositionAndRotation(
                    equippedMount.TransformPoint(melee.handPositionOffset),
                    equippedMount.rotation * Quaternion.Euler(melee.handRotationOffset));
            }
            else
            {
                transform.SetPositionAndRotation(equippedMount.position, equippedMount.rotation);
            }
        }

        [Server]
        public void HideFromHand()
        {
            equippedMount = null;

            SetPickupPhysics(false);
            SetVisualVisible(false);
            RpcSetVisible(false);
        }

        [Server]
        public void ReleaseFromHand(Vector3 position, Quaternion rotation, Vector3 velocity, float angularVelocity)
        {
            equippedMount = null;
            isPickedUp = false;

            transform.SetParent(null, true);
            transform.SetPositionAndRotation(position, rotation);

            SetPickupPhysics(true);

            bool hadRigidbody = GetComponent<Rigidbody>() != null;

            rigidbodyCache = GetComponent<Rigidbody>();
            if (rigidbodyCache == null)
            {
                rigidbodyCache = gameObject.AddComponent<Rigidbody>();
                rigidbodyCache.mass = 1f;
                rigidbodyCache.drag = 0.5f;
                rigidbodyCache.angularDrag = 0.5f;
            }

            rigidbodyCache.isKinematic = false;
            rigidbodyCache.useGravity = true;
            rigidbodyCache.velocity = velocity;
            rigidbodyCache.angularVelocity = Random.onUnitSphere * angularVelocity;
            rigidbodyCache.WakeUp();

            netPosition = transform.position;
            netRotation = transform.rotation;
            netThrown = true;
            SetVisualVisible(true);
            RpcSetVisible(true);

            if (isServer)
            {
                Debug.Log($"[PickupDrop][Release] netId={netId} hadRb={hadRigidbody} kinematic={rigidbodyCache.isKinematic} gravity={rigidbodyCache.useGravity} vel={rigidbodyCache.velocity.magnitude} pos={transform.position}");
            }
        }

        private void SetPickupPhysics(bool enabled)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = enabled;
            }

            RpcSetPhysicsEnabled(enabled);
        }

        [ClientRpc]
        private void RpcSetPhysicsEnabled(bool enabled)
        {
            if (isServer)
                return;

            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = enabled;
            }
        }

        public virtual void Interact(GameObject interactor)
        {
            if (!isServer) return;
            if (isPickedUp) return;
            if (Health.IsDead(interactor)) return;

            ItemData data = ResolveItemData();
            if (data == null) return;

            Inventory inventory = interactor.GetComponent<Inventory>();
            if (inventory == null) return;

            bool added;

            if (data is WeaponData || data is MeleeItemData)
            {
                WeaponController weaponController = interactor.GetComponent<WeaponController>();
                added = weaponController != null && weaponController.ServerTryPickupItem(data, amount, savedAmmo, savedReserveAmmo, this);
            }
            else
            {
                added = inventory.TryAddItem(data, amount);
            }

            if (!added)
                return;

            isPickedUp = true;
        }

        [ClientRpc]
        public void RpcSetVisible(bool visible)
        {
            if (isServer)
                return;

            SetVisualVisible(visible);
        }

        private void SetVisualVisible(bool visible)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = visible;
            }
        }

        protected bool isPickedUp;
    }
}