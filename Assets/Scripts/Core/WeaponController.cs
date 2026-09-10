using UnityEngine;
using Mirror;
using System;
using System.Collections.Generic;
using IsometricShooter.Player;

namespace IsometricShooter.Core
{
    public class WeaponController : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private Inventory inventory;
        [SerializeField] private ShootingController shootingController;

        [Header("Drop")]
        [SerializeField] private ItemPickup dropPickupPrefab;
        [SerializeField] private float dropHeight = 0.6f;
        [SerializeField] private float dropForwardDistance = 1.2f;
        [SerializeField] private float dropThrowHeight = 1.8f;
        [SerializeField] private float dropAngularVelocity = 1200f;

        [SyncVar(hook = nameof(OnEquippedItemChanged))]
        private string equippedItemId;

        [SyncVar(hook = nameof(OnWorldPickupChanged))]
        private ItemPickup equippedWorldPickup;

        [SyncVar(hook = nameof(OnAmmoChanged))]
        private int currentAmmo;

        [SyncVar(hook = nameof(OnReserveAmmoChanged))]
        private int reserveAmmo;

        [SyncVar(hook = nameof(OnIsReloadingChanged))]
        private bool isReloading;

        private ItemData currentItemData;
        private float nextFireTime;
        private float nextMeleeTime;

        private readonly WeaponAmmoStore ammoStore = new WeaponAmmoStore();
        private readonly WeaponPickupRegistry pickupRegistry = new WeaponPickupRegistry();
        private readonly ReloadRuntime reloadRuntime = new ReloadRuntime();
        private readonly MeleeExecution meleeExecution = new MeleeExecution();
        private PlayerController cachedPlayerController;

        public event Action OnItemChanged;
        public event Action<int, int> OnAmmoUpdated;
        public event Action<bool> OnReloadStateChanged;
        public event Action OnMeleeAttack;

        public ItemData CurrentItem => currentItemData;
        public WeaponData CurrentWeapon => currentItemData as WeaponData;
        public MeleeItemData CurrentMelee => currentItemData as MeleeItemData;
        public string EquippedUniqueId => equippedItemId;
        public int CurrentAmmo => currentAmmo;
        public int ReserveAmmo => reserveAmmo;
        public bool IsReloading => isReloading;
        public ItemPickup EquippedWorldPickup => equippedWorldPickup;

        private bool IsPlayerInVehicle()
        {
            PlayerController playerController = GetComponent<PlayerController>();
            return playerController != null && playerController.IsInVehicle;
        }

