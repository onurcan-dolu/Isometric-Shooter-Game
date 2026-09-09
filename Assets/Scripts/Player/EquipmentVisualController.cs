using UnityEngine;
using Mirror;
using System.Collections.Generic;
using UnityEngine.Animations.Rigging;
using IsometricShooter.Core;

namespace IsometricShooter.Player
{
    public class EquipmentVisualController : NetworkBehaviour
    {
        [Header("Mount")]
        [SerializeField] private Transform weaponMount;
        [SerializeField] private Transform handGrip;

        [Header("Rig IK (auto-found if empty)")]
        [SerializeField] private Rig[] ikRigs;

        [Header("Hand IK Targets (auto-found by name if empty)")]
        [SerializeField] private Transform rightHandIKTarget;
        [SerializeField] private Transform leftHandIKTarget;

        [Header("References")]
        [SerializeField] private WeaponController weaponController;
        [SerializeField] private ShootingController shootingController;
        [SerializeField] private WorldCrosshair crosshair;

        private readonly Dictionary<GameObject, GameObject> modelPool = new Dictionary<GameObject, GameObject>();
        private readonly Dictionary<GameObject, GripRefs> gripCache = new Dictionary<GameObject, GripRefs>();
        private GameObject activeModel;
        private Transform rightGrip;
        private Transform leftGrip;
        private ItemData lastItem;
        private ItemPickup lastWorldPickup;
        private bool hasInitialItemState;
        private bool awaitingWorldPickup;
        private float weaponPoseBlend;

        public GameObject ActiveEquipModel => activeModel;
        public Transform WeaponMount => weaponMount;
        public Transform HandGrip => handGrip;

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (weaponController == null)
                weaponController = GetComponent<WeaponController>();

            if (shootingController == null)
                shootingController = GetComponent<ShootingController>();

            if (crosshair == null)
                crosshair = GetComponent<WorldCrosshair>();

            if (ikRigs == null || ikRigs.Length == 0)
                ikRigs = FindAllRigs();

            if (rightHandIKTarget == null)
                rightHandIKTarget = FindTransformByName(transform, "RightHandIK_target");
            if (leftHandIKTarget == null)
                leftHandIKTarget = FindTransformByName(transform, "LeftHandIK_target");

            if (handGrip == null && weaponMount != null)
                handGrip = weaponMount;

            if (weaponController != null)
                weaponController.OnItemChanged += OnItemChanged;

            Invoke(nameof(ApplyInitialItemState), 0f);
        }

        private void ApplyInitialItemState()
        {
            OnItemChanged();
        }

        private void RetryItemState()
        {
            OnItemChanged();
        }

        private void OnDestroy()
        {
            if (weaponController != null)
                weaponController.OnItemChanged -= OnItemChanged;

            foreach (GameObject model in modelPool.Values)
            {
                if (model != null)
                    Destroy(model);
            }

            modelPool.Clear();
            gripCache.Clear();
        }

        private void OnItemChanged()
        {
            OnItemChanged(false);
        }

        private void OnItemChanged(bool force)
        {
            ItemData item = weaponController != null ? weaponController.CurrentItem : null;
            ItemPickup worldPickup = weaponController != null ? weaponController.EquippedWorldPickup : null;

            if (item != null && worldPickup == null)
            {
                if (!awaitingWorldPickup)
                {
                    awaitingWorldPickup = true;
                    Invoke(nameof(RetryItemState), 0.1f);
                    return;
                }

                awaitingWorldPickup = false;
            }
            else
            {
                awaitingWorldPickup = false;
            }

            if (!force && hasInitialItemState && item == lastItem && worldPickup == lastWorldPickup)
                return;

            hasInitialItemState = true;
            lastItem = item;
            lastWorldPickup = worldPickup;

            foreach (GameObject model in modelPool.Values)
            {
                if (model != null)
                    model.SetActive(false);
            }

            if (activeModel != null)
            {
                if (activeModel.GetComponent<ItemPickup>() == null)
                {
                    activeModel.SetActive(false);
                }
                activeModel = null;
            }

            rightGrip = null;
            leftGrip = null;

            if (item != null && worldPickup != null)
            {
                activeModel = worldPickup.gameObject;

                Renderer[] renderers = activeModel.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                    renderers[i].enabled = true;

                if (item is WeaponData weaponData)
                {
                    if (weaponMount != null)
                    {
                        activeModel.transform.SetParent(weaponMount, true);
                        activeModel.transform.localPosition = weaponData.idlePosition;
                        activeModel.transform.localRotation = Quaternion.Euler(weaponData.idleRotation);
                    }

                    weaponPoseBlend = 0f;
                }
                else if (item is MeleeItemData meleeData)
                {
                    Transform grip = handGrip != null ? handGrip : weaponMount;
                    if (grip != null)
                    {
                        activeModel.transform.SetParent(grip, true);
                        activeModel.transform.localPosition = meleeData.handPositionOffset;
                        activeModel.transform.localRotation = Quaternion.Euler(meleeData.handRotationOffset);
                    }
                }

                BindPickupGrips();
            }
            else if (item != null)
            {
                GameObject prefab = item.GetEquipModelPrefab();
                if (prefab != null && weaponMount != null)
                {
                    if (!modelPool.TryGetValue(prefab, out activeModel) || activeModel == null)
                    {
                        activeModel = Instantiate(prefab, weaponMount.position, weaponMount.rotation);
                        activeModel.transform.SetParent(weaponMount, true);
                        DisablePhysics(activeModel);
                        modelPool[prefab] = activeModel;
                    }

                    activeModel.SetActive(true);

                    if (item is WeaponData weaponData)
                    {
                        activeModel.transform.SetParent(weaponMount, true);
                        activeModel.transform.localPosition = weaponData.idlePosition;
                        activeModel.transform.localRotation = Quaternion.Euler(weaponData.idleRotation);
                        weaponPoseBlend = 0f;

                        GripRefs grips = GetGripRefs(activeModel);
                        rightGrip = grips.right;
                        leftGrip = grips.left;
                    }
                    else if (item is MeleeItemData meleeData)
                    {
                        Transform grip = handGrip != null ? handGrip : weaponMount;
                        activeModel.transform.SetParent(grip, false);
                        activeModel.transform.localPosition = meleeData.handPositionOffset;
                        activeModel.transform.localRotation = Quaternion.Euler(meleeData.handRotationOffset);
                    }
                }
            }

            if (shootingController != null)
            {
                WeaponReferancer referancer = activeModel != null
                    ? activeModel.GetComponentInChildren<WeaponReferancer>(true)
                    : null;
                shootingController.SetWeaponReferancer(referancer);
            }

            if (crosshair != null)
            {
                WeaponReferancer referancer = activeModel != null
                    ? activeModel.GetComponentInChildren<WeaponReferancer>(true)
                    : null;
                crosshair.SetWeaponReferancer(referancer);
            }

            UpdateRigsActive(item is WeaponData);
            UpdateCrosshair(item);
        }

