using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using IsometricShooter.Core;

namespace IsometricShooter.AI
{
    [RequireComponent(typeof(AICharacterController))]
    [RequireComponent(typeof(AIShootingController))]
    [RequireComponent(typeof(Health))]
    public class AISimpleBehaviour : MonoBehaviour
    {
        [Header("Detection & Combat Settings")]
        [SerializeField] private float detectionRange = 20f;
        [SerializeField] private float fireRange = 15f;
        [SerializeField] private float fieldOfViewAngle = 120f;
        [SerializeField] private LayerMask enemyLayer;
        [SerializeField] private LayerMask obstacleLayer;
        [SerializeField] private Vector3 bodyOffset = new Vector3(0f, 1f, 0f);
        [SerializeField] private float headshotChance = 0.25f;

        [Header("Pathfinding & Patrol Settings")]
        [SerializeField] private float pathUpdateInterval = 0.5f;
        [SerializeField] private float waypointReachedTolerance = 1.5f;
        [SerializeField] private List<GameObject> patrolWaypoints = new List<GameObject>();
        [SerializeField] private float minWaitTime = 1f;
        [SerializeField] private float maxWaitTime = 3f;

        private AICharacterController moveController;
        private AIShootingController shootController;
        private Health healthComponent;

        private Transform currentTarget;
        private float pathUpdateTimer;

        private int currentWaypointIndex = 0;
        private bool hasPatrolTarget = false;
        private bool isWaitingAtWaypoint = false;
        private float waitTimer = 0f;
        private float targetWaitDuration = 0f;
        private Collider[] overlapBuffer = new Collider[32];
        private Dictionary<Transform, Health> cachedTargetHealth = new Dictionary<Transform, Health>();

        private const float HeadshotHeightOffset = 1.6f;
        private const float RayOriginHeightOffset = 1.2f;

        private void Awake()
        {
            moveController = GetComponent<AICharacterController>();
            shootController = GetComponent<AIShootingController>();
            healthComponent = GetComponent<Health>();
        }

        private void Update()
        {
            if (healthComponent != null && healthComponent.IsDead())
            {
                moveController.SetMovementDirection(Vector3.zero);
                if (shootController != null)
                    shootController.enabled = false;

                enabled = false;
                return;
            }

            FindTarget();

            if (currentTarget != null)
            {
                hasPatrolTarget = false;
                isWaitingAtWaypoint = false;
                HandleCombatAndMovement();
            }
            else
            {
                if (shootController != null)
                {
                    shootController.SetAimState(false, Vector3.zero);
                }

                HandlePatrol();
            }
        }
        public void SetPatrolWaypoints(List<GameObject> waypoints)
        {
            patrolWaypoints = waypoints;
        }
        private void FindTarget()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, detectionRange, overlapBuffer, enemyLayer);
            float closestDistance = Mathf.Infinity;
            Transform bestTarget = null;

            for (int i = 0; i < count; i++)
            {
                Collider hit = overlapBuffer[i];
                if (hit.gameObject == gameObject)
                    continue;

                if (hit.transform.root.CompareTag(transform.tag))
                    continue;

                Health targetHealth;
                if (!cachedTargetHealth.TryGetValue(hit.transform, out targetHealth))
                {
                    targetHealth = hit.GetComponent<Health>();
                    cachedTargetHealth[hit.transform] = targetHealth;
                }

                if (targetHealth != null && targetHealth.IsDead())
                    continue;

                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (dist < closestDistance)
                {
                    Vector3 dirToTarget = (hit.transform.position - transform.position).normalized;
                    float angle = Vector3.Angle(transform.forward, dirToTarget);

                    if (angle <= fieldOfViewAngle * 0.5f)
                    {
                        Vector3 targetAimPos = hit.transform.position + bodyOffset;
                        if (HasLineOfSightToTarget(targetAimPos, dist))
                        {
                            closestDistance = dist;
                            bestTarget = hit.transform;
                        }
                    }
                }
            }

