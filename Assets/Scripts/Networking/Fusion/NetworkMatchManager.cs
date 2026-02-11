using Fusion;
using UnityEngine;
using System.Collections.Generic;
using Project.Leaderboard;

namespace Project.Networking.Fusion
{
    public class NetworkMatchManager : NetworkBehaviour
    {
        public static NetworkMatchManager Instance;

        [Header("Prefabs")]
        [SerializeField] private NetworkPrefabRef playerPrefab;
        [SerializeField] private NetworkPrefabRef consumablePrefab;

        [Networked] private int AliveCount { get; set; }
        [Networked] private TickTimer NextRoundTimer { get; set; }

        private readonly List<NetworkObject> players = new List<NetworkObject> ();

        public override void Spawned()
        {
            if (Instance == null)
                Instance = this;

            if (Object.HasStateAuthority)
            {
                AliveCount = 0;
            }
        }

        // =============================
        // PLAYER REGISTRATION
        // =============================

        public void RegisterPlayer(NetworkObject player)
        {
            if (!Object.HasStateAuthority) return;

            players.Add(player);
            AliveCount++;
        }

        public void PlayerEliminated(NetworkObject player)
        {
            if (!Object.HasStateAuthority) return;

            if (!players.Contains(player)) return;

            AliveCount--;

            if (AliveCount <= 1)
            {
                EndRound();
            }
        }

        private void EndRound()
        {
            if (!Object.HasStateAuthority) return;

            NetworkObject winner = null;

            foreach (var p in players)
            {
                if (p != null && p.gameObject.activeSelf)
                {
                    winner = p;
                    break;
                }
            }

            if (winner != null)
            {
                RPC_AnnounceWinner(winner);
            }

            NextRoundTimer = TickTimer.CreateFromSeconds(Runner, 3f);
        }

        private void StartNextRound()
        {
            if (!Object.HasStateAuthority) return;

            foreach (var p in players)
            {
                if (p != null)
                    Runner.Despawn(p);
            }

            players.Clear();
            AliveCount = 0;

            // Respawn players
            foreach (var player in Runner.ActivePlayers)
            {
                FindObjectOfType<PlayerSpawner>()
                    .SpawnPlayerFor(player);
            }
        }
        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;

            if (NextRoundTimer.IsRunning && NextRoundTimer.Expired(Runner))
            {
                NextRoundTimer = default;
                StartNextRound();
            }
        }

        // =============================
        // RPCs
        // =============================

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_AnnounceWinner(NetworkObject winner)
        {
            Debug.Log("Winner: " + winner.name);

            if (!Object.HasStateAuthority || winner == null)
                return;

            var appearance = winner.GetComponent<NetworkPlayerAppearance>();
            if (appearance == null)
                return;

            var nftKey = appearance.GetLeaderboardNftKey();
            if (string.IsNullOrWhiteSpace(nftKey))
                return;

            string displayName = nftKey;
            _ = SubmitWinnerAsync(nftKey, displayName);
        }
        private async System.Threading.Tasks.Task SubmitWinnerAsync(string nftKey, string displayName)
        {
            try
            {
                var leaderboard = NftLeaderboardService.FindOrCreate();
                await leaderboard.SubmitWinAsync(nftKey, displayName);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Leaderboard submit failed: {ex.Message}");
            }
        }

    }
}
