using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Networking.Fusion
{
    [RequireComponent(typeof(NetworkRunner))]
    [RequireComponent(typeof(PlayerSpawner))]
    [RequireComponent(typeof(FusionInputProvider))]
    public class FusionLauncher : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Header("Defaults")]
        [SerializeField] private GameMode gameMode = GameMode.Shared;
        [SerializeField] private int gameplaySceneBuildIndex = 4; // Map A default
        [SerializeField] private SessionLobby lobby = SessionLobby.Shared; // Public lobby list
        [SerializeField] private int menuSceneBuildIndex = 2; // 02_Menu

        [Header("Latency Tuning")]
        [SerializeField] private bool optimizeLocalLatency = true;
        [SerializeField] private int targetFrameRate = 120;

        private NetworkRunner runner;
        private PlayerSpawner spawner;
        private bool isReturningToMenu;

        public event Action<IReadOnlyList<SessionInfo>> SessionListChanged;

        public static FusionLauncher GetOrCreate()
        {
            var existing = FindObjectOfType<FusionLauncher>();
            if (existing != null)
                return existing;

            var go = new GameObject("FusionLauncher");
            var launcher = go.AddComponent<FusionLauncher>();
            var spawner = go.GetComponent<PlayerSpawner>();
            if (spawner != null)
                spawner.EnsurePlayerPrefabLoaded();
            return launcher;
        }


        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            isReturningToMenu = false;
            ApplyLatencyTuning();
            runner = GetComponent<NetworkRunner>();
            spawner = GetComponent<PlayerSpawner>();
            spawner?.EnsurePlayerPrefabLoaded();

            runner.ProvideInput = true;

            runner.AddCallbacks(this);

            var inputProvider = GetComponent<FusionInputProvider>();
            if (inputProvider != null)
                runner.AddCallbacks(inputProvider);

            spawner.Init(runner);
        }


        private void ApplyLatencyTuning()
        {
            if (!optimizeLocalLatency)
                return;

            // Lower client-side input/render latency (does not remove internet RTT).
            QualitySettings.vSyncCount = 0;
            if (targetFrameRate > 0)
                Application.targetFrameRate = targetFrameRate;

        }

        public async Task JoinPublicLobby()
        {
            isReturningToMenu = false;
            await runner.JoinSessionLobby(lobby);
        }

        public async Task Host(string roomName, int mapBuildIndex, int maxPlayers = 4)
        {
            isReturningToMenu = false;
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
            isReturningToMenu = false;
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

        private void ReturnToMenu()
        {
            if (isReturningToMenu)
                return;

            isReturningToMenu = true;

            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex != menuSceneBuildIndex)
                SceneManager.LoadScene(menuSceneBuildIndex);
        }

        public void ShutdownAndReturnToMenu()
        {
            if (isReturningToMenu)
                return;

            if (runner != null && runner.IsRunning)
            {
                _ = runner.Shutdown();
                return;
            }

            ReturnToMenu();
        }

        // ===== Unused callbacks (kept empty) =====
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            ReturnToMenu();
        }

        public void OnConnectedToServer(NetworkRunner runner) { }

        // Some Fusion versions have this overload, some only have the reason version.
        public void OnDisconnectedFromServer(NetworkRunner runner)
        {
            ReturnToMenu();
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            ReturnToMenu();
        }

        // Token type changed across versions: keep BOTH.
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, ReadOnlySpan<byte> token) { }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

        // ✅ FIX: Fusion newer versions expect ReadOnlySpan<byte>. Keep both overloads.
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }

        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    }
}
