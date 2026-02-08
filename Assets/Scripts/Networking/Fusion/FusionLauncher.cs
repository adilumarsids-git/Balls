using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace Project.Networking.Fusion
{
    public class FusionLauncher : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Header("Defaults")]
        [SerializeField] private GameMode gameMode = GameMode.Shared;
        [SerializeField] private int gameplaySceneBuildIndex = 3; // Map A default
        [SerializeField] private SessionLobby lobby = SessionLobby.Shared; // Public lobby list

        private NetworkRunner runner;
        private PlayerSpawner spawner;

        public event Action<IReadOnlyList<SessionInfo>> SessionListChanged;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            runner = GetComponent<NetworkRunner>();
            spawner = GetComponent<PlayerSpawner>();

            runner.ProvideInput = true;

            runner.AddCallbacks(this);

            var inputProvider = GetComponent<FusionInputProvider>();
            if (inputProvider != null)
                runner.AddCallbacks(inputProvider);

            spawner.Init(runner);
        }

        public async Task JoinPublicLobby()
        {
            // Can be called multiple times safely
            await runner.JoinSessionLobby(lobby);
        }

        public async Task Host(string roomName, int mapBuildIndex, int maxPlayers = 4)
        {
            var sceneManager = GetSceneManager();

            var sceneInfo = new NetworkSceneInfo();
            sceneInfo.AddSceneRef(SceneRef.FromIndex(mapBuildIndex));

            var args = new StartGameArgs
            {
                GameMode = gameMode,
                SessionName = roomName,
                Scene = sceneInfo,
                SceneManager = sceneManager,

                IsVisible = true,
                IsOpen = true,
                PlayerCount = maxPlayers,

                // ✅ Add session props (visible in public list)
                SessionProperties = new Dictionary<string, SessionProperty>
        {
            { "map", mapBuildIndex }
        }
            };

            var result = await runner.StartGame(args);
            if (!result.Ok)
                Debug.LogError($"Host failed: {result.ShutdownReason}");
        }

        public async Task Join(string roomName)
        {
            var sceneManager = GetSceneManager();

            var args = new StartGameArgs
            {
                GameMode = gameMode,
                SessionName = roomName,
                SceneManager = sceneManager
            };

            var result = await runner.StartGame(args);
            if (!result.Ok)
                Debug.LogError($"Join failed: {result.ShutdownReason}");
        }

        private NetworkSceneManagerDefault GetSceneManager()
        {
            var sm = GetComponent<NetworkSceneManagerDefault>();
            if (sm == null) sm = gameObject.AddComponent<NetworkSceneManagerDefault>();
            return sm;
        }

        // ===== Callbacks =====

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            // Shared Mode: each peer spawns its own player object
            if (player == runner.LocalPlayer)
                spawner.SpawnPlayerFor(player);
        }

        public void OnSceneLoadDone(NetworkRunner runner)
        {
            spawner.RefreshSpawnPoints();
            spawner.EnsureLocalPlayerSpawned();
        }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            SessionListChanged?.Invoke(sessionList);
        }


        // Unused
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    }
}
