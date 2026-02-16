using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Networking.Fusion
{
    public class FusionLauncher : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Header("Defaults")]
        [SerializeField] private int gameplaySceneBuildIndex = 4; // Map A default
        [SerializeField] private SessionLobby lobby = SessionLobby.ClientServer; // Public lobby list
        [SerializeField] private int menuSceneBuildIndex = 2; // 02_Menu

        [Header("Host Migration")]
        [SerializeField] private bool enableHostMigration = true;
        [SerializeField] private float hostMigrationGraceSeconds = 6f;

        private NetworkRunner runner;
        private PlayerSpawner spawner;
        private FusionInputProvider inputProvider;
        private bool hostMigrationInProgress;
        private Coroutine pendingDisconnectCoroutine;

        public event Action<IReadOnlyList<SessionInfo>> SessionListChanged;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            EnsureComponents();
            ConfigureRunner();
        }

        private void EnsureComponents()
        {
            runner = GetComponent<NetworkRunner>();
            if (runner == null)
                runner = gameObject.AddComponent<NetworkRunner>();

            spawner = GetComponent<PlayerSpawner>();
            inputProvider = GetComponent<FusionInputProvider>();
        }

        private void ConfigureRunner()
        {
            runner.ProvideInput = true;
            runner.AddCallbacks(this);

            if (inputProvider != null)
                runner.AddCallbacks(inputProvider);

            if (spawner != null)
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
                GameMode = GameMode.Host,
                SessionName = roomName,
                Scene = sceneInfo,
                SceneManager = sceneManager,

                IsVisible = true,
                IsOpen = true,
                PlayerCount = maxPlayers,

                // Add session props (visible in public list)
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
                GameMode = GameMode.Client,
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
            if (!runner.IsServer)
                return;

            spawner.SpawnPlayerFor(player);
        }

        public void OnSceneLoadDone(NetworkRunner runner)
        {
            spawner.RefreshSpawnPoints();
            if (!runner.IsServer)
                return;

            foreach (var player in runner.ActivePlayers)
                spawner.SpawnPlayerFor(player);
        }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            SessionListChanged?.Invoke(sessionList);
        }

        private void ReturnToMenu()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex == menuSceneBuildIndex)
                return;

            SceneManager.LoadScene(menuSceneBuildIndex);
        }

        private bool ShouldReturnToMenu(ShutdownReason shutdownReason)
        {
            if (hostMigrationInProgress)
                return false;

            if (shutdownReason == ShutdownReason.HostMigration)
                return false;

            return true;
        }

        private void CancelPendingDisconnectReturn()
        {
            if (pendingDisconnectCoroutine == null)
                return;

            StopCoroutine(pendingDisconnectCoroutine);
            pendingDisconnectCoroutine = null;
        }

        private void BeginPendingDisconnectReturn()
        {
            if (!enableHostMigration)
            {
                ReturnToMenu();
                return;
            }

            CancelPendingDisconnectReturn();
            pendingDisconnectCoroutine = StartCoroutine(DisconnectGraceRoutine());
        }

        private System.Collections.IEnumerator DisconnectGraceRoutine()
        {
            yield return new WaitForSeconds(hostMigrationGraceSeconds);

            pendingDisconnectCoroutine = null;

            if (!hostMigrationInProgress)
                ReturnToMenu();
        }

        private async void ResumeFromHostMigration(HostMigrationToken hostMigrationToken)
        {
            if (!enableHostMigration)
            {
                hostMigrationInProgress = false;
                ReturnToMenu();
                return;
            }

            if (hostMigrationToken == null)
            {
                hostMigrationInProgress = false;
                ReturnToMenu();
                return;
            }

            CancelPendingDisconnectReturn();
            Debug.Log("[FusionLauncher] Host migration started. Rebuilding runner...");

            var oldRunner = runner;

            if (oldRunner != null)
            {
                oldRunner.RemoveCallbacks(this);
                if (inputProvider != null)
                    oldRunner.RemoveCallbacks(inputProvider);

                oldRunner.Shutdown(destroyGameObject: false, shutdownReason: ShutdownReason.HostMigration);
                Destroy(oldRunner);
            }

            runner = gameObject.AddComponent<NetworkRunner>();
            ConfigureRunner();

            if (spawner != null)
                spawner.ResetSpawnerState();

            var result = await runner.StartGame(new StartGameArgs
            {
                GameMode = hostMigrationToken.GameMode,
                SceneManager = GetSceneManager(),
                HostMigrationToken = hostMigrationToken,
                HostMigrationResume = OnHostMigrationResume
            });

            hostMigrationInProgress = false;

            if (!result.Ok)
            {
                Debug.LogError($"[FusionLauncher] Host migration resume failed: {result.ShutdownReason}");
                ReturnToMenu();
            }
        }

        private void OnHostMigrationResume(NetworkRunner resumedRunner)
        {
            Debug.Log("[FusionLauncher] Host migration resume completed.");

            if (spawner == null || resumedRunner == null || !resumedRunner.IsServer)
                return;

            spawner.RefreshSpawnPoints();

            foreach (var player in resumedRunner.ActivePlayers)
                spawner.SpawnPlayerFor(player);
        }

        // Unused
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer)
                return;

            spawner.DespawnPlayerFor(player);
        }

        public void OnInput(NetworkRunner runner, NetworkInput input) { }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            if (ShouldReturnToMenu(shutdownReason))
                ReturnToMenu();
        }

        public void OnConnectedToServer(NetworkRunner runner)
        {
            CancelPendingDisconnectReturn();
        }

        public void OnDisconnectedFromServer(NetworkRunner runner)
        {
            if (hostMigrationInProgress)
                return;

            BeginPendingDisconnectReturn();
        }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
        {
            if (!enableHostMigration)
                return;

            if (hostMigrationInProgress)
                return;

            hostMigrationInProgress = true;
            CancelPendingDisconnectReturn();
            ResumeFromHostMigration(hostMigrationToken);
        }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            if (hostMigrationInProgress)
                return;

            BeginPendingDisconnectReturn();
        }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    }
}
