using UnityEngine;
using Mirror;
using System.Collections;
using System.Collections.Generic;

namespace IsometricShooter.Core
{
    public class PlayerRespawnManager : NetworkBehaviour
    {
        [Header("Respawn Settings")]
        [SerializeField] private float respawnDelay = 5f;

        public static PlayerRespawnManager Instance { get; private set; }

        private class PlayerInfo
        {
            public GameObject playerObject;
            public Health healthComponent;
            public NetworkConnectionToClient connection;
            public bool isRespawning;
        }

        private readonly List<PlayerInfo> trackedPlayers = new List<PlayerInfo>();
        private NetworkStartPosition[] cachedSpawnPoints;
        private Transform cachedFallbackStart;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public override void OnStartServer()
        {
            cachedSpawnPoints = FindObjectsOfType<NetworkStartPosition>();
            GameObject fallbackObj = GameObject.FindWithTag("StartPos");
            cachedFallbackStart = fallbackObj != null ? fallbackObj.transform : null;
        }

        private void Update()
        {
            if (!isServer)
                return;

            for (int i = trackedPlayers.Count - 1; i >= 0; i--)
            {
                PlayerInfo info = trackedPlayers[i];

                if (info.playerObject == null || info.healthComponent == null)
                {
                    trackedPlayers.RemoveAt(i);
                    continue;
                }

                if (!info.isRespawning && info.healthComponent.GetHealth() <= 0f)
                {
                    info.isRespawning = true;
                    StartCoroutine(RespawnRoutine(info));
                }
            }
        }

        public static void RegisterPlayer(GameObject playerObj, NetworkConnectionToClient conn)
        {
            if (Instance == null || !NetworkServer.active)
                return;

            Health health = playerObj.GetComponent<Health>() ?? playerObj.GetComponentInChildren<Health>();
            if (health != null)
            {
                if (!Instance.trackedPlayers.Exists(p => p.playerObject == playerObj))
                {
                    Instance.trackedPlayers.Add(new PlayerInfo
                    {
                        playerObject = playerObj,
                        healthComponent = health,
                        connection = conn,
                        isRespawning = false
                    });
                }
            }
        }

        public static void GetAlivePlayerTransforms(List<Transform> results)
        {
            results.Clear();

            if (Instance == null || !NetworkServer.active)
                return;

            foreach (PlayerInfo info in Instance.trackedPlayers)
            {
                if (info.playerObject == null || info.healthComponent == null)
                    continue;

                if (info.healthComponent.GetHealth() <= 0f)
                    continue;

                results.Add(info.playerObject.transform);
            }
        }
        private IEnumerator RespawnRoutine(PlayerInfo info)
        {
            yield return new WaitForSeconds(respawnDelay);

            NetworkConnectionToClient conn = info.connection;

            trackedPlayers.Remove(info);

            if (info.playerObject != null)
            {
                NetworkServer.Destroy(info.playerObject);
            }

            Transform spawnPoint = GetRandomSpawnPoint();
            Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
            Quaternion spawnRot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

            if (NetworkManager.singleton != null && NetworkManager.singleton.playerPrefab != null && conn != null)
            {
                GameObject newPlayerInstance = Instantiate(
                    NetworkManager.singleton.playerPrefab,
                    spawnPos,
                    spawnRot
                );

                NetworkServer.ReplacePlayerForConnection(conn, newPlayerInstance, true);

                RegisterPlayer(newPlayerInstance, conn);
            }
        }
        private Transform GetRandomSpawnPoint()
        {
            if (cachedSpawnPoints != null && cachedSpawnPoints.Length > 0)
            {
                int randomIndex = Random.Range(0, cachedSpawnPoints.Length);
                return cachedSpawnPoints[randomIndex].transform;
            }

            return cachedFallbackStart;
        }
    }
}
