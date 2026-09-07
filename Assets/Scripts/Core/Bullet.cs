using UnityEngine;
using Mirror;

namespace IsometricShooter.Core
{
    public enum ImpactType { None, Wood, Brick, Metal, Enemy }

    [RequireComponent(typeof(Collider))]
    public class Bullet : NetworkBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float speed = 40f;
        [SerializeField] private float lifetime = 5f;

        [Header("Damage")]
        [SerializeField] private float damage = 25f;

        [Header("Collision")]
        [SerializeField] private LayerMask collisionLayer = ~0;

        [Header("Impact Particles")]
        [SerializeField] private GameObject woodParticle;
        [SerializeField] private GameObject brickParticle;
        [SerializeField] private GameObject metalParticle;
        [SerializeField] private GameObject enemyParticle;

        [Header("Debug")]
        [SerializeField] private bool showDebugRay = true;
        [SerializeField] private float debugRayLength = 3f;

        private const float MinDirectionSqrMagnitude = 0.0001f;
        private const float DestroyDelay = 0.1f;
        private const float ParticleDestroyExtra = 0.1f;
        private const float FallbackDestroyDelay = 5f;

        [Header("Damage Impact")]
        [SerializeField] private float impactForce = 20f;

        [SyncVar] private Vector3 syncDirection;
        [SyncVar] private float syncSpeed;

        private GameObject owner;
        private bool hasHit;
        private float spawnTime;

        private void Start()
        {
            spawnTime = Time.time;

            if (isServer)
            {
                if (syncDirection == Vector3.zero)
                {
                    syncDirection = transform.forward;
                    syncSpeed = speed;
                }
            }
        }

        public void Initialize(Vector3 shootDirection, float bulletSpeed, GameObject bulletOwner, NetworkConnection conn)
        {
            if (shootDirection.sqrMagnitude < MinDirectionSqrMagnitude)
            {
                shootDirection = transform.forward;
            }

            syncDirection = shootDirection.normalized;
            syncSpeed = bulletSpeed;
            owner = bulletOwner;

            transform.rotation = Quaternion.LookRotation(syncDirection, Vector3.up);

            if (isServer)
            {
                Invoke(nameof(ServerDestroy), lifetime);
            }
        }

        private void ServerDestroy()
        {
            if (hasHit)
                return;

            hasHit = true;
            RpcHideBullet();
            Invoke(nameof(DelayedNetworkDestroy), DestroyDelay);
        }

        private void DelayedNetworkDestroy()
        {
            if (gameObject != null)
            {
                NetworkServer.Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (hasHit)
                return;

            if (Time.time - spawnTime >= lifetime)
            {
                if (isServer)
                {
                    ServerDestroy();
                }
                else
                {
                    hasHit = true;
                    Destroy(gameObject);
                }
                return;
            }

            if (syncDirection == Vector3.zero)
                return;

            float distance = syncSpeed * Time.deltaTime;

            if (isServer)
            {
                Vector3 startPosition = transform.position;

                if (Physics.Raycast(startPosition, syncDirection, out RaycastHit hit, distance, collisionLayer, QueryTriggerInteraction.Collide))
                {
                    if (!ShouldIgnoreCollider(hit.collider))
                    {
                        HandleHit(hit.point, hit.normal, hit.collider);
                        return;
                    }
                }

                transform.position = startPosition + syncDirection * distance;

                if (showDebugRay)
                {
                    Debug.DrawRay(startPosition, syncDirection * debugRayLength, Color.red, Time.deltaTime);
                }
            }
            else
            {
                transform.position += syncDirection * distance;
            }
        }

        private bool ShouldIgnoreCollider(Collider collider)
        {
            if (collider == null)
                return true;

            if (collider.transform == transform || collider.transform.IsChildOf(transform))
                return true;

            if (owner != null)
            {
                Transform hitTransform = collider.transform;
                if (hitTransform == owner.transform || hitTransform.IsChildOf(owner.transform))
                    return true;
            }

            return false;
        }

        private void HandleHit(Vector3 hitPosition, Vector3 hitNormal, Collider hitCollider)
        {
            if (hasHit)
                return;

            hasHit = true;
            transform.position = hitPosition;

            ApplyDamage(hitCollider);

            ImpactType particleType = GetImpactType(hitCollider);
            RpcSpawnImpact(hitPosition, hitNormal, particleType);

            RpcHideBullet();
            Invoke(nameof(DelayedNetworkDestroy), DestroyDelay);
        }

        [ClientRpc]
        private void RpcHideBullet()
        {
            hasHit = true;

            MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
            foreach (var r in renderers)
            {
                r.enabled = false;
            }

            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
            }
        }

