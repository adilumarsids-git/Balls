using Fusion;
using UnityEngine;

namespace Project.Networking.Fusion
{
    public class PlayerSpawner : MonoBehaviour
    {
        [Header("Network Prefab")]
        [SerializeField] private NetworkPrefabRef playerPrefab;

        [Header("Spawn Orientation")]
        [SerializeField] private Transform mapCenter;
        [SerializeField] private string mapCenterName = "CenterPoint";

        private NetworkRunner runner;
        private Transform[] spawnPoints;

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

            spawnPoints = parent.GetComponentsInChildren<Transform>(true);
            Debug.Log($"Found {spawnPoints.Length - 1} spawn points");

            ResolveMapCenter(parent.transform);
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

            if (player == runner.LocalPlayer && localPlayerObject != null)
                return;

            if (spawnPoints == null || spawnPoints.Length <= 1)
                RefreshSpawnPoints();

            if (spawnPoints == null || spawnPoints.Length <= 1)
            {
                Debug.LogWarning("Spawner not ready (SpawnPoints not found yet). Will retry after scene load.");
                return;
            }

            int usable = spawnPoints.Length - 1;
            int index = (Mathf.Abs(player.RawEncoded) % usable) + 1;
            Transform spawn = spawnPoints[index];

            Quaternion spawnRotation = ComputeSpawnRotationTowardsCenter(spawn.position, spawn.rotation);
            var obj = runner.Spawn(playerPrefab, spawn.position, spawnRotation, player);

            if (player == runner.LocalPlayer)
                localPlayerObject = obj;

            var ctrl = obj.GetComponent<NetworkPlayerController>();
            if (ctrl != null)
                ctrl.SetPlayerName(Project.Core.LocalProfile.GetName());

            var match = FindObjectOfType<NetworkMatchManager>();
            if (match != null && obj != null)
                match.RegisterPlayer(obj);

            Debug.Log($"[Spawner] Local={runner.LocalPlayer} spawned for={player} obj.InputAuthority={obj.InputAuthority}");
        }

        private void ResolveMapCenter(Transform spawnRoot)
        {
            if (mapCenter != null)
                return;

            var named = GameObject.Find(mapCenterName);
            if (named != null)
            {
                mapCenter = named.transform;
                return;
            }

            // Fallback: use spawn root origin as arena center.
            mapCenter = spawnRoot;
        }

        private Quaternion ComputeSpawnRotationTowardsCenter(Vector3 spawnPosition, Quaternion fallback)
        {
            if (mapCenter == null)
                return fallback;

            Vector3 toCenter = mapCenter.position - spawnPosition;
            toCenter.y = 0f;

            if (toCenter.sqrMagnitude < 0.0001f)
                return fallback;

            return Quaternion.LookRotation(toCenter.normalized, Vector3.up);
        }
    }
}
