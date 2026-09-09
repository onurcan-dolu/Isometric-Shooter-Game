using UnityEngine;
using Mirror;
using System;
using System.Collections;
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
        private Coroutine reloadCoroutine;

        private readonly List<Vector3> meleeHitContacts = new List<Vector3>();
        private Vector3 meleeHitOrigin;
        private float meleeHitRadius;
        private bool meleeHitWindowActive;

        [NonSerialized] private readonly Dictionary<string, AmmoState> weaponAmmoStates = new Dictionary<string, AmmoState>();
        private readonly Dictionary<string, ItemPickup> slotPickups = new Dictionary<string, ItemPickup>();

        public event Action OnItemChanged;
        public event Action<int, int> OnAmmoUpdated;
        public event Action<bool> OnReloadStateChanged;

        public ItemData CurrentItem => currentItemData;
        public WeaponData CurrentWeapon => currentItemData as WeaponData;
        public MeleeItemData CurrentMelee => currentItemData as MeleeItemData;
        public string EquippedUniqueId => equippedItemId;
        public int CurrentAmmo => currentAmmo;
        public int ReserveAmmo => reserveAmmo;
        public bool IsReloading => isReloading;
        public ItemPickup EquippedWorldPickup => equippedWorldPickup;

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
            if (!isLocalPlayer) return;
            if (Health.IsDead(gameObject)) return;

            if (CurrentMelee != null)
            {
                UpdateMelee();
                return;
            }

            if (CurrentWeapon == null) return;
            if (isReloading) return;

            if (Input.GetMouseButton(0))
            {
                TryFire();
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                TryReload();
            }
        }

        private void UpdateMelee()
        {
            if (Input.GetMouseButton(0))
            {
                TryMeleeAttack();
            }
        }

        private void TryMeleeAttack()
        {
            MeleeItemData data = CurrentMelee;
            if (data == null) return;
            if (Time.time < nextMeleeTime) return;

            nextMeleeTime = Time.time + data.cooldown;

            CmdMeleeAttack();
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
            if (currentAmmo >= data.magazineSize) return;
            if (reserveAmmo <= 0) return;

            CmdStartReload();
        }

        [Command(requiresAuthority = true)]
        private void CmdStartReload()
        {
            if (isReloading) return;
            if (CurrentWeapon == null) return;
            if (currentAmmo >= CurrentWeapon.magazineSize) return;
            if (reserveAmmo <= 0) return;

            isReloading = true;
            StartCoroutine(ReloadRoutine());
        }

        private IEnumerator ReloadRoutine()
        {
            WeaponData data = CurrentWeapon;
            if (data == null)
            {
                isReloading = false;
                yield break;
            }

            yield return new WaitForSeconds(data.reloadTime);

            int needed = data.magazineSize - currentAmmo;
            int toLoad = Mathf.Min(needed, reserveAmmo);

            currentAmmo += toLoad;
            reserveAmmo -= toLoad;
            isReloading = false;
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

        [Server]
        public void ServerDropItem(string uniqueId)
        {
            if (inventory == null) return;
            if (string.IsNullOrEmpty(uniqueId)) return;

            InventorySlot slot = inventory.GetSlotByUniqueId(uniqueId);
            if (slot.IsEmpty) return;

            ItemData data = slot.ResolveItem();
            if (data == null) return;

            int amount = slot.amount;

            int savedAmmo = -1;
            int savedReserveAmmo = -1;
            if (data is WeaponData)
            {
                if (string.Equals(equippedItemId, uniqueId))
                {
                    savedAmmo = currentAmmo;
                    savedReserveAmmo = reserveAmmo;
                }
                else if (weaponAmmoStates.TryGetValue(uniqueId, out AmmoState storedState))
                {
                    savedAmmo = storedState.current;
                    savedReserveAmmo = storedState.reserve;
                }
            }

            bool isEquippedDrop = string.Equals(equippedItemId, uniqueId);
            ItemPickup worldPickup = isEquippedDrop ? equippedWorldPickup : null;

            if (isEquippedDrop)
            {
                Unequip();
            }

            if (worldPickup == null)
            {
                slotPickups.TryGetValue(uniqueId, out worldPickup);
            }
            slotPickups.Remove(uniqueId);

            weaponAmmoStates.Remove(uniqueId);
            inventory.RemoveSlot(uniqueId, amount);

            Vector3 origin = transform.position;
            origin.y += dropHeight;
            Vector3 forward = transform.forward;
            Vector3 spawnPos = origin + (forward * dropForwardDistance);

            Vector3 velocity = forward * 2f + Vector3.up * MathsDropSpeed();

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
                    ItemPickup pickup = Instantiate(onModel, spawnPos, Quaternion.identity);
                    return pickup;
                }
            }

            if (dropPickupPrefab == null)
                return null;

            ItemPickup dropPickup = Instantiate(dropPickupPrefab, spawnPos, Quaternion.identity);
            return dropPickup;
        }

        private float MathsDropSpeed()
        {
            float t = Mathf.Sqrt((2f * dropThrowHeight) / 9.81f);
            return 9.81f * t;
        }

        [Server]
        private void EquipSlot(string uniqueId)
        {
            if (inventory == null) return;

            if (string.Equals(equippedItemId, uniqueId))
            {
                Unequip();
                return;
            }

            if (string.IsNullOrEmpty(uniqueId))
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
                weaponAmmoStates[equippedItemId] = new AmmoState { current = currentAmmo, reserve = reserveAmmo };
            }

            equippedItemId = uniqueId;
            currentItemData = data;

            AmmoState state;
            if (data is WeaponData weaponData && weaponAmmoStates.TryGetValue(uniqueId, out state))
            {
                currentAmmo = state.current;
                reserveAmmo = state.reserve;
            }
            else if (data is WeaponData weaponData2)
            {
                currentAmmo = weaponData2.magazineSize;
                reserveAmmo = weaponData2.maxReserveAmmo;
            }
            else
            {
                currentAmmo = 0;
                reserveAmmo = 0;
            }

            isReloading = false;

            if (reloadCoroutine != null)
            {
                StopCoroutine(reloadCoroutine);
                reloadCoroutine = null;
            }

            if (slotPickups.TryGetValue(uniqueId, out ItemPickup targetPickup) && targetPickup != null)
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

            weaponAmmoStates[equippedItemId] = new AmmoState { current = currentAmmo, reserve = reserveAmmo };

            HideCurrentWorldPickup();

            equippedItemId = string.Empty;
            currentItemData = null;
            currentAmmo = 0;
            reserveAmmo = 0;
            isReloading = false;

            if (reloadCoroutine != null)
            {
                StopCoroutine(reloadCoroutine);
                reloadCoroutine = null;
            }
        }

        [Command(requiresAuthority = true)]
        private void CmdMeleeAttack()
        {
            if (Health.IsDead(gameObject)) return;

            MeleeItemData data = CurrentMelee;
            if (data == null) return;

            RpcPlayMeleeSwing();

            NetworkConnectionToClient shooterConnection = connectionToClient;
            StartCoroutine(SwingDamageRoutine(data, shooterConnection));
        }

        private IEnumerator SwingDamageRoutine(MeleeItemData data, NetworkConnectionToClient shooterConnection)
        {
            float startDelay = Mathf.Max(0f, data.hitStartTime);
            float endDelay = Mathf.Max(startDelay + 0.01f, data.hitEndTime);

            yield return new WaitForSeconds(startDelay);

            float endTime = Time.time + (endDelay - startDelay);

            float radius = Mathf.Max(0.05f, data.hitRadius);
            HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();

            Transform weaponRoot = GetWeaponRootForMelee();
            Transform meleeHitPoint = GetMeleeHitPoint();

            meleeHitOrigin = transform.position;
            meleeHitRadius = radius;
            meleeHitContacts.Clear();
            meleeHitWindowActive = true;

            while (Time.time < endTime)
            {
                Vector3 hitOrigin;

                if (meleeHitPoint != null)
                {
                    hitOrigin = meleeHitPoint.position;
                }
                else if (weaponRoot != null)
                {
                    hitOrigin = weaponRoot.TransformPoint(data.hitPointOffset);
                }
                else
                {
                    hitOrigin = transform.position + transform.forward * (data.range * 0.5f);
                }

                meleeHitOrigin = hitOrigin;

                Collider[] hits = Physics.OverlapSphere(hitOrigin, radius, ~0, QueryTriggerInteraction.Ignore);

                foreach (Collider hit in hits)
                {
                    if (hit.transform.root == transform.root)
                        continue;

                    IDamageable damageable = hit.GetComponentInParent<IDamageable>();
                    if (damageable == null)
                        continue;

                    if (!hitTargets.Add(damageable))
                        continue;

                    Vector3 toTarget = hit.transform.position - hitOrigin;
                    Vector3 direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : transform.forward;
                    Vector3 contactPoint = hit.ClosestPoint(hitOrigin);

                    meleeHitContacts.Add(contactPoint);

                    HitboxPart hitbox = ResolveHitbox(hit);
                    if (hitbox != null)
                    {
                        hitbox.OnHit(data.damage, direction, contactPoint, data.impactForce, shooterConnection);
                    }
                    else
                    {
                        damageable.TakeDamage(data.damage, direction, contactPoint, data.impactForce);
                    }
                }

                yield return new WaitForEndOfFrame();
            }

            meleeHitWindowActive = false;
        }

        private HitboxPart ResolveHitbox(Collider hitCollider)
        {
            if (hitCollider == null)
                return null;

            HitboxPart hitbox = hitCollider.GetComponent<HitboxPart>() ?? hitCollider.GetComponentInParent<HitboxPart>();

            if (hitbox == null)
            {
                HitboxPart[] childHitboxes = hitCollider.GetComponentsInChildren<HitboxPart>();

                float minDistance = float.MaxValue;
                foreach (HitboxPart hb in childHitboxes)
                {
                    float dist = Vector3.Distance(hitCollider.ClosestPoint(transform.position), hb.transform.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        hitbox = hb;
                    }
                }
            }

            return hitbox;
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

        public event Action OnMeleeAttack;

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
            if (CurrentWeapon == null) return;
            if (currentAmmo <= 0) return;
            if (Time.time < nextFireTime) return;

            nextFireTime = Time.time + CurrentWeapon.fireRate;

            if (shootingController != null)
            {
                shootingController.ServerPerformFire(CurrentWeapon);
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
            if (data == null) return false;
            if (!(data is WeaponData || data is MeleeItemData)) return false;

            string uniqueId;
            if (inventory != null && inventory.TryAddItem(data.itemId, amount, out uniqueId))
            {
                if (data is WeaponData && savedAmmo >= 0)
                {
                    weaponAmmoStates[uniqueId] = new AmmoState { current = savedAmmo, reserve = savedReserveAmmo };
                }

                if (string.IsNullOrEmpty(equippedItemId))
                {
                    EquipSlot(uniqueId);
                }

                if (worldPickup != null)
                {
                    slotPickups[uniqueId] = worldPickup;
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

            return false;
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
            if (meleeHitWindowActive)
            {
                Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.9f);
                Gizmos.DrawWireSphere(meleeHitOrigin, meleeHitRadius);

                Gizmos.color = new Color(1f, 0.9f, 0.2f, 1f);
                Gizmos.DrawWireCube(meleeHitOrigin, Vector3.one * meleeHitRadius * 0.5f);
            }

            for (int i = 0; i < meleeHitContacts.Count; i++)
            {
                Gizmos.color = new Color(0.2f, 1f, 0.3f, 1f);
                Gizmos.DrawSphere(meleeHitContacts[i], 0.08f);
                Gizmos.DrawLine(meleeHitOrigin, meleeHitContacts[i]);
            }
        }
    }

    [Serializable]
    public struct AmmoState
    {
        public int current;
        public int reserve;
    }
}