        private void ApplyDamage(Collider hitCollider)
        {
            if (hitCollider == null)
                return;

            HitboxPart hitbox = hitCollider.GetComponent<HitboxPart>() ?? hitCollider.GetComponentInParent<HitboxPart>();

            if (hitbox == null)
            {
                Vector3 hitPositionTemp = transform.position;
                HitboxPart[] childHitboxes = hitCollider.GetComponentsInChildren<HitboxPart>();

                float minDistance = float.MaxValue;
                foreach (HitboxPart hb in childHitboxes)
                {
                    float dist = Vector3.Distance(hitPositionTemp, hb.transform.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        hitbox = hb;
                    }
                }
            }

            if (hitbox != null && IsDamageableTarget(hitCollider))
            {
                Vector3 hitPosition = transform.position;

                Collider hitboxCollider = hitbox.GetComponent<Collider>();

                if (hitboxCollider != null)
                {
                    hitPosition = hitboxCollider.ClosestPoint(transform.position);
                }

                hitbox.OnHit(damage, syncDirection, hitPosition, impactForce, connectionToClient);
            }
        }

        private bool IsDamageableTarget(Collider collider)
        {
            if (collider == null)
                return false;

            if (collider.CompareTag("Player") || collider.CompareTag("Enemy"))
                return true;

            Transform current = collider.transform.parent;
            while (current != null)
            {
                if (current.CompareTag("Player") || current.CompareTag("Enemy"))
                    return true;
                current = current.parent;
            }

            return false;
        }

        private ImpactType GetImpactType(Collider collider)
        {
            if (collider == null)
                return ImpactType.Metal;

            string materialName = null;

            Transform current = collider.transform;
            while (current != null)
            {
                ImpactType tagType = GetImpactTypeFromTag(current.tag);
                if (tagType != ImpactType.None)
                    return tagType;

                Renderer renderer = current.GetComponent<Renderer>();
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    materialName = renderer.sharedMaterial.name;
                    break;
                }

                current = current.parent;
            }

            if (string.IsNullOrEmpty(materialName) &&
                collider.TryGetComponent<Renderer>(out Renderer colliderRenderer) &&
                colliderRenderer.sharedMaterial != null)
            {
                materialName = colliderRenderer.sharedMaterial.name;
            }

            if (string.IsNullOrEmpty(materialName))
                return ImpactType.Metal;

            if (materialName.IndexOf("wood", System.StringComparison.OrdinalIgnoreCase) >= 0) return ImpactType.Wood;
            if (materialName.IndexOf("brick", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                materialName.IndexOf("stone", System.StringComparison.OrdinalIgnoreCase) >= 0) return ImpactType.Brick;
            if (materialName.IndexOf("metal", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                materialName.IndexOf("iron", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                materialName.IndexOf("steel", System.StringComparison.OrdinalIgnoreCase) >= 0) return ImpactType.Metal;

            return ImpactType.Metal;
        }

        private static ImpactType GetImpactTypeFromTag(string tag)
        {
            switch (tag)
            {
                case "Wood": return ImpactType.Wood;
                case "Brick": return ImpactType.Brick;
                case "Metal": return ImpactType.Metal;
                case "Enemy":
                case "Player": return ImpactType.Enemy;
                default: return ImpactType.None;
            }
        }

        [ClientRpc]
        private void RpcSpawnImpact(Vector3 position, Vector3 normal, ImpactType impactType)
        {
            GameObject particlePrefab = impactType switch
            {
                ImpactType.Wood => woodParticle,
                ImpactType.Brick => brickParticle,
                ImpactType.Enemy => enemyParticle,
                _ => metalParticle
            };

            if (particlePrefab == null)
            {
                particlePrefab = metalParticle ?? woodParticle ?? brickParticle;
                if (particlePrefab == null) return;
            }

            Quaternion rotation = Quaternion.LookRotation(normal, Vector3.up);
            GameObject particle = Instantiate(particlePrefab, position, rotation);

            ParticleSystem particleSystem = particle.GetComponent<ParticleSystem>();
            if (particleSystem != null)
            {
                var main = particleSystem.main;
                float destroyTime = main.duration + main.startLifetime.constantMax + ParticleDestroyExtra;
                Destroy(particle, destroyTime);
            }
            else
            {
                Destroy(particle, FallbackDestroyDelay);
            }
        }
    }
}
