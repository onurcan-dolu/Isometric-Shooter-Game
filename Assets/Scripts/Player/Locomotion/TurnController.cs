using UnityEngine;

namespace IsometricShooter.Player
{
    public class TurnController : MonoBehaviour
    {
        private static readonly int AnimTurnLeft90 = Animator.StringToHash("TurnLeft90");
        private static readonly int AnimTurnRight90 = Animator.StringToHash("TurnRight90");
        private static readonly int AnimTurnLeft180 = Animator.StringToHash("TurnLeft180");
        private static readonly int AnimTurnRight180 = Animator.StringToHash("TurnRight180");

        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private ShootingController shootingController;

        [Header("Turn Thresholds")]
        [SerializeField] private bool enableTurnAnimations = true;
        [SerializeField] private float turnAngleThreshold = 80f;
        [SerializeField] private float turn180Threshold = 150f;

        [Header("Timing")]
        [SerializeField] private float turn90Duration = 0.6f;
        [SerializeField] private float turn180Duration = 0.9f;
        [SerializeField] private float turnCooldown = 0.3f;
        [SerializeField] private float idleGracePeriod = 0.2f;
        [SerializeField] private float decisionTime = 0.18f;

        private float lastTurnEndTime;
        private float lastMovementEndTime;
        private float turnStartTime;
        private float turnDuration;
        private float startYaw;
        private float turnDelta;
        private bool isTurning;

        private bool deciding;
        private float decideStartTime;
        private float maxDeltaDuringDecide;
        private bool decideLeft;

        public bool BlocksRotation => isTurning;
        public bool ManagesIdleRotation => enableTurnAnimations;
        public bool IsTurning => isTurning;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();

            if (playerController == null)
                playerController = GetComponent<PlayerController>();

            if (shootingController == null)
                shootingController = GetComponent<ShootingController>();
        }

        private void Update()
        {
            if (!enableTurnAnimations)
                return;

            if (playerController == null || !playerController.isLocalPlayer)
                return;

            if (playerController.IsInVehicle)
                return;

            if (!playerController.IsIdleStanding())
            {
                lastMovementEndTime = Time.time;
                isTurning = false;
                CancelDecision();
                return;
            }

            if (isTurning)
            {
                UpdateTurnRotation();
                return;
            }

            if (Time.time - lastTurnEndTime < turnCooldown)
                return;

            if (Time.time - lastMovementEndTime < idleGracePeriod)
                return;

            if (deciding)
            {
                UpdateDecision();
                return;
            }

            TryStartDecision();
        }

        private void TryStartDecision()
        {
            if (shootingController == null)
                return;

            float bodyYaw = transform.eulerAngles.y;
            float aimYaw = GetAimYaw();
            float delta = Mathf.DeltaAngle(bodyYaw, aimYaw);
            float absDelta = Mathf.Abs(delta);

            if (absDelta < turnAngleThreshold)
                return;

            deciding = true;
            decideStartTime = Time.time;
            maxDeltaDuringDecide = absDelta;
            decideLeft = delta < 0f;
        }

        private void UpdateDecision()
        {
            float elapsed = Time.time - decideStartTime;

            float bodyYaw = transform.eulerAngles.y;
            float aimYaw = GetAimYaw();
            float delta = Mathf.DeltaAngle(bodyYaw, aimYaw);
            float absDelta = Mathf.Abs(delta);

            if (absDelta < turnAngleThreshold)
            {
                CancelDecision();
                return;
            }

            if (absDelta > maxDeltaDuringDecide)
            {
                maxDeltaDuringDecide = absDelta;
                decideLeft = delta < 0f;
            }

            if (elapsed < decisionTime)
                return;

            FireTurn(bodyYaw, maxDeltaDuringDecide, decideLeft);
            CancelDecision();
        }

        private void FireTurn(float bodyYaw, float absDelta, bool left)
        {
            float sign = left ? -1f : 1f;
            bool is180 = absDelta >= turn180Threshold;

            int trigger = is180
                ? (left ? AnimTurnLeft180 : AnimTurnRight180)
                : (left ? AnimTurnLeft90 : AnimTurnRight90);

            if (animator != null)
            {
                ResetTurnTriggers();
                animator.SetTrigger(trigger);
            }

            isTurning = true;
            startYaw = bodyYaw;
            turnDelta = sign * Mathf.Min(absDelta, is180 ? 180f : 90f);
            turnStartTime = Time.time;

            float normalizedDelta = Mathf.Abs(turnDelta) / 90f;
            turnDuration = is180
                ? turn180Duration * Mathf.Clamp(normalizedDelta / 2f, 0.5f, 1f)
                : turn90Duration * Mathf.Clamp(normalizedDelta, 0.5f, 1.5f);
        }

        private void CancelDecision()
        {
            deciding = false;
            maxDeltaDuringDecide = 0f;
        }

        private void UpdateTurnRotation()
        {
            float progress = Mathf.Clamp01((Time.time - turnStartTime) / turnDuration);

            float eased = progress * progress * (3f - 2f * progress);
            float targetYaw = startYaw + turnDelta * eased;

            transform.rotation = Quaternion.Euler(0f, targetYaw, 0f);

            if (progress >= 1f)
                FinishTurn();
        }

        private void FinishTurn()
        {
            if (isTurning)
                lastTurnEndTime = Time.time;

            isTurning = false;
        }

        private float GetAimYaw()
        {
            Vector3 offset = shootingController.GetAimWorldPosition() - transform.position;
            offset.y = 0f;

            if (offset.sqrMagnitude < 0.001f)
                return transform.eulerAngles.y;

            return Quaternion.LookRotation(offset).eulerAngles.y;
        }

        private void ResetTurnTriggers()
        {
            animator.ResetTrigger(AnimTurnLeft90);
            animator.ResetTrigger(AnimTurnRight90);
            animator.ResetTrigger(AnimTurnLeft180);
            animator.ResetTrigger(AnimTurnRight180);
        }
    }
}
