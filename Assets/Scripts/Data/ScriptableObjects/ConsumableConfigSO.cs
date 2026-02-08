using UnityEngine;

namespace Project.Data
{
    [CreateAssetMenu(menuName = "Project/Config/Consumable", fileName = "ConsumableConfig")]
    public class ConsumableConfigSO : ScriptableObject
    {
        public float speedAdd = 0.5f;
        public float massAdd = 0.5f;
        public float sizeAdd = 0.12f;

        [Header("Visual scaling")]
        public float sizeScaleFactor = 1.0f; // multiply sizeAdd by this for transform scale
    }
}
