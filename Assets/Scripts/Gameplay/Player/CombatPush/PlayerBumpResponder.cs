using UnityEngine;
using Project.Gameplay.Player.Stats;

namespace Project.Gameplay.Player.CombatPush
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerBumpResponder : MonoBehaviour
    {
        [Header("Tuning")]
        [Tooltip("Multiplier applied to the bounce-back speed when two players collide.")]
        public float bounceSpeedMultiplier = 1.25f;
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
            if (rb == null) return;

            var otherBody = collision.rigidbody;

            // Handle each collision pair once so we do not double-apply bounce impulses.
            if (rb.GetInstanceID() > otherBody.GetInstanceID())
                return;

            Vector3 rel = collision.relativeVelocity;
            float impactSpeed = new Vector2(rel.x, rel.y).magnitude;
            if (impactSpeed < minImpactSpeed) return;

            float myMassBonus = stats != null ? stats.MassBonus : 0f;
            float myMassFactor = 1f + myMassBonus * massInfluence;
            var otherStats = otherBody.GetComponent<PlayerStats>();
            float otherMassBonus = otherStats != null ? otherStats.MassBonus : 0f;
            float otherMassFactor = 1f + otherMassBonus * massInfluence;

            Vector3 away = (collision.transform.position - transform.position);
            away.z = 0f;
            if (away.sqrMagnitude < 0.0001f) return;
            away.Normalize();

            Vector2 collisionAxis = new Vector2(away.x, away.y);
            Vector2 myVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y);
            Vector2 otherVelocity = new Vector2(otherBody.linearVelocity.x, otherBody.linearVelocity.y);

            float myAxisSpeed = Vector2.Dot(myVelocity, collisionAxis);
            float otherAxisSpeed = Vector2.Dot(otherVelocity, collisionAxis);

            Vector2 tangent = new Vector2(-collisionAxis.y, collisionAxis.x);
            float myTangentSpeed = Vector2.Dot(myVelocity, tangent);
            float otherTangentSpeed = Vector2.Dot(otherVelocity, tangent);

            float averageAxisSpeed = (Mathf.Abs(myAxisSpeed) + Mathf.Abs(otherAxisSpeed)) * 0.5f;
            float exaggeratedBounceSpeed = Mathf.Max(minImpactSpeed, averageAxisSpeed) * bounceSpeedMultiplier;

            float myDirection = myAxisSpeed != 0f ? Mathf.Sign(myAxisSpeed) : -1f;
            float otherDirection = otherAxisSpeed != 0f ? Mathf.Sign(otherAxisSpeed) : 1f;

            float targetMyAxisSpeed = -myDirection * exaggeratedBounceSpeed * otherMassFactor;
            float targetOtherAxisSpeed = -otherDirection * exaggeratedBounceSpeed * myMassFactor;

            Vector2 myTargetVelocity = collisionAxis * targetMyAxisSpeed + tangent * myTangentSpeed;
            Vector2 otherTargetVelocity = collisionAxis * targetOtherAxisSpeed + tangent * otherTangentSpeed;

            Vector2 myDelta = myTargetVelocity - myVelocity;
            Vector2 otherDelta = otherTargetVelocity - otherVelocity;

            rb.AddForce(new Vector3(myDelta.x, myDelta.y, 0f), ForceMode.VelocityChange);
            otherBody.AddForce(new Vector3(otherDelta.x, otherDelta.y, 0f), ForceMode.VelocityChange);
        }
    }
}
