using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace IsometricShooter.Core
{
    public sealed class MeleeExecution
    {
        public bool IsActive { get; private set; }
        public bool ShowHitWindow { get; private set; }
        public Vector3 HitOrigin { get; private set; }
        public float HitRadius { get; private set; }

        public IReadOnlyList<Vector3> HitContacts => hitContacts;

        private readonly List<Vector3> hitContacts = new List<Vector3>();
        private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();

        private MeleeItemData data;
        private Transform selfTransform;
        private Transform weaponRoot;
        private Transform meleeHitPoint;
        private NetworkConnection shooterConnection;
        private float startTime;
        private float finishTime;

        public void Begin(MeleeItemData meleeData, Transform self, Transform rootModel, Transform hitPoint, NetworkConnection connection)
        {
            data = meleeData;
            selfTransform = self;
            weaponRoot = rootModel;
            meleeHitPoint = hitPoint;
            shooterConnection = connection;

            float startDelay = Mathf.Max(0f, meleeData.hitStartTime);
            float endDelay = Mathf.Max(startDelay + 0.01f, meleeData.hitEndTime);

            startTime = Time.time + startDelay;
            finishTime = startTime + (endDelay - startDelay);

            hitTargets.Clear();
            hitContacts.Clear();
            HitRadius = Mathf.Max(0.05f, meleeData.hitRadius);
            IsActive = true;
            ShowHitWindow = false;
        }

        public void Tick()
        {
            if (!IsActive)
                return;

            if (Time.time < startTime)
                return;

            ShowHitWindow = true;

            if (Time.time >= finishTime)
            {
                IsActive = false;
                ShowHitWindow = false;
                return;
            }

            Vector3 hitOrigin = ComputeHitOrigin();
            HitOrigin = hitOrigin;

            Collider[] hits = Physics.OverlapSphere(hitOrigin, HitRadius, ~0, QueryTriggerInteraction.Ignore);
            if (hits == null)
                return;

            foreach (Collider hit in hits)
            {
                if (hit == null)
                    continue;

                if (hit.transform.root == selfTransform.root)
                    continue;

                IDamageable damageable = hit.GetComponentInParent<IDamageable>();
                if (damageable == null)
                    continue;

                if (!hitTargets.Add(damageable))
                    continue;

                Vector3 toTarget = hit.transform.position - hitOrigin;
                Vector3 direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : selfTransform.forward;
                Vector3 contactPoint = hit.ClosestPoint(hitOrigin);

                hitContacts.Add(contactPoint);

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
        }

        private Vector3 ComputeHitOrigin()
        {
            if (meleeHitPoint != null)
                return meleeHitPoint.position;

            if (weaponRoot != null)
                return weaponRoot.TransformPoint(data.hitPointOffset);

            return selfTransform.position + selfTransform.forward * (data.range * 0.5f);
        }

        private HitboxPart ResolveHitbox(Collider hit)
        {
            HitboxPart hitbox = hit.GetComponent<HitboxPart>() ?? hit.GetComponentInParent<HitboxPart>();

            if (hitbox == null)
            {
                HitboxPart[] childHitboxes = hit.GetComponentsInChildren<HitboxPart>();

                float minDistance = float.MaxValue;
                foreach (HitboxPart hb in childHitboxes)
                {
                    float distance = Vector3.Distance(hit.ClosestPoint(selfTransform.position), hb.transform.position);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        hitbox = hb;
                    }
                }
            }

            return hitbox;
        }
    }
}