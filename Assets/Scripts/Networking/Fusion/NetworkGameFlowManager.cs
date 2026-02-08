using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    /// Shared mode: SharedModeMasterClient acts as referee and drives the state machine.
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
        [SerializeField] private int menuSceneBuildIndex = 1; // 01_Menu

        [Networked] public MatchFlowState State { get; private set; }
        [Networked] public int CurrentPlayers { get; private set; }

        [Networked] private TickTimer CountdownTimer { get; set; }
        [Networked] private TickTimer BackToMenuTimer { get; set; }

        [Networked] public NetworkString<_32> WinnerName { get; private set; }
        public bool IsReady { get; private set; }
        [Header("Consumables")]
        [SerializeField] private float consumableRespawnSeconds = 3f;

        public override void Spawned()
        {
            Instance = this;
            IsReady = true;

            if (Runner.IsSharedModeMasterClient)
            {
                if (!Object.HasStateAuthority)
                {
                    Object.RequestStateAuthority();
                }
                else
                {
                    State = MatchFlowState.WaitingForPlayers;
                    CurrentPlayers = 0;
                    CountdownTimer = default;
                    BackToMenuTimer = default;
                    WinnerName = default;
                }
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (Runner.IsSharedModeMasterClient && !Object.HasStateAuthority)
            {
                Object.RequestStateAuthority();
            }

            // One source of truth in Shared Mode
            if (!Runner.IsSharedModeMasterClient || !Object.HasStateAuthority)
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
                return;

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
                return;
            }
        }

        /// <summary>
        /// Call this from RingOutZone (MASTER ONLY) when a player is eliminated.
        /// This will disable eliminated players and decide winner when 1 remains.
        /// </summary>
        public void NotifyEliminated(NetworkObject playerObj)
        {
            Debug.Log($"[Flow] NotifyEliminated called for {playerObj.name}");

            if (!Runner.IsSharedModeMasterClient || !Object.HasStateAuthority) return;
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

        [Rpc(RpcSources.All, RpcTargets.All)]
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

        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_AnnounceWinner(NetworkString<_32> winner)
        {
            Debug.Log($"Winner: {winner}");
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_BackToMenu(int menuBuildIndex)
        {
            // Cleanly exit the session on each client
            var r = NetworkRunner.GetRunnerForGameObject(gameObject);
            if (r != null)
                r.Shutdown();

            SceneManager.LoadScene(menuBuildIndex);
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        public void RPC_ReportRingOut(NetworkObject playerObj)
        {
            // Only master processes ringouts
            if (!Runner.IsSharedModeMasterClient || !Object.HasStateAuthority) return;

            Debug.Log($"[Flow] Master received ringout for {playerObj.name}");
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

        [Rpc(RpcSources.All, RpcTargets.All)]
        public void RPC_ReportConsumablePickup(NetworkObject consumableObj, NetworkObject playerObj, float sizeMul, float speedMul, float massMul)
        {
            if (!Runner.IsSharedModeMasterClient || !Object.HasStateAuthority) return; // master decides
            if (State != MatchFlowState.Playing) return;
            if (consumableObj == null || playerObj == null) return;

            // Prevent double pickup: if already inactive, ignore
            var cons = consumableObj.GetComponent<Project.Game.Consumables.NetworkConsumable>();
            if (cons == null) return;

            // Use a master-only RPC to apply effects and respawn
            RPC_ApplyConsumable(consumableObj, playerObj, sizeMul, speedMul, massMul);
        }
        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_ApplyConsumable(NetworkObject consumableObj, NetworkObject playerObj, float sizeMul, float speedMul, float massMul)
        {
            if (consumableObj == null || playerObj == null) return;

            var cons = consumableObj.GetComponent<Project.Game.Consumables.NetworkConsumable>();
            if (cons == null) return;

            // Hide on all
            cons.SetActiveVisual(false);

            // Apply effect to the player (player's StateAuthority will actually simulate)
            var ctrl = playerObj.GetComponent<NetworkPlayerController>();
            if (ctrl != null)
                ctrl.OnConsumablePickup(sizeMul, speedMul, massMul);

            // Master schedules respawn by sending another RPC after delay (simple approach)
            if (Runner != null && Runner.IsSharedModeMasterClient)
                StartCoroutine(RespawnConsumableAfter(consumableObj, consumableRespawnSeconds));
        }
        private System.Collections.IEnumerator RespawnConsumableAfter(NetworkObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);

            // Master chooses new position then broadcasts
            var spawner = FindObjectOfType<Project.Game.Consumables.NetworkConsumableSpawner>();
            if (spawner == null) yield break;

            Vector3 pos = spawner.GetRandomSpawnPosition();
            RPC_RespawnConsumable(obj, pos);
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_RespawnConsumable(NetworkObject obj, Vector3 pos)
        {
            if (obj == null) return;
            obj.transform.position = pos;

            var cons = obj.GetComponent<Project.Game.Consumables.NetworkConsumable>();
            if (cons != null)
                cons.SetActiveVisual(true);
        }


    }
}
