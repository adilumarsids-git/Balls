using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using Project.Game.Consumables;
using Project.Leaderboard;

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
        [SerializeField] private int totalRounds = 5;
        public int TotalRounds => totalRounds;
        [SerializeField] private float nextRoundDelaySeconds = 2f;

        [Header("End Game")]
        [SerializeField] private float backToMenuDelay = 1f;
        [SerializeField] private int menuSceneBuildIndex = 0; // 02_Menu

        [Networked] public MatchFlowState State { get; private set; }
        [Networked] public int CurrentPlayers { get; private set; }

        [Networked] private TickTimer CountdownTimer { get; set; }
        [Networked] private TickTimer BackToMenuTimer { get; set; }

        [Networked] public NetworkString<_32> WinnerName { get; private set; }
        [Networked] public int CurrentRound { get; private set; }
        [Networked] private NetworkBool MatchLocked { get; set; }
        public bool IsReady { get; private set; }
        [Header("Consumables")]
        [SerializeField] private NetworkPrefabRef consumablePrefab;
        [SerializeField] private float consumableInitialSpawnDelay = 10f;
        [SerializeField] private float consumableRespawnSeconds = 3f;
        private NetworkConsumable activeConsumable;
        [Networked] private TickTimer ConsumableSpawnTimer { get; set; }
        private bool _restartQueued;
        private readonly System.Collections.Generic.Dictionary<int, int> _playerScores = new System.Collections.Generic.Dictionary<int, int>();
        private readonly System.Collections.Generic.Dictionary<int, string> _playerDisplayNames = new System.Collections.Generic.Dictionary<int, string>();
        private readonly System.Collections.Generic.HashSet<int> _leftPlayers = new System.Collections.Generic.HashSet<int>();
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
                    CurrentRound = 1;
                    MatchLocked = false;
                    CountdownTimer = default;
                    BackToMenuTimer = default;
                    WinnerName = default;
                    ConsumableSpawnTimer = default;
                    _playerScores.Clear();
                    _playerDisplayNames.Clear();
                    _leftPlayers.Clear();
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
            CaptureParticipantSnapshot();

            if (State == MatchFlowState.GameOver)
            {
                // We restart after leaderboard submission now.
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

            // Waiting for players (only before match is locked)
            if (!MatchLocked && CurrentPlayers < requiredPlayers)
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
                MatchLocked = true;
                CountdownTimer = default;
                ConsumableSpawnTimer = TickTimer.CreateFromSeconds(Runner, consumableInitialSpawnDelay);
                return;
            }

        }

        private void EnsureConsumableSpawned()
        {
            if (!Runner.IsSharedModeMasterClient || !Object.HasStateAuthority)
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
        /// Call this from RingOutZone (MASTER ONLY) when a player is eliminated.
        /// This will disable eliminated players and decide winner when 1 remains.
        /// </summary>
        public void NotifyEliminated(NetworkObject playerObj)
        {
            Debug.Log($"[Flow] NotifyEliminated called for {playerObj.name}");

            if (!Runner.IsSharedModeMasterClient || !Object.HasStateAuthority) return;
            if (State != MatchFlowState.Playing) return;
            if (playerObj == null) return;
            if (!playerObj.gameObject.activeSelf) return;

            // Move eliminated player back to spawn, then disable until next round
            Vector3 spawnPos;
            Quaternion spawnRot;
            GetSpawnPoseFor(playerObj.InputAuthority, out spawnPos, out spawnRot);
            RPC_ResetPlayerForRound(playerObj, spawnPos, spawnRot);
            RPC_DisablePlayer(playerObj);

            // Determine if we have a winner
            int alive = CountAlivePlayers();
            if (alive <= 1)
            {
                var winnerObj = FindLastAlivePlayer();
                HandleRoundFinished(winnerObj);
            }

        }

        private void HandleRoundFinished(NetworkObject winnerObj)
        {
            var winnerName = GetWinnerName(winnerObj);
            WinnerName = winnerName;

            if (winnerObj != null)
            {
                int winnerKey = winnerObj.InputAuthority.RawEncoded;
                int current = 0;
                _playerScores.TryGetValue(winnerKey, out current);
                current++;
                _playerScores[winnerKey] = current;
                _leftPlayers.Remove(winnerKey);
                _playerDisplayNames[winnerKey] = GetDisplayName(winnerObj);
                RPC_AnnounceRoundWinner(winnerName, CurrentRound, totalRounds, winnerObj.InputAuthority.RawEncoded, current);
            }
            else
            {
                RPC_AnnounceRoundWinner("No winner", CurrentRound, totalRounds, -1, 0);
            }

            if (CurrentRound >= totalRounds)
            {
                State = MatchFlowState.GameOver;
                var matchWinnerObj = FindMatchWinnerObject();
                WinnerName = GetWinnerName(matchWinnerObj);
                RPC_AnnounceWinner(matchWinnerObj, WinnerName);
                StartCoroutine(SubmitLeaderboardThenRestart(matchWinnerObj, WinnerName));
                return;
            }

            CurrentRound += 1;
            StartCoroutine(BeginNextRoundRoutine());
        }

        private System.Collections.IEnumerator BeginNextRoundRoutine()
        {
            yield return new WaitForSeconds(nextRoundDelaySeconds);

            if (!Runner.IsSharedModeMasterClient || !Object.HasStateAuthority)
                yield break;

            ResetAllPlayersToSpawn();
            State = MatchFlowState.Countdown;
            CountdownTimer = TickTimer.CreateFromSeconds(Runner, countdownSeconds);
            ConsumableSpawnTimer = TickTimer.CreateFromSeconds(Runner, consumableInitialSpawnDelay);
        }

        private void ResetAllPlayersToSpawn()
        {
            var players = FindObjectsOfType<Project.Gameplay.Player.PlayerTag>(true);
            foreach (var p in players)
            {
                if (p == null) continue;

                var netObj = p.NetObj != null ? p.NetObj : p.GetComponent<NetworkObject>();
                if (netObj == null) continue;

                Vector3 spawnPos;
                Quaternion spawnRot;
                GetSpawnPoseFor(netObj.InputAuthority, out spawnPos, out spawnRot);
                RPC_ResetPlayerForRound(netObj, spawnPos, spawnRot);
            }
        }

        private void GetSpawnPoseFor(PlayerRef player, out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;

            var parent = GameObject.Find("SpawnPoints");
            if (parent == null)
                return;

            var points = parent.GetComponentsInChildren<Transform>(true);
            if (points == null || points.Length <= 1)
                return;

            int usable = points.Length - 1;
            int index = (Mathf.Abs(player.RawEncoded) % usable) + 1;
            var t = points[index];
            pos = t.position;

            var center = GameObject.Find("CenterPoint");
            if (center != null)
            {
                Vector3 dir = center.transform.position - t.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                    rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
        }

        private NetworkObject FindMatchWinnerObject()
        {
            NetworkObject best = null;
            int bestScore = int.MinValue;

            foreach (var kv in _playerScores)
            {
                if (kv.Value <= bestScore) continue;
                bestScore = kv.Value;
                best = FindPlayerByRawRef(kv.Key);
            }

            if (best != null)
                return best;

            return FindLastAlivePlayer();
        }

        private NetworkObject FindPlayerByRawRef(int raw)
        {
            var players = FindObjectsOfType<Project.Gameplay.Player.PlayerTag>(true);
            foreach (var p in players)
            {
                if (p == null) continue;
                var n = p.NetObj != null ? p.NetObj : p.GetComponent<NetworkObject>();
                if (n != null && n.InputAuthority.RawEncoded == raw)
                    return n;
            }
            return null;
        }

        public bool IsLocalPlayerEliminated()
        {
            if (Runner == null || !Runner.IsRunning) return false;
            if (State != MatchFlowState.Playing) return false;

            var localObj = Runner.GetPlayerObject(Runner.LocalPlayer);
            return localObj != null && !localObj.gameObject.activeSelf;
        }


        public string GetPointsBoardText()
        {
            CaptureParticipantSnapshot();
            if (_playerScores.Count == 0 && _playerDisplayNames.Count == 0)
                return string.Empty;

            var keys = new System.Collections.Generic.HashSet<int>(_playerDisplayNames.Keys);
            foreach (var k in _playerScores.Keys)
                keys.Add(k);

            var ordered = new System.Collections.Generic.List<int>(keys);
            ordered.Sort();

            var sb = new System.Text.StringBuilder();
            foreach (var raw in ordered)
            {
                int score = 0;
                _playerScores.TryGetValue(raw, out score);

                if (sb.Length > 0)
                    sb.Append('\n');
                var displayName = GetDisplayNameByRawRef(raw);
                sb.Append(displayName);
                if (_leftPlayers.Contains(raw))
                    sb.Append(" (left)");

                sb.Append(" = ");
                sb.Append(score);
            }

            return sb.ToString();
        }

        private void CaptureParticipantSnapshot()
        {
            var players = FindObjectsOfType<Project.Gameplay.Player.PlayerTag>(true);
            foreach (var p in players)
            {
                if (p == null) continue;
                var netObj = p.NetObj != null ? p.NetObj : p.GetComponent<NetworkObject>();
                if (netObj == null) continue;

                int raw = netObj.InputAuthority.RawEncoded;
                if (!_playerScores.ContainsKey(raw))
                    _playerScores[raw] = 0;

                _playerDisplayNames[raw] = GetDisplayName(netObj);
            }
        }

        private string GetDisplayNameByRawRef(int raw)
        {
            string name;
            if (_playerDisplayNames.TryGetValue(raw, out name) && !string.IsNullOrWhiteSpace(name))
                return name;

            var obj = FindPlayerByRawRef(raw);
            if (obj != null)
            {
                name = GetDisplayName(obj);
                _playerDisplayNames[raw] = name;
                return name;
            }

            return $"P{raw}";
        }

        public void NotifyPlayerLeft(PlayerRef player)
        {
            if (!Runner.IsSharedModeMasterClient || !Object.HasStateAuthority) return;
            RPC_ReportPlayerLeft(player.RawEncoded);
        }


        private string GetDisplayName(NetworkObject obj)
        {
            if (obj == null) return "Unknown";

            var ctrl = obj.GetComponent<NetworkPlayerController>();
            if (ctrl != null)
            {
                // Avoid reading Networked PlayerName here because this method can run
                // while a behaviour is not fully spawned on some peers.
                var n = ctrl.gameObject.name;
                if (!string.IsNullOrWhiteSpace(n))
                    return n;
            }

            var appearance = obj.GetComponent<NetworkPlayerAppearance>();
            if (appearance != null)
            {
                var nftName = appearance.GetLeaderboardNftDisplayName();
                if (!string.IsNullOrWhiteSpace(nftName))
                    return nftName;
            }

            return $"P{obj.InputAuthority.RawEncoded}";
        }

        private async System.Threading.Tasks.Task SubmitWinnerAsync(string nftKey, string displayName)
        {
            try
            {
                var leaderboard = NftLeaderboardService.FindOrCreate();
                await leaderboard.SubmitWinAsync(nftKey, displayName);
                Debug.Log($"[Flow] Leaderboard submitted: {displayName} ({nftKey})");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Flow] Leaderboard submit failed: {ex.Message}");
            }
        }

        private NetworkString<_32> GetWinnerName(NetworkObject winnerObj)
        {
            if (winnerObj == null) return "Unknown NFT";

            var appearance = winnerObj.GetComponent<NetworkPlayerAppearance>();
            if (appearance != null)
            {
                var nftName = appearance.GetLeaderboardNftDisplayName();
                if (!string.IsNullOrWhiteSpace(nftName))
                    return nftName;
            }

            return "Unknown NFT";
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

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ResetPlayerForRound(NetworkObject obj, Vector3 spawnPos, Quaternion spawnRot)
        {
            if (obj == null) return;

            obj.gameObject.SetActive(true);
            obj.transform.SetPositionAndRotation(spawnPos, spawnRot);

            var nt = obj.GetComponent<NetworkTransform>();
            if (nt != null)
                nt.Teleport(spawnPos, spawnRot);

            var rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            var ctrl = obj.GetComponent<NetworkPlayerController>();
            if (ctrl != null)
                ctrl.RPC_ResetRoundModifiers();
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        public void RPC_ReportPlayerLeft(int rawRef)
        {
            if (rawRef == PlayerRef.None.RawEncoded)
                return;

            _leftPlayers.Add(rawRef);
            if (!_playerScores.ContainsKey(rawRef))
                _playerScores[rawRef] = 0;

            if (!_playerDisplayNames.ContainsKey(rawRef))
                _playerDisplayNames[rawRef] = $"P{rawRef}";

            var playerObj = FindPlayerByRawRef(rawRef);
            if (Runner != null && Runner.IsSharedModeMasterClient && Object != null && Object.HasStateAuthority)
            {
                if (State == MatchFlowState.Playing && playerObj != null && playerObj.gameObject.activeSelf)
                {
                    NotifyEliminated(playerObj);
                }
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_AnnounceRoundWinner(NetworkString<_32> roundWinnerName, int roundNumber, int maxRounds, int winnerRawRef, int winnerScore)
        {
            Debug.Log($"[Flow] Round {roundNumber}/{maxRounds} winner: {roundWinnerName} (score {winnerScore})");
            if (winnerRawRef >= 0)
                _playerScores[winnerRawRef] = winnerScore;
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_AnnounceWinner(NetworkObject winnerObj, NetworkString<_32> winnerName)
        {
            Debug.Log($"Winner: {winnerName}");
            // Full restart for everyone after result is announced.

            // Only Shared master/state authority should submit to leaderboard
           
        }

        private System.Collections.IEnumerator SubmitLeaderboardThenRestart(NetworkObject winnerObj, NetworkString<_32> winnerName)
        {
            // Only Shared master/state authority should run this
            if (!Runner.IsSharedModeMasterClient || !Object.HasStateAuthority)
                yield break;

            // Protect against double calls
            if (_restartQueued)
                yield break;

            _restartQueued = true;

            // --- Submit leaderboard (same logic you already use) ---
            if (winnerObj == null)
            {
                Debug.LogWarning("[Flow] WinnerObj is null, cannot submit leaderboard.");
            }
            else
            {
                var appearance = winnerObj.GetComponent<NetworkPlayerAppearance>();
                if (appearance == null)
                {
                    Debug.LogWarning("[Flow] Winner has no NetworkPlayerAppearance, cannot submit leaderboard.");
                }
                else
                {
                    var nftKey = appearance.GetLeaderboardNftKey();
                    if (string.IsNullOrWhiteSpace(nftKey))
                    {
                        Debug.LogWarning("[Flow] Winner nftKey is empty, cannot submit leaderboard.");
                    }
                    else
                    {
                        string displayName = winnerName.ToString();

                        var task = SubmitWinnerAsync(nftKey, displayName);
                        while (!task.IsCompleted) yield return null;

                        // If it failed, your SubmitWinnerAsync already logs warning.
                    }
                }
            }

            // --- Now restart ALL clients fresh to scene 0 ---
            var restarter = GetComponent<FusionFullRestart>();
            if (restarter == null)
            {
                Debug.LogError("[Flow] FusionFullRestart component missing on NetworkGameFlowManager!");
                yield break;
            }

            restarter.RequestRestartAll();
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
            cons.SetActiveState(false);

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
            Vector3 pos = GetRandomConsumablePosition();
            RPC_RespawnConsumable(obj, pos);
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_RespawnConsumable(NetworkObject obj, Vector3 pos)
        {
            if (obj == null) return;

            var nt = obj.GetComponent<NetworkTransform>();
            if (nt != null) nt.Teleport(pos, obj.transform.rotation);
            else obj.transform.position = pos;


            var cons = obj.GetComponent<Project.Game.Consumables.NetworkConsumable>();
            if (cons != null)
                cons.SetActiveState(true);
        }


        [Rpc(RpcSources.All, RpcTargets.All)]
        public void RPC_ScheduleConsumableRespawn(NetworkObject consumableObj)
        {
            // Master only schedules respawn
            if (!Runner.IsSharedModeMasterClient || !Object.HasStateAuthority) return;
            if (State != MatchFlowState.Playing) return;
            if (consumableObj == null) return;

            StartCoroutine(RespawnConsumableAfter(consumableObj, consumableRespawnSeconds));
        }



    }
}
