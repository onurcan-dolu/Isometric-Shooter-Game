using UnityEngine;
using System.Collections;

namespace IsometricShooter.Core
{
    public class WeaponReferancer : MonoBehaviour
    {
        [Header("Weapon References")]
        [SerializeField] private Transform pivot;
        [SerializeField] private Transform aimPoint;
        [SerializeField] private Transform firePoint;
        [SerializeField] private ParticleSystem muzzleFlash;
        [SerializeField] private Light muzzleLight;
        [SerializeField] private float lightDuration = 0.05f;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip shootSound;

        public Transform Pivot => pivot != null ? pivot : transform;
        public Transform AimPoint => aimPoint != null ? aimPoint : Pivot;
        public Transform FirePoint => firePoint != null ? firePoint : AimPoint;
        public ParticleSystem MuzzleFlash => muzzleFlash;
        public Light MuzzleLight => muzzleLight;
        public float LightDuration => lightDuration;
        public AudioSource AudioSource => audioSource;
        public AudioClip ShootSound => shootSound;

        private void Awake()
        {
            AutoFill();
        }

        public void AutoFill()
        {
            if (pivot == null) pivot = transform;
            if (aimPoint == null) aimPoint = FindDeepChild("Muzzle");
            if (firePoint == null)
            {
                firePoint = FindDeepChild("FirePoint") ?? FindDeepChild("BulletSpawnPoint");
            }
            if (firePoint == null && aimPoint != null)
            {
                firePoint = aimPoint.childCount > 0 ? aimPoint.GetChild(0) : aimPoint;
            }
            if (firePoint == null) firePoint = Pivot;
            if (muzzleFlash == null) muzzleFlash = GetComponentInChildren<ParticleSystem>(true);
            if (muzzleLight == null) muzzleLight = GetComponentInChildren<Light>(true);
            if (audioSource == null) audioSource = GetComponentInChildren<AudioSource>(true);
            if (audioSource == null) audioSource = GetComponentInParent<AudioSource>();
        }

        public void PlayMuzzleFlash()
        {
            if (muzzleLight != null)
            {
                StopAllCoroutines();
                StartCoroutine(FlashLightRoutine());
            }
            else if (muzzleFlash != null)
            {
                muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                muzzleFlash.Play();
            }
        }

        public void PlayShootSound()
        {
            if (audioSource != null && shootSound != null && !audioSource.isPlaying)
            {
                audioSource.PlayOneShot(shootSound);
            }
        }

        private IEnumerator FlashLightRoutine()
        {
            if (muzzleFlash != null)
            {
                muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                muzzleFlash.Play();
            }
            muzzleLight.enabled = true;
            yield return new WaitForSeconds(lightDuration);
            muzzleLight.enabled = false;
        }

        private Transform FindDeepChild(string name)
        {
            foreach (Transform child in transform)
            {
                if (child.name == name) return child;
                Transform deeper = RecursiveFind(child, name);
                if (deeper != null) return deeper;
            }
            return null;
        }

        private Transform RecursiveFind(Transform current, string name)
        {
            foreach (Transform child in current)
            {
                if (child.name == name) return child;
                Transform deeper = RecursiveFind(child, name);
                if (deeper != null) return deeper;
            }
            return null;
        }
    }
}