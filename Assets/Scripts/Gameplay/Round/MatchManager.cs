using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Gameplay.Round
{
    public class MatchManager : MonoBehaviour
    {
        public static MatchManager Instance;

        [Header("Setup")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private Transform spawnPointsParent;

        [Header("Consumable")]
        [SerializeField] private GameObject consumablePrefab;
        [SerializeField] private Transform consumableSpawnParent;
        [SerializeField] private Project.Data.GameConfigSO gameConfig;

        private GameObject activeConsumable;

        private readonly List<GameObject> players = new List<GameObject> ();
        private int aliveCount;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            StartMatch();
        }

        // ===============================
        // MATCH FLOW
        // ===============================

        public void StartMatch()
        {
            ClearPlayers();
            SpawnPlayers();
            SpawnConsumable();

        }

        private void SpawnPlayers()
        {
            players.Clear();

            Transform[] spawns = spawnPointsParent.GetComponentsInChildren<Transform>();

            int index = 0;

            foreach (Transform t in spawns)
            {
                if (t == spawnPointsParent) continue;

                if (index >= 4) break;

                GameObject p = Instantiate(
                    playerPrefab,
                    t.position,
                    Quaternion.identity
                );

                players.Add(p);
                index++;
            }

            aliveCount = players.Count;
        }

        public void OnPlayerEliminated(GameObject player)
        {
            if (!players.Contains(player))
                return;

            aliveCount--;

            if (aliveCount <= 1)
            {
                StartCoroutine(EndRoundRoutine());
            }
        }

        private IEnumerator EndRoundRoutine()
        {
            yield return new WaitForSeconds(1f);

            GameObject winner = GetLastAlivePlayer();

            if (winner != null)
            {
                Debug.Log("Winner: " + winner.name);
                Project.Core.Events.GameEvents.RaiseRoundWinner(winner);

            }

            yield return new WaitForSeconds(2f);

            StartMatch();
        }

        private GameObject GetLastAlivePlayer()
        {
            foreach (var p in players)
            {
                if (p != null && p.activeSelf)
                    return p;
            }

            return null;
        }

        private void ClearPlayers()
        {
            foreach (var p in players)
            {
                if (p != null)
                    Destroy(p);
            }

            players.Clear();
        }

        private void SpawnConsumable()
        {
            if (consumablePrefab == null || consumableSpawnParent == null) return;

            // clear old
            if (activeConsumable != null)
                Destroy(activeConsumable);

            // pick random spawn child
            var points = consumableSpawnParent.GetComponentsInChildren<Transform>();
            var valid = new System.Collections.Generic.List<Transform>();
            foreach (var t in points)
                if (t != consumableSpawnParent) valid.Add(t);

            if (valid.Count == 0) return;

            var chosen = valid[Random.Range(0, valid.Count)];

            activeConsumable = Instantiate(consumablePrefab, chosen.position, Quaternion.identity);

            var pickup = activeConsumable.GetComponent<Project.Gameplay.Consumables.ConsumablePickup>();
            if (pickup != null)
                pickup.OnConsumed += HandleConsumableConsumed;
        }

        private void HandleConsumableConsumed(Project.Gameplay.Consumables.ConsumablePickup pickup)
        {
            if (pickup != null)
                pickup.OnConsumed -= HandleConsumableConsumed;

            StartCoroutine(RespawnConsumableRoutine());
        }

        private System.Collections.IEnumerator RespawnConsumableRoutine()
        {
            float wait = gameConfig != null ? gameConfig.consumableRespawnSeconds : 3f;
            yield return new WaitForSeconds(wait);
            SpawnConsumable();
        }

    }
}
