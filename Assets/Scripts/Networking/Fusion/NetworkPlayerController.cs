using Fusion;
using UnityEngine;
using Project.Data;
using Project.Gameplay.Player.Stats;

namespace Project.Networking.Fusion
{
    /// Shared mode controller tuned for low-latency feel:
    /// - Input is consumed in FixedUpdateNetwork (tick-accurate)
    /// - Only StateAuthority simulates Rigidbody physics
    /// - Remote proxies are kinematic and rendered via NetworkTransform interpolation
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(NetworkTransform))]
    public class NetworkPlayerController : NetworkBehaviour
    {
        [Header("Configs (ScriptableObjects)")]
        [SerializeField] private TopDownMovementConfigSO moveConfig;
        [SerializeField] private GameConfigSO gameConfig;

        [Header("Ball Feel Tweaks")]
        [Range(0.1f, 2.0f)]
        [SerializeField] private float steeringResponsiveness = 0.55f;

        [Range(0.1f, 1.0f)]
        [SerializeField] private float reverseResponsiveness = 0.35f;

        [Range(0f, 1f)]
        [SerializeField] private float highSpeedTurnReduction = 0.65f;

        [Range(0f, 1.5f)]
        [SerializeField] private float rollingResistance = 0.10f;

        [Range(0f, 2.5f)]
        [SerializeField] private float sidewaysGripMultiplier = 0.75f;

        private Rigidbody rb;
        private PlayerStats stats;
        private Vector3 _baseScale;

        [Networked] private Vector2 MoveInput { get; set; }
        [Networked] private NetworkBool BoostHeld { get; set; }
        [Networked] private NetworkBool LastBoostHeld { get; set; }
        [Networked] private TickTimer BoostActiveTimer { get; set; }
        [Networked] private TickTimer BoostCooldownTimer { get; set; }

        [Networked] private Vector2 LastMoveDir { get; set; }
        [Networked] public NetworkString<_16> PlayerName { get; private set; }

        [Networked] private float SpeedMul { get; set; } = 1f;
        [Networked] private float SizeMul { get; set; } = 1f;
        [Networked] private float MassMul { get; set; } = 1f;

        public override void Spawned()
        {
            rb = GetComponent<Rigidbody>();
            stats = GetComponent<PlayerStats>();
            _baseScale = transform.localScale;

            if (LastMoveDir == Vector2.zero)
                LastMoveDir = Vector2.up;

            if (Object.HasStateAuthority && PlayerName.ToString().Length == 0)
                SetPlayerName($"P{Object.InputAuthority.RawEncoded}");

            ConfigurePhysicsAuthorityMode();
        }

        public override void FixedUpdateNetwork()
        {
            if (rb == null || moveConfig == null || gameConfig == null)
                return;

            ConfigurePhysicsAuthorityMode();
            ApplyVisualAndMassState();

            if (!Object.HasStateAuthority)
                return;

            var flow = NetworkGameFlowManager.Instance;
            if (flow != null && flow.IsReady && flow.State != MatchFlowState.Playing)
            {
                MoveInput = Vector2.zero;
                BoostHeld = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                return;
            }

            NetInput input;
            if (GetInput(out input))
            {
                MoveInput = input.Move;
                BoostHeld = input.Boost;
            }
            else
            {
                MoveInput = Vector2.zero;
                BoostHeld = false;
            }

            bool boostPressed = BoostHeld && !LastBoostHeld;
            LastBoostHeld = BoostHeld;

            if (boostPressed && (!BoostCooldownTimer.IsRunning || BoostCooldownTimer.Expired(Runner)))
            {
                BoostActiveTimer = TickTimer.CreateFromSeconds(Runner, gameConfig.boostBurstDuration);
                BoostCooldownTimer = TickTimer.CreateFromSeconds(Runner, gameConfig.boostCooldown);
            }

            SimulateOwnedPhysics(Runner.DeltaTime);
        }

        private void ConfigurePhysicsAuthorityMode()
        {
            bool authoritative = Object != null && Object.HasStateAuthority;
            rb.isKinematic = !authoritative;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.useGravity = true;

            // Remote proxies should never integrate stale local velocities.
            if (!authoritative)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        private void ApplyVisualAndMassState()
        {
            transform.localScale = _baseScale * SizeMul;

            float targetMass = Mathf.Max(0.1f, MassMul + (stats != null ? stats.MassBonus : 0f));
            if (Mathf.Abs(rb.mass - targetMass) > 0.001f)
                rb.mass = targetMass;
        }

        private void SimulateOwnedPhysics(float dt)
        {
            Vector2 input = MoveInput;
            bool hasInput = input.sqrMagnitude > 0.0001f;
            Vector2 inputDir = hasInput ? input.normalized : LastMoveDir;

            if (hasInput)
                LastMoveDir = inputDir;

            Vector2 velocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
            float speed = velocity.magnitude;

            bool boostActive = BoostActiveTimer.IsRunning && !BoostActiveTimer.Expired(Runner);
            float boostMultiplier = boostActive ? gameConfig.boostMultiplier : 1f;
            float speedBonus = stats != null ? stats.SpeedBonus : 0f;

            float baseSpeed = (moveConfig.baseSpeed + speedBonus) * SpeedMul * boostMultiplier;
            float maxSpeed = (moveConfig.maxSpeed + speedBonus) * SpeedMul * boostMultiplier;
            float accel = moveConfig.acceleration * SpeedMul;

            Vector2 desiredVelocity = hasInput ? (inputDir * baseSpeed) : velocity;

            Vector2 velDir = speed > 0.001f ? (velocity / speed) : inputDir;
            float dirDot = Vector2.Dot(velDir, inputDir);

            float response = steeringResponsiveness;
            if (hasInput && dirDot < 0f)
                response *= reverseResponsiveness;

            if (hasInput && maxSpeed > 0.01f)
            {
                float speed01 = Mathf.Clamp01(speed / maxSpeed);
                float turnPenalty = Mathf.Lerp(1f, 1f - highSpeedTurnReduction, speed01);
                response *= Mathf.Max(0.05f, turnPenalty);
            }

            Vector2 velocityDelta = desiredVelocity - velocity;
            float maxDelta = accel * response * dt;
            Vector2 clampedDelta = Vector2.ClampMagnitude(velocityDelta, maxDelta);
            rb.AddForce(new Vector3(clampedDelta.x, 0f, clampedDelta.y), ForceMode.VelocityChange);

            velocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
            speed = velocity.magnitude;

            if (speed > 0.001f)
            {
                Vector2 forward = velocity / speed;
                Vector2 lateral = velocity - forward * Vector2.Dot(velocity, forward);
                float grip = moveConfig.linearDrag * sidewaysGripMultiplier;
                Vector2 lateralFriction = -lateral * grip;
                rb.AddForce(new Vector3(lateralFriction.x, 0f, lateralFriction.y), ForceMode.Acceleration);
            }

            if (rollingResistance > 0f && speed > 0.001f)
            {
                Vector2 resist = -velocity * rollingResistance;
                rb.AddForce(new Vector3(resist.x, 0f, resist.y), ForceMode.Acceleration);
            }

            velocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
            if (velocity.magnitude > maxSpeed)
            {
                velocity = velocity.normalized * maxSpeed;
                rb.linearVelocity = new Vector3(velocity.x, rb.linearVelocity.y, velocity.y);
            }
        }

        public void SetPlayerName(string name)
        {
            if (!Object.HasStateAuthority) return;

            if (string.IsNullOrWhiteSpace(name))
                name = $"P{Object.InputAuthority.RawEncoded}";

            name = name.Trim();
            if (name.Length > 16) name = name.Substring(0, 16);

            PlayerName = name;
            gameObject.name = name;
        }

        public void OnConsumablePickup(float sizeMul, float speedMul, float massMul)
        {
            if (!Object.HasStateAuthority) return;

            SizeMul *= sizeMul;
            SpeedMul = Mathf.Clamp(SpeedMul * speedMul, 0.5f, 4f);
            MassMul *= massMul;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_ApplyConsumableToAuthority(float sizeMul, float speedMul, float massMul)
        {
            OnConsumablePickup(sizeMul, speedMul, massMul);
        }
    }
}
