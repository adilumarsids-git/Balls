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

        // Keep track so we don't spawn twice for local player
        private NetworkObject localPlayerObject;

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

        public void EnsureLocalPlayerSpawned()
        {
            if (runner == null || runner.LocalPlayer == PlayerRef.None) return;
            if (localPlayerObject != null && localPlayerObject.gameObject != null)
                return;

            SpawnPlayerFor(runner.LocalPlayer);
        }

        public void SpawnPlayerFor(PlayerRef player)
        {
            if (runner == null)
            {
                Debug.LogError("Spawner missing runner.");
                return;
            }

            // If this is local player and already spawned, do nothing
            if (player == runner.LocalPlayer && localPlayerObject != null)
                return;

            // SpawnPoints might not exist yet (scene still loading) → try refresh
            if (spawnPoints == null || spawnPoints.Length <= 1)
                RefreshSpawnPoints();

            if (spawnPoints == null || spawnPoints.Length <= 1)
            {
                Debug.LogWarning("Spawner not ready (SpawnPoints not found yet). Will retry after scene load.");
                return;
            }

            int usable = spawnPoints.Length - 1; // skip parent
            int spawnIndex = Mathf.Abs(player.RawEncoded) % usable;
            int index = spawnIndex + 1;
            Transform t = spawnPoints[index];

            var obj = runner.Spawn(playerPrefab, t.position, t.rotation, player);

            if (player == runner.LocalPlayer)
                localPlayerObject = obj;

            var ctrl = obj.GetComponent<NetworkPlayerController>();
            if (ctrl != null)
            {
                // StateAuthority for local player is local in Shared Mode (since we spawned it)
                ctrl.SetPlayerName(Project.Core.LocalProfile.GetName());
                ctrl.SetSpawnIndex(spawnIndex);

            }

            // Optional: register with match manager (fine for now)
            var match = FindObjectOfType<NetworkMatchManager>();
            if (match != null && obj != null)
                match.RegisterPlayer(obj);

            Debug.Log($"[Spawner] Local={runner.LocalPlayer} spawned for={player} spawnIndex={spawnIndex} obj.InputAuthority={obj.InputAuthority}");
        }
    }
}
