using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using IsometricShooter.Core;

namespace IsometricShooter.AI
{
    public class AISpawner : NetworkBehaviour
    {
        [Header("Spawner Settings")]
        [SerializeField] private GameObject aiPrefab;
        [SerializeField] private List<Transform> spawnPoints = new List<Transform>();
        [SerializeField] private List<GameObject> patrolWaypoints = new List<GameObject>();
        [SerializeField] private float respawnDelay = 3.0f;

        private List<GameObject> activeAIs = new List<GameObject>();

        public override void OnStartServer()
        {
            base.OnStartServer();
            SpawnAllInitialAIs();
        }

        private void SpawnAllInitialAIs()
        {
            foreach (var spawnPoint in spawnPoints)
            {
                if (spawnPoint != null && aiPrefab != null)
                {
                    SpawnAIAt(spawnPoint);
                }
            }
        }

        private void SpawnAIAt(Transform spawnPoint)
        {
            GameObject aiInstance = Instantiate(aiPrefab, spawnPoint.position, spawnPoint.rotation);

            AISimpleBehaviour aiBehaviour = aiInstance.GetComponent<AISimpleBehaviour>();
            if (aiBehaviour != null && patrolWaypoints != null && patrolWaypoints.Count > 0)
            {
                aiBehaviour.SetPatrolWaypoints(patrolWaypoints);
            }

            NetworkServer.Spawn(aiInstance);
            activeAIs.Add(aiInstance);

            Health health = aiInstance.GetComponent<Health>();
            if (health != null)
            {
                health.OnDeath += () => HandleAIDeath(aiInstance, spawnPoint);
            }
        }

        private void HandleAIDeath(GameObject deadAI, Transform spawnPoint)
        {
            if (deadAI == null) return;

            if (activeAIs.Contains(deadAI))
            {
                activeAIs.Remove(deadAI);
            }

            StartCoroutine(RespawnRoutine(deadAI, spawnPoint));
        }

        IEnumerator RespawnRoutine(GameObject deadAI, Transform spawnPoint)
        {
            yield return new WaitForSeconds(respawnDelay);

            if (deadAI != null)
            {
                NetworkServer.Destroy(deadAI);
            }

            if (aiPrefab != null && spawnPoint != null)
            {
                SpawnAIAt(spawnPoint);
            }
        }
    }
}
