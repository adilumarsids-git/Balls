using UnityEngine;
using Project.Gameplay.Player.Stats;

namespace Project.Gameplay.Player.CombatPush
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerBumpResponder : MonoBehaviour
    {
        [Header("Tuning")]
        public float bumpImpulse = 5.0f;
        public float minImpactSpeed = 1.5f;
        public float massInfluence = 0.6f;

        private Rigidbody rb;
        private PlayerStats stats;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            stats = GetComponent<PlayerStats>();
        }

        private void OnCollisionEnter(Collision collision)
        {

            if (!collision.rigidbody) return;
            if (collision.gameObject.GetComponentInParent<Project.Gameplay.Player.PlayerTag>() == null) return;

            // Only bump other players/balls (tag later for cleanliness)
            // For now we bump anything with a rigidbody.
            Vector3 rel = collision.relativeVelocity;
            float impactSpeed = new Vector2(rel.x, rel.y).magnitude;
            if (impactSpeed < minImpactSpeed) return;

            float myMassBonus = stats != null ? stats.MassBonus : 0f;
            float myMassFactor = 1f + myMassBonus * massInfluence;

            // Push the other body away from me in XY
            Vector3 away = (collision.transform.position - transform.position);
            away.z = 0f;
            if (away.sqrMagnitude < 0.0001f) return;
            away.Normalize();

            collision.rigidbody.AddForce(away * bumpImpulse * myMassFactor, ForceMode.VelocityChange);
        }
    }
}
