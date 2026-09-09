using UnityEngine;
using Mirror;
using IsometricShooter.Core;

namespace IsometricShooter.Player
{
    public class CharacterAnimationController : NetworkBehaviour
    {
        private static readonly int AnimForward = Animator.StringToHash("Forward");
        private static readonly int AnimLeft = Animator.StringToHash("Left");
        private static readonly int AnimWalk = Animator.StringToHash("Walk");
        private static readonly int AnimSprint = Animator.StringToHash("Sprint");
        private static readonly int AnimCrouch = Animator.StringToHash("Crouch");
        private static readonly int AnimHasWeapon = Animator.StringToHash("HasWeapon");
        private static readonly int AnimAiming = Animator.StringToHash("Aiming");
        private static readonly int AnimMeleeAttack = Animator.StringToHash("MeleeAttack");
        private static readonly int AnimWeaponType = Animator.StringToHash("WeaponType");

        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private Transform rigTarget;
        [SerializeField] private PlayerController characterController;
        [SerializeField] private WeaponController weaponController;
        [SerializeField] private ShootingController shootingController;

        [Header("Animation")]
        [SerializeField] private float animationSmoothSpeed = 10f;

        private const float MinMoveSqrMagnitude = 0.01f;

        [SyncVar] private float syncForward;
        [SyncVar] private float syncLeft;
        [SyncVar] private bool syncWalk;
        [SyncVar] private bool syncSprint;
        [SyncVar] private bool syncCrouch;

        private float currentForward;
        private float currentLeft;

        private float lastSentForward;
        private float lastSentLeft;
        private bool lastSentWalk;
        private bool lastSentSprint;
        private bool lastSentCrouch;

        private EquipmentState lastWeaponState;
        private float weaponTypeFloat;

        private struct EquipmentState
        {
            public int type;
            public bool aiming;
        }

        private void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();

            if (characterController == null)
                characterController = GetComponent<PlayerController>();

            if (weaponController == null)
                weaponController = GetComponent<WeaponController>();

            if (shootingController == null)
                shootingController = GetComponent<ShootingController>();
        }

        private void OnEnable()
        {
            if (weaponController != null)
            {
                weaponController.OnItemChanged += OnItemChanged;
                weaponController.OnMeleeAttack += OnMeleeAttack;
            }
        }

        private void OnDisable()
        {
            if (weaponController != null)
            {
                weaponController.OnItemChanged -= OnItemChanged;
                weaponController.OnMeleeAttack -= OnMeleeAttack;
            }
        }

        private void OnItemChanged()
        {
            ApplyWeaponState();
        }

        private void OnMeleeAttack()
        {
            if (animator == null)
                return;

            animator.SetTrigger(AnimMeleeAttack);
        }

        private void ApplyWeaponState()
        {
            if (animator == null || weaponController == null)
                return;

            ItemData item = weaponController.CurrentItem;

            int type = 0;
            if (item is WeaponData)
                type = 1;
            else if (item is MeleeItemData)
                type = 2;

            bool hasWeapon = item != null;

            animator.SetBool(AnimHasWeapon, hasWeapon);
            animator.SetFloat(AnimWeaponType, type);
        }

        private void Update()
        {
            if (isLocalPlayer)
            {
                Vector3 moveDirection = characterController.GetMoveDirection();
                Vector3 localMoveDirection = transform.InverseTransformDirection(moveDirection);

                float targetForward = Mathf.Clamp(localMoveDirection.z, -1f, 1f);
                float targetLeft = Mathf.Clamp(localMoveDirection.x, -1f, 1f);

                bool walk = moveDirection.sqrMagnitude > MinMoveSqrMagnitude &&
                            !characterController.IsRunning() &&
                            !characterController.IsCrouching();

                bool sprint = moveDirection.sqrMagnitude > MinMoveSqrMagnitude &&
                              characterController.IsRunning();

                bool crouch = characterController.IsCrouching();

                if (targetForward != lastSentForward || targetLeft != lastSentLeft || walk != lastSentWalk || sprint != lastSentSprint || crouch != lastSentCrouch)
                {
                    lastSentForward = targetForward;
                    lastSentLeft = targetLeft;
                    lastSentWalk = walk;
                    lastSentSprint = sprint;
                    lastSentCrouch = crouch;
                    CmdUpdateAnimationState(targetForward, targetLeft, walk, sprint, crouch);
                }

                bool aiming = shootingController != null && shootingController.IsAiming();
                int type = weaponController.CurrentItem is MeleeItemData ? 2 : (weaponController.CurrentItem is WeaponData ? 1 : 0);

                if (lastWeaponState.type != type || lastWeaponState.aiming != aiming)
                {
                    lastWeaponState.type = type;
                    lastWeaponState.aiming = aiming;
                    ApplyWeaponState();
                }

                if (animator != null)
                {
                    animator.SetBool(AnimAiming, aiming);
                }
            }
            if (rigTarget != null && shootingController != null)
            {
                Vector3 aimTarget = isLocalPlayer
                    ? shootingController.GetAimWorldPosition()
                    : shootingController.GetSyncedAimWorldPosition();

                rigTarget.position = Vector3.Lerp(rigTarget.position, aimTarget, Time.deltaTime * 60f);
            }

            ApplyAnimations();
        }

        [Command]
        private void CmdUpdateAnimationState(float forward, float left, bool walk, bool sprint, bool crouch)
        {
            syncForward = forward;
            syncLeft = left;
            syncWalk = walk;
            syncSprint = sprint;
            syncCrouch = crouch;
        }

        private void ApplyAnimations()
        {
            if (animator == null || characterController == null)
                return;

            float targetForward = isLocalPlayer ? Mathf.Clamp(transform.InverseTransformDirection(characterController.GetMoveDirection()).z, -1f, 1f) : syncForward;
            float targetLeft = isLocalPlayer ? Mathf.Clamp(transform.InverseTransformDirection(characterController.GetMoveDirection()).x, -1f, 1f) : syncLeft;

            currentForward = Mathf.Lerp(
                currentForward,
                targetForward,
                animationSmoothSpeed * Time.deltaTime
            );

            currentLeft = Mathf.Lerp(
                currentLeft,
                targetLeft,
                animationSmoothSpeed * Time.deltaTime
            );

            animator.SetFloat(AnimForward, currentForward);
            animator.SetFloat(AnimLeft, currentLeft);

            bool walkState = isLocalPlayer ? (characterController.GetMoveDirection().sqrMagnitude > MinMoveSqrMagnitude && !characterController.IsRunning() && !characterController.IsCrouching()) : syncWalk;
            bool sprintState = isLocalPlayer ? (characterController.GetMoveDirection().sqrMagnitude > MinMoveSqrMagnitude && characterController.IsRunning()) : syncSprint;
            bool crouchState = isLocalPlayer ? characterController.IsCrouching() : syncCrouch;

            animator.SetBool(AnimWalk, walkState);
            animator.SetBool(AnimSprint, sprintState);
            animator.SetBool(AnimCrouch, crouchState);
        }
    }
}
