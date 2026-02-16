using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Project.Networking.Fusion
{
    public class PlayerSpawner : MonoBehaviour
    {
        [Header("Network Prefab")]
        [SerializeField] private NetworkPrefabRef playerPrefab;

        private NetworkRunner runner;
        private Transform[] spawnPoints;
        private readonly Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();

        public void Init(NetworkRunner r) => runner = r;

        public void RefreshSpawnPoints()
        {
            var parent = GameObject.Find("SpawnPoints");
            if (parent == null)
            {
                Debug.LogError("SpawnPoints object not found in map! Make sure the map has a GameObject named exactly: SpawnPoints");
                spawnPoints = null;
                return;
            }

            // includes parent at index 0
            spawnPoints = parent.GetComponentsInChildren<Transform>(true);
            Debug.Log($"Found {spawnPoints.Length - 1} spawn points");
        }

        public void SpawnPlayerFor(PlayerRef player)
        {
            if (runner == null)
            {
                Debug.LogError("Spawner missing runner.");
                return;
            }

            if (!runner.IsServer)
                return;

            if (spawnedPlayers.TryGetValue(player, out var existing) && existing != null)
                return;

            // SpawnPoints might not exist yet (scene still loading) -> try refresh
            if (spawnPoints == null || spawnPoints.Length <= 1)
                RefreshSpawnPoints();

            if (spawnPoints == null || spawnPoints.Length <= 1)
            {
                Debug.LogWarning("Spawner not ready (SpawnPoints not found yet). Will retry after scene load.");
                return;
            }

            int usable = spawnPoints.Length - 1; // skip parent
            int index = (Mathf.Abs(player.RawEncoded) % usable) + 1;
            Transform t = spawnPoints[index];

            var obj = runner.Spawn(playerPrefab, t.position, t.rotation, player);
            spawnedPlayers[player] = obj;
            runner.SetPlayerObject(player, obj);

            var ctrl = obj.GetComponent<NetworkPlayerController>();
            if (ctrl != null)
                ctrl.SetPlayerName($"P{player.RawEncoded}");

            var match = FindObjectOfType<NetworkMatchManager>();
            if (match != null && obj != null)
                match.RegisterPlayer(obj);

            Debug.Log($"[Spawner] Server spawned for={player} obj.InputAuthority={obj.InputAuthority}");
        }

        public void ResetSpawnerState()
        {
            spawnedPlayers.Clear();
            spawnPoints = null;
        }

        public void DespawnPlayerFor(PlayerRef player)
        {
            if (runner == null || !runner.IsServer)
                return;

            if (!spawnedPlayers.TryGetValue(player, out var playerObject))
                return;

            spawnedPlayers.Remove(player);

            runner.SetPlayerObject(player, null);

            if (playerObject != null)
                runner.Despawn(playerObject);
        }
    }
}
