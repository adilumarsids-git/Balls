using UnityEngine;

namespace Project.Game.Consumables
{
    public class NetworkConsumableSpawner : MonoBehaviour
    {
        [SerializeField] private Transform spawnPointsParent;

        private Transform[] points;

        private void Awake()
        {
            if (spawnPointsParent == null)
            {
                var go = GameObject.Find("ConsumableSpawnPoints");
                if (go != null) spawnPointsParent = go.transform;
            }

            points = spawnPointsParent != null
                ? spawnPointsParent.GetComponentsInChildren<Transform>(true)
                : null;
        }

        public Vector3 GetRandomSpawnPosition()
        {
            if (points == null || points.Length <= 1)
                return Vector3.zero;

            int idx = Random.Range(1, points.Length);
            return points[idx].position;
        }
    }
}
