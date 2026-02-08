using UnityEngine;
using Project.Gameplay.Player.Stats;

namespace Project.Gameplay.Player.Stats
{
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerBuffApplier : MonoBehaviour
    {
        private PlayerStats stats;
        private Rigidbody rb;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            rb = GetComponent<Rigidbody>();
        }

        public void ApplyBuff(float speedAdd, float massAdd, float sizeAdd, float sizeScaleFactor)
        {
            stats.AddBuff(speedAdd, massAdd, sizeAdd);

            // Apply mass (affects bump & physics feel)
            if (rb != null)
            {
                rb.mass = Mathf.Max(0.1f, rb.mass + massAdd);
            }

            // Apply size visually and physically (collider auto scales with transform)
            float scaleAdd = sizeAdd * sizeScaleFactor;
            var s = transform.localScale;
            s += Vector3.one * scaleAdd;
            transform.localScale = s;
        }
    }
}