        private void BindPickupGrips()
        {
            if (activeModel == null)
                return;

            if (weaponController != null && weaponController.CurrentWeapon != null)
            {
                GripRefs grips = GetGripRefs(activeModel);
                rightGrip = grips.right;
                leftGrip = grips.left;
            }
        }

        private void LateUpdate()
        {
            if (activeModel == null &&
                weaponController != null &&
                weaponController.CurrentItem != null)
            {
                OnItemChanged(true);
                if (activeModel == null)
                    return;
            }

            if (activeModel == null)
                return;

            if (weaponController != null && weaponController.CurrentWeapon is WeaponData weaponData)
            {
                bool wantAim = shootingController != null && shootingController.IsAimingVisual;
                float targetBlend = wantAim ? 1f : 0f;
                weaponPoseBlend = Mathf.MoveTowards(weaponPoseBlend, targetBlend, weaponData.aimTransitionSpeed * Time.deltaTime);

                activeModel.transform.localPosition = Vector3.Lerp(weaponData.idlePosition, weaponData.aimPosition, weaponPoseBlend);
                activeModel.transform.localRotation = Quaternion.Euler(Vector3.Lerp(weaponData.idleRotation, weaponData.aimRotation, weaponPoseBlend));
            }
            else if (weaponController != null && weaponController.CurrentItem is MeleeItemData meleeData)
            {
                activeModel.transform.localPosition = meleeData.handPositionOffset;
                activeModel.transform.localRotation = Quaternion.Euler(meleeData.handRotationOffset);
            }

            if (rightHandIKTarget != null && rightGrip != null)
            {
                rightHandIKTarget.SetPositionAndRotation(rightGrip.position, rightGrip.rotation);
            }

            if (leftHandIKTarget != null && leftGrip != null)
            {
                leftHandIKTarget.SetPositionAndRotation(leftGrip.position, leftGrip.rotation);
            }
        }

        private Rig[] FindAllRigs()
        {
            List<Rig> rigs = new List<Rig>();
            CollectRigs(transform, rigs);
            return rigs.ToArray();
        }

        private static void CollectRigs(Transform root, List<Rig> rigs)
        {
            if (root == null)
                return;

            Rig rig = root.GetComponent<Rig>();
            if (rig != null)
                rigs.Add(rig);

            for (int i = 0; i < root.childCount; i++)
            {
                CollectRigs(root.GetChild(i), rigs);
            }
        }

        private struct GripRefs
        {
            public Transform right;
            public Transform left;
        }

        private GripRefs GetGripRefs(GameObject modelRoot)
        {
            if (modelRoot == null)
                return default;

            if (gripCache.TryGetValue(modelRoot, out GripRefs cached))
                return cached;

            GripRefs refs = default;
            refs.right = FindTransformByName(modelRoot.transform, "Main_Grip");
            refs.left = FindTransformByName(modelRoot.transform, "Foregrip_Main");
            if (refs.left == null)
                refs.left = FindTransformByName(modelRoot.transform, "Foregrip_Switch");

            gripCache[modelRoot] = refs;
            return refs;
        }

        private static Transform FindTransformByName(Transform root, string name)
        {
            if (root == null)
                return null;

            if (root.name == name)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindTransformByName(root.GetChild(i), name);
                if (result != null)
                    return result;
            }

            return null;
        }

        private static void DisablePhysics(GameObject root)
        {
            if (root == null)
                return;

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }

            Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                rigidbodies[i].isKinematic = true;
                rigidbodies[i].useGravity = false;
            }
        }

        private void UpdateRigsActive(bool active)
        {
            if (ikRigs == null)
                return;

            float weight = active ? 1f : 0f;
            for (int i = 0; i < ikRigs.Length; i++)
            {
                if (ikRigs[i] != null)
                    ikRigs[i].weight = weight;
            }
        }

        private void UpdateCrosshair(ItemData item)
        {
            if (crosshair == null || !isLocalPlayer)
                return;

            crosshair.SetVisible(item is WeaponData);
        }
    }
}