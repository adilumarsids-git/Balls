using Fusion;
using UnityEngine;

namespace Project.Networking.Fusion
{
    public class PlayerSpawner : MonoBehaviour
    {
        [Header("Network Prefab")]
        [Tooltip("Optional direct reference. If empty, we load from Resources path below.")]
        [SerializeField] private NetworkObject playerPrefab;

        [Tooltip("Resources path for player prefab (without .prefab).")]
        [SerializeField] private string playerPrefabResourcesPath = "PlayerBall";

        private NetworkRunner runner;
        private Transform[] spawnPoints;

        [Header("Spawn Facing")]
        [SerializeField] private Transform mapCenter;
        [SerializeField] private string mapCenterName = "CenterPoint";

        // Keep track so we don't spawn twice for local player
        private NetworkObject localPlayerObject;

        public void Init(NetworkRunner r)
        {
            runner = r;
            EnsurePlayerPrefabLoaded();
        }

        public void EnsurePlayerPrefabLoaded()
        {
            if (playerPrefab != null)
                return;

            // Primary path requested: load PlayerBall from Resources.
            playerPrefab = Resources.Load<NetworkObject>(playerPrefabResourcesPath);

            // Secondary fallback if kept in subfolder like Resources/Networked/PlayerBall.prefab
            if (playerPrefab == null)
                playerPrefab = Resources.Load<NetworkObject>("Networked/PlayerBall");

            if (playerPrefab == null)
            {
                Debug.LogError($"[PlayerSpawner] Could not load player prefab from Resources. Tried '{playerPrefabResourcesPath}' and 'Networked/PlayerBall'.");
                return;
            }

            Debug.Log("[PlayerSpawner] Loaded PlayerBall prefab from Resources.");
        }

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
            EnsurePlayerPrefabLoaded();

            if (playerPrefab == null)
            {
                Debug.LogError("[PlayerSpawner] playerPrefab is null. Cannot spawn player.");
                return;
            }

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
            int index = (Mathf.Abs(player.RawEncoded) % usable) + 1;
            Transform t = spawnPoints[index];

            Quaternion spawnRotation = GetSpawnRotationFacingCenter(t.position);
            var obj = runner.Spawn(playerPrefab, t.position, spawnRotation, player);

            if (player == runner.LocalPlayer)
                localPlayerObject = obj;

            var ctrl = obj.GetComponent<NetworkPlayerController>();
            if (ctrl != null)
            {
                // StateAuthority for local player is local in Shared Mode (since we spawned it)
                ctrl.SetPlayerName(Project.Core.LocalProfile.GetName());
            }

            // Optional: register with match manager (fine for now)
            var match = FindObjectOfType<NetworkMatchManager>();
            if (match != null && obj != null)
                match.RegisterPlayer(obj);

            Debug.Log($"[Spawner] Local={runner.LocalPlayer} spawned for={player} obj.InputAuthority={obj.InputAuthority}");
        }

        private Quaternion GetSpawnRotationFacingCenter(Vector3 spawnPosition)
        {
            if (mapCenter == null && !string.IsNullOrWhiteSpace(mapCenterName))
            {
                var centerGo = GameObject.Find(mapCenterName);
                if (centerGo != null)
                    mapCenter = centerGo.transform;
            }

            Vector3 centerPos = mapCenter != null ? mapCenter.position : Vector3.zero;
            Vector3 toCenter = centerPos - spawnPosition;
            toCenter.y = 0f;

            if (toCenter.sqrMagnitude < 0.0001f)
                toCenter = Vector3.forward;

            return Quaternion.LookRotation(toCenter.normalized, Vector3.up);
        }
    }
}
