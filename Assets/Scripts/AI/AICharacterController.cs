using UnityEngine;

namespace IsometricShooter.AI
{
    [RequireComponent(typeof(Rigidbody))]
    public class AICharacterController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private LayerMask groundLayer;

        private Rigidbody rb;
        private Vector3 moveDirection;
        private Quaternion targetRotation;

        private const float MinRotationSqrMagnitude = 0.001f;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.freezeRotation = true;
        }

        private void Update()
        {
            ApplySmoothRotation();
        }

        private void FixedUpdate()
        {
            Move();
        }

        public void SetMovementDirection(Vector3 direction)
        {
            moveDirection = direction.normalized;
        }

        public void SetTargetPosition(Vector3 targetPos)
        {
            Vector3 lookDir = targetPos - transform.position;
            lookDir.y = 0f;

            if (lookDir.sqrMagnitude > MinRotationSqrMagnitude)
            {
                targetRotation = Quaternion.LookRotation(lookDir);
            }
        }

        private void Move()
        {
            Vector3 velocity = moveDirection * moveSpeed;

            rb.velocity = new Vector3(
                velocity.x,
                rb.velocity.y,
                velocity.z
            );
        }

        private void ApplySmoothRotation()
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }
}
