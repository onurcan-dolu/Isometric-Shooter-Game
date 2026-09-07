using UnityEngine;
using Mirror;
using Steamworks;
using Steamworks.Data;
using System.Threading.Tasks;
using TMPro;

namespace IsometricShooter.Network
{
    public class SteamNetworkManager : MonoBehaviour
    {
        public static SteamNetworkManager Instance { get; private set; }

        [Header("Mirror")]
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private Transport fizzyTransport;

        [Header("UI Reference")]
        [SerializeField] private TMP_InputField lobbyIdInputField;

        [Header("Settings")]
        [SerializeField] private int maxPlayers = 4;

        private Lobby? currentLobby;
        private const string HostSteamIdKey = "HostSteamID";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (networkManager == null)
                networkManager = NetworkManager.singleton;
        }

        private void SetTransport(Transport transport)
        {
            if (transport == null || networkManager == null)
                return;

            if (networkManager.transport != transport)
            {
                networkManager.transport = transport;
            }

            Transport.active = transport;
        }

        private void OnEnable()
        {
            SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
            SteamNetworking.OnP2PSessionRequest += OnP2PSessionRequest;
        }

        private void OnDisable()
        {
            SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
            SteamNetworking.OnP2PSessionRequest -= OnP2PSessionRequest;
        }

        private void OnP2PSessionRequest(SteamId steamId)
        {
            SteamNetworking.AcceptP2PSessionWithUser(steamId);
        }

        private void OnLobbyEntered(Lobby lobby)
        {
            currentLobby = lobby;
        }

        public void HostGame()
        {
            _ = HostGameAsync();
        }

        private async Task HostGameAsync()
        {
            try
            {
                if (!SteamClient.IsValid)
                {
                    Debug.LogError("Steam failed to initialize.");
                    return;
                }

                if (NetworkServer.active || NetworkClient.active) return;

                SetTransport(fizzyTransport);

                Lobby? lobby = await SteamMatchmaking.CreateLobbyAsync(maxPlayers);
                if (!lobby.HasValue)
                {
                    Debug.LogError("Failed to create lobby.");
                    return;
                }

                currentLobby = lobby.Value;
                currentLobby.Value.SetFriendsOnly();
                currentLobby.Value.SetJoinable(true);

                string mySteamIdStr = SteamClient.SteamId.ToString();
                currentLobby.Value.SetData(HostSteamIdKey, mySteamIdStr);

                GUIUtility.systemCopyBuffer = currentLobby.Value.Id.ToString();
                Debug.Log($"Lobby created. ID copied to clipboard: {currentLobby.Value.Id}");

                networkManager.StartHost();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"HostGame failed: {e.Message}");
            }
        }

        public void JoinLobbyById()
        {
            _ = JoinLobbyByIdAsync();
        }

        private async Task JoinLobbyByIdAsync()
        {
            try
            {
                if (lobbyIdInputField == null)
                {
                    Debug.LogError("LobbyId InputField is not assigned!");
                    return;
                }

                string lobbyIdStr = lobbyIdInputField.text.Trim();
                if (string.IsNullOrEmpty(lobbyIdStr))
                {
                    Debug.LogError("Lobby ID cannot be empty.");
                    return;
                }

                if (!ulong.TryParse(lobbyIdStr, out ulong lobbyId))
                {
                    Debug.LogError("Invalid Lobby ID format.");
                    return;
                }

                if (NetworkClient.active || NetworkServer.active)
                {
                    StopNetwork();
                }

                SetTransport(fizzyTransport);

                Debug.Log($"Joining lobby ID: {lobbyId}");

                Lobby? joinedLobby = await SteamMatchmaking.JoinLobbyAsync(lobbyId);
                if (!joinedLobby.HasValue)
                {
                    Debug.LogError("Failed to join lobby.");
                    return;
                }

                currentLobby = joinedLobby.Value;

                await Task.Delay(1500);

                string hostSteamIdString = currentLobby.Value.GetData(HostSteamIdKey);
                if (string.IsNullOrEmpty(hostSteamIdString) || !ulong.TryParse(hostSteamIdString, out ulong hostSteamId))
                {
                    Debug.LogError("Valid HostSteamID not found in lobby.");
                    return;
                }

                Debug.Log($"Host SteamID retrieved: {hostSteamId}");

                networkManager.networkAddress = hostSteamId.ToString();
                networkManager.StartClient();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"JoinLobbyById failed: {e.Message}");
            }
        }

        public void StopNetwork()
        {
            if (currentLobby.HasValue)
            {
                currentLobby.Value.Leave();
                currentLobby = null;
            }

            if (NetworkServer.active) networkManager.StopHost();
            else if (NetworkClient.active) networkManager.StopClient();
        }
    }
}
