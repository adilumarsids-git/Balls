using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using Project.Game.Consumables;

namespace Project.Networking.Fusion
{
    public enum MatchFlowState : byte
    {
        WaitingForPlayers = 0,
        Countdown = 1,
        Playing = 2,
        GameOver = 3
    }

    /// <summary>
    /// Scene NetworkObject (place it in each map scene).
    /// Server/Host drives the authoritative state machine.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkGameFlowManager : NetworkBehaviour
    {
        public static NetworkGameFlowManager Instance { get; private set; }

        [Header("Rules")]
        [SerializeField] private int requiredPlayers = 4;
        [SerializeField] private float countdownSeconds = 3f;

        [Header("End Game")]
        [SerializeField] private float backToMenuDelay = 5f;
        [SerializeField] private int menuSceneBuildIndex = 2; // 02_Menu

        [Networked] public MatchFlowState State { get; private set; }
        [Networked] public int CurrentPlayers { get; private set; }

        [Networked] private TickTimer CountdownTimer { get; set; }
        [Networked] private TickTimer BackToMenuTimer { get; set; }

        [Networked] public NetworkString<_32> WinnerName { get; private set; }
        public bool IsReady { get; private set; }
        [Header("Consumables")]
        [SerializeField] private NetworkPrefabRef consumablePrefab;
        [SerializeField] private float consumableInitialSpawnDelay = 10f;
        [SerializeField] private float consumableRespawnSeconds = 3f;
        private NetworkConsumable activeConsumable;
        [Networked] private TickTimer ConsumableSpawnTimer { get; set; }

        public override void Spawned()
        {
            Instance = this;
            IsReady = true;

            if (!Runner.IsServer || !Object.HasStateAuthority)
                return;

            State = MatchFlowState.WaitingForPlayers;
            CurrentPlayers = 0;
            CountdownTimer = default;
            BackToMenuTimer = default;
            WinnerName = default;
            ConsumableSpawnTimer = default;
        }

        public override void FixedUpdateNetwork()
        {
            // One source of truth on Host/Server
            if (!Runner.IsServer || !Object.HasStateAuthority)
                return;

            // Always refresh player count while not shutdown
            CurrentPlayers = CountPlayers();

            // Game over: wait then send everyone back to menu
            if (State == MatchFlowState.GameOver)
            {
                if (BackToMenuTimer.IsRunning && BackToMenuTimer.Expired(Runner))
                {
                    BackToMenuTimer = default;
                    RPC_BackToMenu(menuSceneBuildIndex);
                }
                return;
            }

            // If match already started, don’t auto-change state here
            if (State == MatchFlowState.Playing)
            {
                if (!ConsumableSpawnTimer.IsRunning)
                {
                    ConsumableSpawnTimer = TickTimer.CreateFromSeconds(Runner, consumableInitialSpawnDelay);
                }
                EnsureConsumableSpawned();
                return;
            }

            // Waiting for players
            if (CurrentPlayers < requiredPlayers)
            {
                State = MatchFlowState.WaitingForPlayers;
                CountdownTimer = default;
                return;
            }

            // Enough players -> start countdown if not already
            if (State == MatchFlowState.WaitingForPlayers)
            {
                State = MatchFlowState.Countdown;
                CountdownTimer = TickTimer.CreateFromSeconds(Runner, countdownSeconds);
                return;
            }

            // Countdown expired -> playing
            if (State == MatchFlowState.Countdown && CountdownTimer.Expired(Runner))
            {
                State = MatchFlowState.Playing;
                CountdownTimer = default;
                ConsumableSpawnTimer = TickTimer.CreateFromSeconds(Runner, consumableInitialSpawnDelay);
                return;
            }

        }

        private void EnsureConsumableSpawned()
        {
            if (!Runner.IsServer || !Object.HasStateAuthority)
                return;

            if (activeConsumable == null)
                activeConsumable = FindObjectOfType<NetworkConsumable>(true);

            if (ConsumableSpawnTimer.IsRunning && !ConsumableSpawnTimer.Expired(Runner))
                return;

            if (activeConsumable != null)
                return;

            if (!consumablePrefab.IsValid)
                return;

            Vector3 pos = GetRandomConsumablePosition();
            var spawnedObj = Runner.Spawn(consumablePrefab, pos, Quaternion.identity, Runner.LocalPlayer);
            if (spawnedObj != null)
            {
                activeConsumable = spawnedObj.GetComponent<NetworkConsumable>();
                if (activeConsumable != null)
                    activeConsumable.SetActiveState(true);
            }
        }

        private Vector3 GetRandomConsumablePosition()
        {
            var spawner = FindObjectOfType<NetworkConsumableSpawner>();
            if (spawner != null && spawner.HasSpawnPoints)
                return spawner.GetRandomSpawnPosition();

            var parent = GameObject.Find("ConsumableSpawnPoints");
            if (parent == null)
                return Vector3.zero;

            var points = parent.GetComponentsInChildren<Transform>(true);
            if (points.Length <= 1)
                return parent.transform.position;

            int idx = Random.Range(1, points.Length);
            return points[idx].position;
        }

        /// <summary>
        /// Call this from RingOutZone (SERVER ONLY) when a player is eliminated.
        /// This will disable eliminated players and decide winner when 1 remains.
        /// </summary>
        public void NotifyEliminated(NetworkObject playerObj)
        {
            Debug.Log($"[Flow] NotifyEliminated called for {playerObj.name}");

            if (!Runner.IsServer || !Object.HasStateAuthority) return;
            if (State != MatchFlowState.Playing) return;
            if (playerObj == null) return;

            // Disable eliminated player everywhere
            RPC_DisablePlayer(playerObj);

            // Determine if we have a winner
            int alive = CountAlivePlayers();
            if (alive <= 1)
            {
                var winnerObj = FindLastAlivePlayer();
                WinnerName = GetWinnerName(winnerObj);

                State = MatchFlowState.GameOver;

                RPC_AnnounceWinner(WinnerName);

                BackToMenuTimer = TickTimer.CreateFromSeconds(Runner, backToMenuDelay);
            }

        }
        private NetworkString<_32> GetWinnerName(NetworkObject winnerObj)
        {
            if (winnerObj == null) return "Unknown";

            var ctrl = winnerObj.GetComponent<NetworkPlayerController>();
            if (ctrl != null)
            {
                var n = ctrl.PlayerName.ToString();
                if (!string.IsNullOrWhiteSpace(n))
                    return n;
            }

            return winnerObj.name;
        }


        /// <summary> Remaining countdown seconds for UI. </summary>
        public float GetCountdownRemaining()
        {
            if (State != MatchFlowState.Countdown) return 0f;
            float? t = CountdownTimer.RemainingTime(Runner);
            return t.HasValue ? Mathf.Max(0f, t.Value) : 0f;
        }

        // =========================
        // Helpers
        // =========================

        private int CountPlayers()
        {
            int c = 0;
            foreach (var p in Runner.ActivePlayers)
                c++;
            return c;
        }

        // Uses PlayerTag component instead of Unity tags (more robust).
        private int CountAlivePlayers()
        {
            int alive = 0;
            var players = FindObjectsOfType<Project.Gameplay.Player.PlayerTag>(true);

            foreach (var p in players)
            {
                if (p != null && p.gameObject.activeSelf)
                    alive++;
            }

            return alive;
        }

        private NetworkObject FindLastAlivePlayer()
        {
            var players = FindObjectsOfType<Project.Gameplay.Player.PlayerTag>(true);

            foreach (var p in players)
            {
                if (p != null && p.gameObject.activeSelf)
                    return p.NetObj != null ? p.NetObj : p.GetComponent<NetworkObject>();
            }

            return null;
        }

        // =========================
        // RPCs
        // =========================

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_DisablePlayer(NetworkObject obj)
        {
            if (obj == null) return;

            // Hard stop physics too
            var rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            obj.gameObject.SetActive(false);
            Debug.Log($"[Flow] RPC_DisablePlayer running for {obj.name}");

        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_AnnounceWinner(NetworkString<_32> winner)
        {
            Debug.Log($"Winner: {winner}");
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_BackToMenu(int menuBuildIndex)
        {
            // Cleanly exit the session on each client
            var r = NetworkRunner.GetRunnerForGameObject(gameObject);
            if (r != null)
                r.Shutdown();

            SceneManager.LoadScene(menuBuildIndex);
        }

        public void ReportRingOut(NetworkObject playerObj)
        {
            if (!Runner.IsServer || !Object.HasStateAuthority)
                return;

            if (playerObj == null)
                return;

            Debug.Log($"[Flow] Server received ringout for {playerObj.name}");
            NotifyEliminated(playerObj);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            IsReady = false;
            if (Instance == this) Instance = null;
        }

        private void OnDestroy()
        {
            IsReady = false;
            if (Instance == this) Instance = null;
        }

        public void ReportConsumablePickup(NetworkObject consumableObj, NetworkObject playerObj, float sizeMul, float speedMul, float massMul)
        {
            if (!Runner.IsServer || !Object.HasStateAuthority) return; // server decides
            if (State != MatchFlowState.Playing) return;
            if (consumableObj == null || playerObj == null) return;

            // Prevent double pickup: if already inactive, ignore
            var cons = consumableObj.GetComponent<Project.Game.Consumables.NetworkConsumable>();
            if (cons == null) return;

            RPC_ApplyConsumable(consumableObj, playerObj, sizeMul, speedMul, massMul);
        }
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ApplyConsumable(NetworkObject consumableObj, NetworkObject playerObj, float sizeMul, float speedMul, float massMul)
        {
            if (consumableObj == null || playerObj == null) return;

            var cons = consumableObj.GetComponent<Project.Game.Consumables.NetworkConsumable>();
            if (cons == null) return;

            // Hide on all
            cons.SetActiveState(false);

            // Apply effect to the player (player's StateAuthority will actually simulate)
            var ctrl = playerObj.GetComponent<NetworkPlayerController>();
            if (ctrl != null)
                ctrl.OnConsumablePickup(sizeMul, speedMul, massMul);

            // Server schedules respawn by sending another RPC after delay (simple approach)
            if (Runner != null && Runner.IsServer)
                StartCoroutine(RespawnConsumableAfter(consumableObj, consumableRespawnSeconds));
        }
        private System.Collections.IEnumerator RespawnConsumableAfter(NetworkObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);

            // Server chooses new position then broadcasts
            Vector3 pos = GetRandomConsumablePosition();
            RPC_RespawnConsumable(obj, pos);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_RespawnConsumable(NetworkObject obj, Vector3 pos)
        {
            if (obj == null) return;
            obj.transform.position = pos;

            var cons = obj.GetComponent<Project.Game.Consumables.NetworkConsumable>();
            if (cons != null)
                cons.SetActiveState(true);
        }


    }
}
