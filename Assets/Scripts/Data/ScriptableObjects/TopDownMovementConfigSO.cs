using UnityEngine;

namespace Project.Data
{
    [CreateAssetMenu(menuName = "Project/Config/TopDown Movement", fileName = "TopDownMovementConfig")]
    public class TopDownMovementConfigSO : ScriptableObject
    {
        [Header("Base")]
        public float baseSpeed = 8f;
        public float acceleration = 40f;
        public float maxSpeed = 10f;

        [Header("Feel")]
        public float linearDrag = 6f;
        public float stopDamping = 12f;
    }
}
