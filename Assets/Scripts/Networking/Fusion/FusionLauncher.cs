using System;
using System.Collections;
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
        [SerializeField] private float hostMigrationGraceSeconds = 1f;

        private NetworkRunner runner;
        private PlayerSpawner spawner;
        private FusionInputProvider inputProvider;

        private bool hostMigrationInProgress;
        private Coroutine reconnectCoroutine;
        private string activeSessionName;

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

        private void RebuildRunner(ShutdownReason shutdownReason)
        {
            var oldRunner = runner;
            if (oldRunner != null)
            {
                oldRunner.RemoveCallbacks(this);
                if (inputProvider != null)
                    oldRunner.RemoveCallbacks(inputProvider);

                oldRunner.Shutdown(destroyGameObject: false, shutdownReason: shutdownReason);
                Destroy(oldRunner);
            }

            runner = gameObject.AddComponent<NetworkRunner>();
            ConfigureRunner();

            if (spawner != null)
                spawner.ResetSpawnerState();
        }

        public async Task JoinPublicLobby()
        {
            await runner.JoinSessionLobby(lobby);
        }

        public async Task Host(string roomName, int mapBuildIndex, int maxPlayers = 4)
        {
            activeSessionName = roomName;

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
            activeSessionName = roomName;

            var args = new StartGameArgs
            {
                GameMode = GameMode.Client,
                SessionName = roomName,
                SceneManager = GetSceneManager()
            };

            var result = await runner.StartGame(args);
            if (!result.Ok)
                Debug.LogError($"Join failed: {result.ShutdownReason}");
        }

        private NetworkSceneManagerDefault GetSceneManager()
        {
            var sm = GetComponent<NetworkSceneManagerDefault>();
            if (sm == null)
                sm = gameObject.AddComponent<NetworkSceneManagerDefault>();
            return sm;
        }

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

        private void CancelReconnect()
        {
            if (reconnectCoroutine == null)
                return;

            StopCoroutine(reconnectCoroutine);
            reconnectCoroutine = null;
        }

        private void BeginReconnectFallback()
        {
            if (!enableHostMigration)
            {
                ReturnToMenu();
                return;
            }

            if (string.IsNullOrWhiteSpace(activeSessionName))
            {
                ReturnToMenu();
                return;
            }

            if (reconnectCoroutine != null)
                return;

            reconnectCoroutine = StartCoroutine(ReconnectRoutine());
        }

        private IEnumerator ReconnectRoutine()
        {
            yield return new WaitForSeconds(hostMigrationGraceSeconds);

            const int maxAttempts = 20;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (hostMigrationInProgress)
                    yield break;

                RebuildRunner(ShutdownReason.Ok);

                var startTask = runner.StartGame(new StartGameArgs
                {
                    GameMode = GameMode.Client,
                    SessionName = activeSessionName,
                    SceneManager = GetSceneManager()
                });

                while (!startTask.IsCompleted)
                    yield return null;

                if (startTask.Result.Ok)
                {
                    reconnectCoroutine = null;
                    yield break;
                }

                yield return new WaitForSeconds(0.2f);
            }

            reconnectCoroutine = null;
            ReturnToMenu();
        }

        private async void ResumeFromHostMigration(HostMigrationToken hostMigrationToken)
        {
            if (!enableHostMigration || hostMigrationToken == null)
            {
                hostMigrationInProgress = false;
                BeginReconnectFallback();
                return;
            }

            CancelReconnect();
            RebuildRunner(ShutdownReason.HostMigration);

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
                Debug.LogWarning($"[FusionLauncher] Host migration resume failed: {result.ShutdownReason}. Falling back to reconnect.");
                BeginReconnectFallback();
            }
        }

        private void OnHostMigrationResume(NetworkRunner resumedRunner)
        {
            CancelReconnect();

            if (spawner == null || resumedRunner == null || !resumedRunner.IsServer)
                return;

            spawner.RefreshSpawnPoints();
            foreach (var player in resumedRunner.ActivePlayers)
                spawner.SpawnPlayerFor(player);
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer)
                return;

            spawner.DespawnPlayerFor(player);
        }

        public void OnInput(NetworkRunner runner, NetworkInput input) { }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            if (hostMigrationInProgress || shutdownReason == ShutdownReason.HostMigration)
                return;

            if (enableHostMigration && runner != null && !runner.IsServer)
            {
                BeginReconnectFallback();
                return;
            }

            ReturnToMenu();
        }

        public void OnConnectedToServer(NetworkRunner runner)
        {
            CancelReconnect();
        }

        public void OnDisconnectedFromServer(NetworkRunner runner)
        {
            if (hostMigrationInProgress)
                return;

            BeginReconnectFallback();
        }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
        {
            if (!enableHostMigration || hostMigrationInProgress)
                return;

            hostMigrationInProgress = true;
            CancelReconnect();
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

            BeginReconnectFallback();
        }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    }
}
