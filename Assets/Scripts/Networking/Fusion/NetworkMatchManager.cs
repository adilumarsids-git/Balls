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
            if (Object.HasStateAuthority)
            {
                players.Add(player);
                AliveCount++;
                return;
            }

            RPC_RegisterPlayer(player);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RegisterPlayer(NetworkObject player, RpcInfo info = default)
        {
            if (players.Contains(player)) return;

            players.Add(player);
            AliveCount++;
        }

        public void PlayerEliminated(NetworkObject player)
        {
            if (Object.HasStateAuthority)
            {
                if (!players.Contains(player)) return;

                AliveCount--;

                if (AliveCount <= 1)
                    EndRound();

                return;
            }

            RPC_PlayerEliminated(player);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_PlayerEliminated(NetworkObject player, RpcInfo info = default)
        {
            if (!players.Contains(player)) return;

            AliveCount--;

            if (AliveCount <= 1)
                EndRound();
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

            // Reset match state (authority side)
            players.Clear();
            AliveCount = 0;

            // Tell everyone to respawn locally (Shared-correct)
            RPC_RequestLocalRespawn();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_RequestLocalRespawn(RpcInfo info = default)
        {
            var spawner = FindObjectOfType<PlayerSpawner>();
            if (spawner == null)
            {
                Debug.LogError("[MatchManager] PlayerSpawner not found for respawn.");
                return;
            }

            spawner.RefreshSpawnPoints();
            spawner.DespawnLocalPlayer();
            spawner.EnsureLocalPlayerSpawned();
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
