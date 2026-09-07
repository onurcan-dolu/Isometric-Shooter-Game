using UnityEngine;
using Mirror;

namespace IsometricShooter.Core
{
    public enum BodyPart { Head, Body, Limb }
    public enum HitSoundType { Headshot, Body, Limb }

    public class HitboxPart : NetworkBehaviour
    {
        [Header("Hitbox Settings")]
        [SerializeField] private BodyPart bodyPart = BodyPart.Body;

        [Header("Hit Sounds")]
        [SerializeField] private AudioClip headshotSound;
        [SerializeField] private AudioClip bodyShotSound;
        [SerializeField] private AudioClip limbShotSound;
        [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

        [Header("Damage Popup UI")]
        [SerializeField] private GameObject damagePopupPrefab;

        private AudioSource audioSource;
        private IDamageable damageable;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 0f;
                audioSource.volume = .2f;
            }

            damageable = GetComponent<IDamageable>() ?? GetComponentInParent<IDamageable>();
        }

        public void OnHit(float baseDamage, Vector3 hitDirection, Vector3 hitPosition, float impactForce, NetworkConnection shooterConnection)
        {
            float multiplier = bodyPart switch
            {
                BodyPart.Head => 2.0f,
                BodyPart.Body => 1.5f,
                _ => 1.0f
            };

            float finalDamage = baseDamage * multiplier;

            if (damageable != null)
            {
                damageable.TakeDamage(finalDamage, hitDirection, hitPosition, impactForce);
            }

            HitSoundType soundType = bodyPart switch
            {
                BodyPart.Head => HitSoundType.Headshot,
                BodyPart.Body => HitSoundType.Body,
                _ => HitSoundType.Limb
            };

            DamagePopupType popupType = bodyPart switch
            {
                BodyPart.Head => DamagePopupType.Headshot,
                BodyPart.Body => DamagePopupType.Body,
                _ => DamagePopupType.Normal
            };

            if (shooterConnection != null)
            {
                TargetTriggerHitFeedback(shooterConnection, soundType, finalDamage, hitPosition, popupType);
            }
        }

        [TargetRpc]
        private void TargetTriggerHitFeedback(NetworkConnection target, HitSoundType soundType, float damageAmount, Vector3 hitPosition, DamagePopupType popupType)
        {
            AudioClip clipToPlay = soundType switch
            {
                HitSoundType.Headshot => headshotSound,
                HitSoundType.Body => bodyShotSound,
                _ => limbShotSound
            };

            if (clipToPlay != null && audioSource != null)
            {
                audioSource.PlayOneShot(clipToPlay, soundVolume);
            }

            if (damagePopupPrefab != null)
            {
                GameObject popupObj = Instantiate(damagePopupPrefab, hitPosition + Vector3.up * 0.2f, Quaternion.identity);
                DamagePopup popup = popupObj.GetComponent<DamagePopup>();
                if (popup != null)
                {
                    popup.Setup(damageAmount, popupType);
                }
            }
        }
    }
}