        private void Awake()
        {
            cachedPlayerController = GetComponent<PlayerController>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (inventory == null)
                inventory = GetComponent<Inventory>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!isLocalPlayer) return;
            ResolveItemData();
            OnItemChanged?.Invoke();
            OnAmmoUpdated?.Invoke(currentAmmo, reserveAmmo);
        }

        private void Update()
        {
            if (isServer)
            {
                TickServer();
            }

            if (!isLocalPlayer) return;
            if (Health.IsDead(gameObject)) return;
            if (cachedPlayerController != null && cachedPlayerController.IsSprinting()) return;

            if (CurrentMelee != null)
            {
                if (Input.GetMouseButton(0))
                {
                    TryMeleeAttack();
                }
                return;
            }

            if (CurrentWeapon == null || isReloading)
                return;

            if (Input.GetMouseButton(0))
            {
                TryFire();
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                TryReload();
            }
        }

        private void TickServer()
        {
            if (reloadRuntime.IsActive && reloadRuntime.Tick())
            {
                ApplyCompletedReload();
            }

            meleeExecution.Tick();
        }

        private void TryFire()
        {
            WeaponData data = CurrentWeapon;
            if (data == null) return;
            if (currentAmmo <= 0) return;
            if (Time.time < nextFireTime) return;

            nextFireTime = Time.time + data.fireRate;

            if (shootingController != null)
            {
                shootingController.PerformFire(data);
            }
        }

        private void TryReload()
        {
            WeaponData data = CurrentWeapon;
            if (data == null) return;
            if (isReloading) return;
            if (!ReloadCalculator.CanReload(currentAmmo, reserveAmmo, data.magazineSize)) return;

            CmdStartReload();
        }

        [Command(requiresAuthority = true)]
        private void CmdStartReload()
        {
            if (IsPlayerInVehicle()) return;

            WeaponData data = CurrentWeapon;
            if (data == null) return;
            if (isReloading) return;
            if (!ReloadCalculator.CanReload(currentAmmo, reserveAmmo, data.magazineSize)) return;

            isReloading = true;
            reloadRuntime.Begin(data.reloadTime);
        }

        private void ApplyCompletedReload()
        {
            WeaponData data = CurrentWeapon;
            if (data == null)
            {
                isReloading = false;
                return;
            }

            ReloadCalculator.ReloadPlan plan = ReloadCalculator.Resolve(currentAmmo, reserveAmmo, data.magazineSize);
            currentAmmo = plan.magazineAmmo;
            reserveAmmo = plan.reserveAmmo;
            isReloading = false;
        }

        private void TryMeleeAttack()
        {
            MeleeItemData data = CurrentMelee;
            if (data == null) return;
            if (Time.time < nextMeleeTime) return;

            nextMeleeTime = Time.time + data.cooldown;

            CmdMeleeAttack();
        }

        [Command(requiresAuthority = true)]
        public void CmdEquipSlot(string uniqueId)
        {
            EquipSlot(uniqueId);
        }

        [Command(requiresAuthority = true)]
        public void CmdDropItem(string uniqueId)
        {
            ServerDropItem(uniqueId);
        }

        [Command(requiresAuthority = true)]
        private void CmdMeleeAttack()
        {
            if (IsPlayerInVehicle()) return;
            if (Health.IsDead(gameObject)) return;

            MeleeItemData data = CurrentMelee;
            if (data == null) return;

            RpcPlayMeleeSwing();

            meleeExecution.Begin(data, transform, GetWeaponRootForMelee(), GetMeleeHitPoint(), connectionToClient);
        }

        [Server]
        public void ServerDropItem(string uniqueId)
        {
            if (IsPlayerInVehicle()) return;
            if (inventory == null) return;
            if (string.IsNullOrEmpty(uniqueId)) return;

            InventorySlot slot = inventory.GetSlotByUniqueId(uniqueId);
            if (slot.IsEmpty) return;

            ItemData data = slot.ResolveItem();
            if (data == null) return;

            int amount = slot.amount;
            bool isEquippedDrop = string.Equals(equippedItemId, uniqueId);

            int savedAmmo = -1;
            int savedReserveAmmo = -1;
            if (data is WeaponData)
            {
                if (isEquippedDrop)
                {
                    savedAmmo = currentAmmo;
                    savedReserveAmmo = reserveAmmo;
                }
                else if (ammoStore.TryRestore(uniqueId, out savedAmmo, out savedReserveAmmo))
                {
                }
            }

            ItemPickup worldPickup = isEquippedDrop ? equippedWorldPickup : null;
            if (worldPickup == null)
            {
                pickupRegistry.TryGet(uniqueId, out worldPickup);
            }

            if (isEquippedDrop)
            {
                Unequip();
            }

            pickupRegistry.Remove(uniqueId);
            ammoStore.Remove(uniqueId);
            inventory.RemoveSlot(uniqueId, amount);

            Vector3 spawnPos = WeaponDropCalculator.ComputeSpawnPosition(transform, dropHeight, dropForwardDistance);
            Vector3 velocity = WeaponDropCalculator.ComputeDropVelocity(transform, dropThrowHeight);

            if (worldPickup != null)
            {
                equippedWorldPickup = null;
                worldPickup.ReleaseFromHand(spawnPos, Quaternion.identity, velocity, dropAngularVelocity);
                return;
            }

            if (dropPickupPrefab == null)
                return;

            ItemPickup pickup = CreatePickup(data, spawnPos);
            if (pickup == null)
                return;

            pickup.Initialize(data, amount, savedAmmo, savedReserveAmmo);
            NetworkServer.Spawn(pickup.gameObject);
            pickup.DropFromThrow(velocity, dropAngularVelocity);
        }

        private ItemPickup CreatePickup(ItemData data, Vector3 spawnPos)
        {
            GameObject modelPrefab = data != null ? data.GetWorldModelPrefab() : null;
            if (modelPrefab != null)
            {
                ItemPickup onModel = modelPrefab.GetComponent<ItemPickup>();
                if (onModel != null)
                {
                    return Instantiate(onModel, spawnPos, Quaternion.identity);
                }
            }

            if (dropPickupPrefab == null)
                return null;

            return Instantiate(dropPickupPrefab, spawnPos, Quaternion.identity);
        }

        [Server]
        private void EquipSlot(string uniqueId)
        {
            if (IsPlayerInVehicle()) return;
            if (inventory == null) return;

            if (string.IsNullOrEmpty(uniqueId) || string.Equals(equippedItemId, uniqueId))
            {
                Unequip();
                return;
            }

            InventorySlot slot = inventory.GetSlotByUniqueId(uniqueId);
            if (slot.IsEmpty)
            {
                Unequip();
                return;
            }

            ItemData data = slot.ResolveItem();
            if (data == null)
            {
                Unequip();
                return;
            }

            if (!(data is WeaponData || data is MeleeItemData))
            {
                return;
            }

            HideCurrentWorldPickup();

            if (!string.IsNullOrEmpty(equippedItemId))
            {
                ammoStore.Save(equippedItemId, currentAmmo, reserveAmmo);
            }

            equippedItemId = uniqueId;
            currentItemData = data;

            if (data is WeaponData weapon)
            {
                if (ammoStore.TryRestore(uniqueId, out int savedMagazine, out int savedReserve))
                {
                    currentAmmo = savedMagazine;
                    reserveAmmo = savedReserve;
                }
                else
                {
                    currentAmmo = weapon.magazineSize;
                    reserveAmmo = weapon.maxReserveAmmo;
                }
            }
            else
            {
                currentAmmo = 0;
                reserveAmmo = 0;
            }

            isReloading = false;
            reloadRuntime.Cancel();

            if (pickupRegistry.TryGet(uniqueId, out ItemPickup targetPickup))
            {
                equippedWorldPickup = targetPickup;
                targetPickup.EquipTo(GetEquipMount());
            }
        }

        private void HideCurrentWorldPickup()
        {
            if (equippedWorldPickup != null)
            {
                equippedWorldPickup.HideFromHand();
                equippedWorldPickup = null;
            }
        }

        [Server]
        private void Unequip()
        {
            if (string.IsNullOrEmpty(equippedItemId))
                return;

            ammoStore.Save(equippedItemId, currentAmmo, reserveAmmo);

            HideCurrentWorldPickup();

            equippedItemId = string.Empty;
            currentItemData = null;
            currentAmmo = 0;
            reserveAmmo = 0;
            isReloading = false;
            reloadRuntime.Cancel();
        }

        [Server]
        public void ConsumeServerAmmo()
        {
            if (currentAmmo > 0)
            {
                currentAmmo--;
            }
        }

        [Server]
        public void ServerFireFromAI()
        {
            WeaponData data = CurrentWeapon;
            if (data == null) return;
            if (currentAmmo <= 0) return;
            if (Time.time < nextFireTime) return;

            nextFireTime = Time.time + data.fireRate;

            if (shootingController != null)
            {
                shootingController.ServerPerformFire(data);
            }

            if (currentAmmo > 0)
            {
                currentAmmo--;
            }
        }

        [Server]
        public bool ServerTryPickupItem(ItemData data, int amount)
        {
            return ServerTryPickupItem(data, amount, -1, -1);
        }

        [Server]
        public bool ServerTryPickupItem(ItemData data, int amount, int savedAmmo, int savedReserveAmmo)
        {
            return ServerTryPickupItem(data, amount, savedAmmo, savedReserveAmmo, null);
        }

        [Server]
        public bool ServerTryPickupItem(ItemData data, int amount, int savedAmmo, int savedReserveAmmo, ItemPickup worldPickup)
        {
            if (IsPlayerInVehicle()) return false;
            if (data == null) return false;
            if (!(data is WeaponData || data is MeleeItemData)) return false;

            string uniqueId;
            if (inventory == null || !inventory.TryAddItem(data.itemId, amount, out uniqueId))
            {
                return false;
            }

            if (data is WeaponData && savedAmmo >= 0)
            {
                ammoStore.Save(uniqueId, savedAmmo, savedReserveAmmo);
            }

            if (string.IsNullOrEmpty(equippedItemId))
            {
                EquipSlot(uniqueId);
            }

            if (worldPickup != null)
            {
                pickupRegistry.Register(uniqueId, worldPickup);
                worldPickup.Initialize(data, amount, savedAmmo, savedReserveAmmo);

                if (string.Equals(equippedItemId, uniqueId))
                {
                    equippedWorldPickup = worldPickup;
                    worldPickup.EquipTo(GetEquipMount());
                }
                else
                {
                    worldPickup.HideFromHand();
                }
            }

            return true;
        }

        private Transform GetEquipMount()
        {
            EquipmentVisualController equipVisual = GetComponent<EquipmentVisualController>();
            Transform mount = equipVisual != null ? equipVisual.WeaponMount : null;

            if (equipVisual != null && CurrentMelee != null && equipVisual.HandGrip != null)
            {
                mount = equipVisual.HandGrip;
            }

            if (mount == null)
                mount = transform;

            return mount;
        }

        private Transform GetMeleeHitPoint()
        {
            MeleeReferancer referancer = GetComponentInChildren<MeleeReferancer>(true);
            if (referancer != null && referancer.HitPoint != null)
                return referancer.HitPoint;

            return null;
        }

        private Transform GetWeaponRootForMelee()
        {
            EquipmentVisualController equipVisual = GetComponent<EquipmentVisualController>();
            if (equipVisual != null && equipVisual.ActiveEquipModel != null)
                return equipVisual.ActiveEquipModel.transform;

            return null;
        }

        [ClientRpc]
        private void RpcPlayMeleeSwing()
        {
            OnMeleeAttack?.Invoke();
        }

        private void OnWorldPickupChanged(ItemPickup oldPickup, ItemPickup newPickup)
        {
            OnItemChanged?.Invoke();
        }

        private void OnEquippedItemChanged(string oldId, string newId)
        {
            ResolveItemData();
            OnItemChanged?.Invoke();
        }

        private void OnAmmoChanged(int oldAmmo, int newAmmo)
        {
            OnAmmoUpdated?.Invoke(currentAmmo, reserveAmmo);
        }

        private void OnReserveAmmoChanged(int oldReserve, int newReserve)
        {
            OnAmmoUpdated?.Invoke(currentAmmo, reserveAmmo);
        }

        private void OnIsReloadingChanged(bool oldVal, bool newVal)
        {
            OnReloadStateChanged?.Invoke(newVal);
        }

        private void ResolveItemData()
        {
            if (string.IsNullOrEmpty(equippedItemId))
            {
                currentItemData = null;
                return;
            }

            if (inventory != null)
            {
                InventorySlot slot = inventory.GetSlotByUniqueId(equippedItemId);
                if (!slot.IsEmpty)
                {
                    currentItemData = slot.ResolveItem();
                    return;
                }
            }

            currentItemData = null;
        }

        private void OnDrawGizmos()
        {
            if (meleeExecution != null && meleeExecution.ShowHitWindow)
            {
                Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.9f);
                Gizmos.DrawWireSphere(meleeExecution.HitOrigin, meleeExecution.HitRadius);

                Gizmos.color = new Color(1f, 0.9f, 0.2f, 1f);
                Gizmos.DrawWireCube(meleeExecution.HitOrigin, Vector3.one * meleeExecution.HitRadius * 0.5f);
            }

            if (meleeExecution == null)
                return;

            IReadOnlyList<Vector3> contacts = meleeExecution.HitContacts;
            for (int i = 0; i < contacts.Count; i++)
            {
                Gizmos.color = new Color(0.2f, 1f, 0.3f, 1f);
                Gizmos.DrawSphere(contacts[i], 0.08f);
                Gizmos.DrawLine(meleeExecution.HitOrigin, contacts[i]);
            }
        }
    }
}