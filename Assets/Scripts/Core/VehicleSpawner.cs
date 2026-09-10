using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace IsometricShooter.Core
{
    public class VehicleSpawner : MonoBehaviour
    {
        private static readonly HashSet<GameObject> registeredPrefabs = new HashSet<GameObject>();

        [Header("References")]
        [SerializeField] private Vehicle vehiclePrefab;

        [Header("Spawn")]
        [SerializeField] private Vector3 spawnPosition = new Vector3(0f, 0.5f, 10f);
        [SerializeField] private Quaternion spawnRotation = Quaternion.identity;

        private Vehicle spawnedInstance;

        private void Start()
        {
            if (vehiclePrefab == null)
            {
                Debug.LogWarning("[VehicleSpawner] vehiclePrefab atanmadi; araba spawn edilemiyor.", this);
                return;
            }

            if (vehiclePrefab.gameObject.scene.isLoaded)
            {
                Debug.LogError("[VehicleSpawner] vehiclePrefab YANLIS SURUKLENDI! Sahne objesi degil Projekten (Assets) araba prefab'i surukleyin.", this);
                return;
            }

            if (registeredPrefabs.Add(vehiclePrefab.gameObject))
            {
                NetworkClient.RegisterPrefab(vehiclePrefab.gameObject);
            }

            StartCoroutine(SpawnWhenServerReady());
        }

        private System.Collections.IEnumerator SpawnWhenServerReady()
        {
            while (!NetworkServer.active)
            {
                yield return new WaitForSeconds(0.1f);
            }

            Spawn();
        }

        private void Spawn()
        {
            if (spawnedInstance != null && spawnedInstance.gameObject != null)
                return;

            Vehicle instance = Instantiate(vehiclePrefab, spawnPosition, spawnRotation);
            spawnedInstance = instance;
            NetworkServer.Spawn(instance.gameObject);
            Debug.Log($"[VehicleSpawner] Araba spawn edildi ve network'e eklendi. Konum: {spawnPosition}", this);
        }
    }
}