            currentTarget = bestTarget;
        }
        private void HandleCombatAndMovement()
        {
            if (currentTarget == null) return;

            Health targetHealth = currentTarget.GetComponent<Health>();
            if (targetHealth != null && targetHealth.IsDead())
            {
                currentTarget = null;
                return;
            }

            Vector3 targetAimPos = currentTarget.position + bodyOffset;
            if (Random.value < headshotChance)
            {
                targetAimPos = currentTarget.position + Vector3.up * HeadshotHeightOffset;
            }

            shootController.SetAimState(true, targetAimPos);

            float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);
            bool canSeeTarget = HasLineOfSightToTarget(targetAimPos, distanceToTarget);

            if (distanceToTarget <= fireRange && canSeeTarget)
            {
                moveController.SetMovementDirection(Vector3.zero);
                moveController.SetTargetPosition(currentTarget.position);

                Vector3 shootDir = (targetAimPos - transform.position).normalized;
                shootController.TryShoot(shootDir);
            }
            else
            {
                pathUpdateTimer += Time.deltaTime;
                if (pathUpdateTimer >= pathUpdateInterval)
                {
                    pathUpdateTimer = 0f;
                    CalculatePathAndMove(currentTarget.position);
                }
            }
        }

        private void HandlePatrol()
        {
            if (patrolWaypoints == null || patrolWaypoints.Count == 0)
            {
                moveController.SetMovementDirection(Vector3.zero);
                return;
            }

            if (isWaitingAtWaypoint)
            {
                moveController.SetMovementDirection(Vector3.zero);
                waitTimer += Time.deltaTime;

                if (waitTimer >= targetWaitDuration)
                {
                    isWaitingAtWaypoint = false;
                    SelectRandomWaypoint();
                }
                return;
            }

            if (!hasPatrolTarget)
            {
                SelectRandomWaypoint();
            }

            Vector3 targetPosition = patrolWaypoints[currentWaypointIndex].transform.position;

            if (Vector3.Distance(transform.position, targetPosition) <= waypointReachedTolerance)
            {
                moveController.SetMovementDirection(Vector3.zero);
                isWaitingAtWaypoint = true;
                waitTimer = 0f;
                targetWaitDuration = Random.Range(minWaitTime, maxWaitTime);
                return;
            }

            pathUpdateTimer += Time.deltaTime;
            if (pathUpdateTimer >= pathUpdateInterval)
            {
                pathUpdateTimer = 0f;
                CalculatePathAndMove(targetPosition);
            }
        }

        private void SelectRandomWaypoint()
        {
            if (patrolWaypoints.Count == 1)
            {
                currentWaypointIndex = 0;
            }
            else
            {
                int newIndex;
                do
                {
                    newIndex = Random.Range(0, patrolWaypoints.Count);
                } while (newIndex == currentWaypointIndex);

                currentWaypointIndex = newIndex;
            }

            hasPatrolTarget = true;
        }

        private void CalculatePathAndMove(Vector3 destination)
        {
            NavMeshPath path = new NavMeshPath();
            if (NavMesh.CalculatePath(transform.position, destination, NavMesh.AllAreas, path))
            {
                if (path.corners.Length > 1)
                {
                    Vector3 nextCorner = path.corners[1];
                    Vector3 moveDir = (nextCorner - transform.position).normalized;

                    moveController.SetMovementDirection(moveDir);
                    moveController.SetTargetPosition(nextCorner);
                }
            }
        }

        private bool HasLineOfSightToTarget(Vector3 targetPos, float distance)
        {
            Vector3 origin = transform.position + Vector3.up * RayOriginHeightOffset;
            Vector3 dir = (targetPos - origin).normalized;

            if (Physics.Raycast(origin, dir, out RaycastHit hit, distance, obstacleLayer))
            {
                if (hit.collider.transform.root != transform.root)
                {
                    return false;
                }
            }
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, detectionRange);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, fireRange);

            if (patrolWaypoints != null && patrolWaypoints.Count > 0)
            {
                Gizmos.color = Color.blue;
                foreach (var wp in patrolWaypoints)
                {
                    if (wp != null)
                    {
                        Gizmos.DrawSphere(wp.transform.position, 0.4f);
                    }
                }
            }
        }
    }
}
