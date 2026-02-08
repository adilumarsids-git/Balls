using UnityEngine;

namespace Project.Data
{
    [CreateAssetMenu(menuName = "Project/Config/Game", fileName = "GameConfig")]
    public class GameConfigSO : ScriptableObject
    {
        public int maxPlayers = 4;

        [Header("Boost (press-to-burst)")]
        public float boostMultiplier = 1.5f;     // 150%
        public float boostBurstDuration = 0.35f; // seconds
        public float boostCooldown = 1.25f;      // seconds

        [Header("Consumable")]
        public float consumableRespawnSeconds = 3f;
    }
}
