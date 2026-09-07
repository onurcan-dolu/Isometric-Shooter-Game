using UnityEngine;
using Mirror;

namespace IsometricShooter.AI
{
    public class AICharacterAnimationController : NetworkBehaviour
    {
        private static readonly int AnimForward = Animator.StringToHash("Forward");
        private static readonly int AnimLeft = Animator.StringToHash("Left");
        private static readonly int AnimWalk = Animator.StringToHash("Walk");
        private static readonly int AnimSprint = Animator.StringToHash("Sprint");
        private static readonly int AnimCrouch = Animator.StringToHash("Crouch");

        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private Rigidbody rb;

        [Header("Animation")]
        [SerializeField] private float animationSmoothSpeed = 10f;
        [SerializeField] private float maxSpeed = 4f;

        private const float MinMoveSqrMagnitude = 0.01f;

        [SyncVar] private float syncForward;
        [SyncVar] private float syncLeft;
        [SyncVar] private bool syncWalk;
        [SyncVar] private bool syncSprint;
        [SyncVar] private bool syncCrouch;

        private float currentForward;
        private float currentLeft;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();

            if (rb == null)
                rb = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            if (isServer)
            {
                Vector3 localVelocity = transform.InverseTransformDirection(rb.velocity);

                float targetForward = Mathf.Clamp(localVelocity.z / maxSpeed, -1f, 1f);
                float targetLeft = Mathf.Clamp(localVelocity.x / maxSpeed, -1f, 1f);

                bool walk = rb.velocity.sqrMagnitude > MinMoveSqrMagnitude;
                bool sprint = false;
                bool crouch = false;

                syncForward = targetForward;
                syncLeft = targetLeft;
                syncWalk = walk;
                syncSprint = sprint;
                syncCrouch = crouch;
            }

            ApplyAnimations();
        }

        private void ApplyAnimations()
        {
            if (animator == null)
                return;

            currentForward = Mathf.Lerp(
                currentForward,
                syncForward,
                animationSmoothSpeed * Time.deltaTime
            );

            currentLeft = Mathf.Lerp(
                currentLeft,
                syncLeft,
                animationSmoothSpeed * Time.deltaTime
            );

            animator.SetFloat(AnimForward, currentForward);
            animator.SetFloat(AnimLeft, currentLeft);

            animator.SetBool(AnimWalk, syncWalk);
            animator.SetBool(AnimSprint, syncSprint);
            animator.SetBool(AnimCrouch, syncCrouch);
        }
    }
}
