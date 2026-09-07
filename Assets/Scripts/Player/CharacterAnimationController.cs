using UnityEngine;
using Mirror;

namespace IsometricShooter.Player
{
    public class CharacterAnimationController : NetworkBehaviour
    {
        private static readonly int AnimForward = Animator.StringToHash("Forward");
        private static readonly int AnimLeft = Animator.StringToHash("Left");
        private static readonly int AnimWalk = Animator.StringToHash("Walk");
        private static readonly int AnimSprint = Animator.StringToHash("Sprint");
        private static readonly int AnimCrouch = Animator.StringToHash("Crouch");

        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private PlayerController characterController;

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

        private void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();

            if (characterController == null)
                characterController = GetComponent<PlayerController>();
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
