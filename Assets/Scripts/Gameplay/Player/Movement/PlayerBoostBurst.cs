using UnityEngine;
using Project.Data;

namespace Project.Gameplay.Player.Movement
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerBoostBurst : MonoBehaviour
    {
        [SerializeField] private GameConfigSO gameConfig;
        [SerializeField] private float burstImpulse = 6.5f; // tuning

        private Rigidbody rb;
        private PlayerInputTopDown input;
        private PlayerMotorGroundPhysics motor;

        private float nextBoostTime = 0f;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            input = GetComponent<PlayerInputTopDown>();
            motor = GetComponent<PlayerMotorGroundPhysics>();
        }

        private void Update()
        {
            if (gameConfig == null || input == null || motor == null) return;

            if (!input.BoostPressed) return;
            input.ConsumeBoostPressed();

            if (Time.time < nextBoostTime) return;

            // Only boost if actually moving (prevents free impulse spam)
            Vector2 dir = input.Move.sqrMagnitude > 0.0001f ? input.Move : motor.LastMoveDir;
            if (dir.sqrMagnitude < 0.0001f) return;

            // Apply burst impulse + temporary speed multiplier
            rb.AddForce(new Vector3(dir.x, 0f, dir.y) * burstImpulse, ForceMode.VelocityChange);
            motor.ApplySpeedMultiplier(gameConfig.boostMultiplier, gameConfig.boostBurstDuration);

            nextBoostTime = Time.time + gameConfig.boostCooldown;
        }
    }
}
