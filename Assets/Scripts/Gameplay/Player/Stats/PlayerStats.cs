using UnityEngine;

namespace Project.Gameplay.Player.Stats
{
    public class PlayerStats : MonoBehaviour
    {
        [Header("Runtime (buffed)")]
        [SerializeField] private float speedBonus = 0f;
        [SerializeField] private float massBonus = 0f;
        [SerializeField] private float sizeBonus = 0f;

        public float SpeedBonus => speedBonus;
        public float MassBonus => massBonus;
        public float SizeBonus => sizeBonus;

        public void AddBuff(float speedAdd, float massAdd, float sizeAdd)
        {
            speedBonus += speedAdd;
            massBonus += massAdd;
            sizeBonus += sizeAdd;
        }
    }
}
