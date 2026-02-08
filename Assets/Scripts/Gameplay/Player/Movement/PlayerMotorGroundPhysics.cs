using UnityEngine;
using Project.Data;
using Project.Gameplay.Player.Stats;

namespace Project.Gameplay.Player.Movement
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerMotorGroundPhysics : MonoBehaviour
    {
        [SerializeField] private TopDownMovementConfigSO config;

        private Rigidbody rb;
        private PlayerInputTopDown input;
        private PlayerStats stats;

        private float speedMultiplier = 1f;
        private float speedMultiplierUntil = -1f;

        // XZ direction (x,z)
        public Vector2 LastMoveDir { get; private set; } = Vector2.up;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            input = GetComponent<PlayerInputTopDown>();
            stats = GetComponent<PlayerStats>();

            // Ground movement
            rb.useGravity = true; // keep the ball on the island
            rb.constraints = RigidbodyConstraints.None; // keeps it stable; change if you want rolling visuals
        }

        private void FixedUpdate()
        {
            if (config == null || input == null) return;

            // Update burst multiplier timer
            if (Time.time > speedMultiplierUntil)
                speedMultiplier = 1f;

            rb.linearDamping = config.linearDrag;

            Vector2 move = input.Move;
            if (move.sqrMagnitude > 0.0001f)
                LastMoveDir = move;

            float bonus = stats != null ? stats.SpeedBonus : 0f;

            float targetMax = (config.maxSpeed + bonus) * speedMultiplier;
            float accel = config.acceleration * speedMultiplier;

            // Current velocity on XZ
            Vector2 v = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);

            // Desired velocity on XZ (W/S drives Z)
            Vector2 desired = move * (config.baseSpeed + bonus);
            Vector2 delta = desired - v;

            // Accelerate toward desired
            Vector2 force = Vector2.ClampMagnitude(delta * accel, accel);
            rb.AddForce(new Vector3(force.x, 0f, force.y), ForceMode.Acceleration);

            // If no input, damp faster to stop cleanly
            if (move.sqrMagnitude < 0.0001f)
            {
                Vector2 damp = -v * config.stopDamping;
                rb.AddForce(new Vector3(damp.x, 0f, damp.y), ForceMode.Acceleration);
            }

            // Clamp horizontal speed (XZ only)
            Vector2 v2 = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
            if (v2.magnitude > targetMax)
            {
                v2 = v2.normalized * targetMax;
                rb.linearVelocity = new Vector3(v2.x, rb.linearVelocity.y, v2.y);
            }
        }

        public void ApplySpeedMultiplier(float multiplier, float duration)
        {
            speedMultiplier = Mathf.Max(multiplier, 1f);
            speedMultiplierUntil = Time.time + Mathf.Max(0.01f, duration);
        }
    }
}
