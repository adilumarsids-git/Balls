using Fusion;
using UnityEngine;
using Project.Data;
using Project.Gameplay.Player.Stats;

namespace Project.Networking.Fusion
{
    /// Forecast-Physics controller (Shared mode):
    /// - Uses NetworkTransform with Forecast Physics Enabled (NO NetworkRigidbody).
    /// - StateAuthority reads Fusion input in FixedUpdateNetwork() and writes to Networked fields.
    /// - ALL clients simulate Rigidbody physics in Unity FixedUpdate() using those Networked fields.
    ///
    /// Ball Feel:
    /// - No brakes (no stop damping)
    /// - Inertial steering (direction changes take time, especially at speed)
    /// - Optional small rolling resistance so it doesn't roll forever
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(NetworkTransform))]
    public class NetworkPlayerController : NetworkBehaviour
    {
        [Header("Configs (ScriptableObjects)")]
        [SerializeField] private TopDownMovementConfigSO moveConfig;
        [SerializeField] private GameConfigSO gameConfig;
<<<<<<< Updated upstream
        [Networked] private NetworkBool AuthorityRequested { get; set; }
=======

        [Header("Ball Feel Tweaks")]
        [Tooltip("How quickly the ball responds to steering. Lower = more slippery/drifty.")]
        [Range(0.1f, 2.0f)]
        [SerializeField] private float steeringResponsiveness = 0.55f;

        [Tooltip("Extra slowdown when trying to reverse direction (right -> left, forward -> back). Lower = harder to reverse.")]
        [Range(0.1f, 1.0f)]
        [SerializeField] private float reverseResponsiveness = 0.35f;

        [Tooltip("At high speed, turning becomes harder. 0 = no effect, 1 = strong effect.")]
        [Range(0f, 1f)]
        [SerializeField] private float highSpeedTurnReduction = 0.65f;

        [Tooltip("Very small resistance so the ball slowly loses speed over time. Set 0 for infinite rolling.")]
        [Range(0f, 1.5f)]
        [SerializeField] private float rollingResistance = 0.10f;

        [Tooltip("Sideways grip. Higher = less drifting. For ball feel keep this low-ish.")]
        [Range(0f, 2.5f)]
        [SerializeField] private float sidewaysGripMultiplier = 0.75f;
>>>>>>> Stashed changes

        private Rigidbody rb;
        private PlayerStats stats;

<<<<<<< Updated upstream
        // Burst timers (networked)
        [Networked] private TickTimer BoostActive { get; set; }
        [Networked] private TickTimer BoostCooldown { get; set; }

        // Remember last direction for boost when input is tiny
=======
        private Vector3 _baseScale;

        // ===== Networked INPUT (StateAuthority writes these) =====
        [Networked] private Vector2 MoveInput { get; set; }
        [Networked] private NetworkBool BoostHeld { get; set; }

        [Networked] private NetworkBool LastBoostHeld { get; set; }
        [Networked] private TickTimer BoostActiveTimer { get; set; }
        [Networked] private TickTimer BoostCooldownTimer { get; set; }

        // Other networked state
>>>>>>> Stashed changes
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
                rb.useGravity = true;

            if (LastMoveDir == Vector2.zero)
                LastMoveDir = Vector2.up;
            if (Object.HasStateAuthority && PlayerName.ToString().Length == 0)
            {
                SetPlayerName($"P{Object.InputAuthority.RawEncoded}");
<<<<<<< Updated upstream
            }

            // ✅ Critical: in Shared mode, clients must request StateAuthority for their own physics object
            if (Object.HasInputAuthority && !Object.HasStateAuthority && !AuthorityRequested)
            {
                AuthorityRequested = true;
                Object.RequestStateAuthority();
            }

            // Optional debug (remove later)
            Debug.Log($"Spawned {name} Local={Runner.LocalPlayer} InputAuth={Object.InputAuthority} HasInputAuth={Object.HasInputAuthority} HasStateAuth={Object.HasStateAuthority}");

        }


=======
        }

        /// INPUT MUST BE READ HERE (Fusion tick), NOT in Unity FixedUpdate.
>>>>>>> Stashed changes
        public override void FixedUpdateNetwork()
        {
            if (moveConfig == null || gameConfig == null) return;

            var flow = NetworkGameFlowManager.Instance;
            if (flow != null && flow.State != MatchFlowState.Playing)
            {
                if (Object.HasStateAuthority)
                {
                    MoveInput = Vector2.zero;
                    BoostHeld = false;

                    if (rb != null)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                }
                return;
            }

<<<<<<< Updated upstream
            if (moveConfig == null || gameConfig == null) return;

            // Only the peer with StateAuthority should drive physics for this object.
            // In Shared Mode we will assign StateAuthority = InputAuthority for each player object.
            if (!Object.HasStateAuthority) return;
=======
            if (!Object.HasStateAuthority)
                return;
>>>>>>> Stashed changes

            // Read Fusion input
            NetInput input;
            if (!GetInput(out input))
            {
                // no input this tick
                MoveInput = Vector2.zero;
                BoostHeld = false;
                return;
            }

<<<<<<< Updated upstream
            Vector2 move = input.Move;
            if (move.sqrMagnitude > 0.0001f)
                LastMoveDir = move;

            float speedBonus = stats != null ? stats.SpeedBonus : 0f;

            bool boostingNow = BoostActive.IsRunning;
            float multiplier = boostingNow ? gameConfig.boostMultiplier : 1f;

            float targetMax = (moveConfig.maxSpeed + speedBonus) * SpeedMul;
            float accel = moveConfig.acceleration * SpeedMul;

            // Current velocity XZ
            Vector2 v = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);

            // Desired velocity XZ
            Vector2 desired = move * (moveConfig.baseSpeed + SpeedMul);
            Vector2 delta = desired - v;

            // Accelerate
            Vector2 force = Vector2.ClampMagnitude(delta * accel, accel);
            rb.AddForce(new Vector3(force.x, 0f, force.y), ForceMode.Acceleration);

            // Stop damping
            if (move.sqrMagnitude < 0.0001f)
            {
                Vector2 damp = -v * moveConfig.stopDamping;
                rb.AddForce(new Vector3(damp.x, 0f, damp.y), ForceMode.Acceleration);
            }

            // Clamp XZ speed
            Vector2 v2 = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
            if (v2.magnitude > targetMax)
            {
                v2 = v2.normalized * targetMax;
                rb.linearVelocity = new Vector3(v2.x, rb.linearVelocity.y, v2.y);
            }

            // Handle press-to-burst boost
            if (input.Boost && !BoostCooldown.IsRunning)
            {
                Vector2 dir = move.sqrMagnitude > 0.0001f ? move : LastMoveDir;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    // impulse
                    rb.AddForce(new Vector3(dir.x, 0f, dir.y) * 6.5f, ForceMode.VelocityChange);

                    BoostActive = TickTimer.CreateFromSeconds(Runner, gameConfig.boostBurstDuration);
                    BoostCooldown = TickTimer.CreateFromSeconds(Runner, gameConfig.boostCooldown);
                }
            }
=======
            MoveInput = input.Move;
            BoostHeld = input.Boost;

            // Boost press edge
            bool boostPressed = BoostHeld && !LastBoostHeld;
            LastBoostHeld = BoostHeld;

            if (boostPressed && (!BoostCooldownTimer.IsRunning || BoostCooldownTimer.Expired(Runner)))
            {
                BoostActiveTimer = TickTimer.CreateFromSeconds(Runner, gameConfig.boostBurstDuration);
                BoostCooldownTimer = TickTimer.CreateFromSeconds(Runner, gameConfig.boostCooldown);
            }
        }

        /// PHYSICS SIM HAPPENS HERE (Unity FixedUpdate) for Forecast Physics.
        private void FixedUpdate()
        {
            if (rb == null || moveConfig == null || gameConfig == null) return;

            // Apply size locally for everyone
            transform.localScale = _baseScale * SizeMul;

            Vector2 input = MoveInput;
            bool hasInput = input.sqrMagnitude > 0.0001f;

            Vector2 inputDir = hasInput ? input.normalized : LastMoveDir;


            if (hasInput)
                LastMoveDir = inputDir; // keep local direction memory


            // Current planar velocity
            Vector2 v = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
            float speed = v.magnitude;

            // Boost multiplier
            bool boostActive = BoostActiveTimer.IsRunning && Runner != null && !BoostActiveTimer.Expired(Runner);
            float boostMultiplier = boostActive ? gameConfig.boostMultiplier : 1f;

            float speedBonus = stats != null ? stats.SpeedBonus : 0f;

            float baseSpeed = (moveConfig.baseSpeed + speedBonus) * SpeedMul * boostMultiplier;
            float maxSpeed = (moveConfig.maxSpeed + speedBonus) * SpeedMul * boostMultiplier;

            // We use acceleration as our "engine force"
            float accel = moveConfig.acceleration * SpeedMul;

            // Desired velocity (only if input is held). If no input: we do NOT brake.
            Vector2 desiredV = hasInput ? (inputDir * baseSpeed) : v;

            // --- Steering inertia / drift ---
            // How aligned are we with desired direction?
            Vector2 vDir = (speed > 0.001f) ? (v / speed) : inputDir;
            float dirDot = Vector2.Dot(vDir, inputDir); // -1 opposite, +1 same

            // Reverse steering is harder
            float resp = steeringResponsiveness;
            if (hasInput && dirDot < 0f)
                resp *= reverseResponsiveness;

            // Harder to turn at higher speeds
            if (hasInput && maxSpeed > 0.01f)
            {
                float speed01 = Mathf.Clamp01(speed / maxSpeed);
                float turnPenalty = Mathf.Lerp(1f, 1f - highSpeedTurnReduction, speed01);
                resp *= Mathf.Max(0.05f, turnPenalty);
            }

            // Compute velocity change we want (but we clamp how fast we can change)
            Vector2 dv = desiredV - v;

            // Clamp max steering acceleration per fixed step
            // (this is what creates that “keeps going a bit then turns” feel)
            float maxDv = accel * resp * Time.fixedDeltaTime;
            Vector2 dvClamped = Vector2.ClampMagnitude(dv, maxDv);

            // Apply as velocity change (very consistent ball control)
            rb.AddForce(new Vector3(dvClamped.x, 0f, dvClamped.y), ForceMode.VelocityChange);

            // --- Sideways grip (controls drift) ---
            // Low grip = slides sideways more (ball feels slippery)
            v = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
            speed = v.magnitude;

            if (speed > 0.001f)
            {
                Vector2 forward = v / speed;
                Vector2 lateral = v - forward * Vector2.Dot(v, forward);

                // Use your ScriptableObject linearDrag as the base, then multiply by our grip multiplier
                float grip = moveConfig.linearDrag * sidewaysGripMultiplier;

                // Apply lateral friction as acceleration (doesn't kill forward momentum too aggressively)
                Vector2 lateralFriction = -lateral * grip;
                rb.AddForce(new Vector3(lateralFriction.x, 0f, lateralFriction.y), ForceMode.Acceleration);
            }

            // --- Rolling resistance (optional tiny slowdown) ---
            // This is NOT braking; just prevents infinite rolling.
            if (rollingResistance > 0f && speed > 0.001f)
            {
                Vector2 resist = -v * rollingResistance;
                rb.AddForce(new Vector3(resist.x, 0f, resist.y), ForceMode.Acceleration);
            }

            // Clamp top speed
            v = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
            if (v.magnitude > maxSpeed)
            {
                v = v.normalized * maxSpeed;
                rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.y);
            }

            rb.mass = Mathf.Max(0.1f, rb.mass);
>>>>>>> Stashed changes
        }

        public void SetPlayerName(string name)
        {
            if (!Object.HasStateAuthority) return;

            if (string.IsNullOrWhiteSpace(name))
                name = $"P{Object.InputAuthority.RawEncoded}";

            // Trim to be safe
            name = name.Trim();
            if (name.Length > 16) name = name.Substring(0, 16);

            PlayerName = name;
            gameObject.name = name; // helps editor/hierarchy debugging
        }

        public void OnConsumablePickup(float sizeMul, float speedMul, float massMul)
        {
            // Only StateAuthority should change physics-affecting values
            if (!Object.HasStateAuthority) return;

            SizeMul *= sizeMul;
            SpeedMul = Mathf.Clamp(SpeedMul * speedMul, 0.5f, 3f);
            MassMul *= massMul;

<<<<<<< Updated upstream
            // Apply locally (StateAuthority simulates)
            transform.localScale *= sizeMul;

=======
>>>>>>> Stashed changes
            if (rb != null)
                rb.mass *= massMul;
        }

<<<<<<< Updated upstream

=======
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_ApplyConsumableToAuthority(float sizeMul, float speedMul, float massMul)
        {
            OnConsumablePickup(sizeMul, speedMul, massMul);
        }
>>>>>>> Stashed changes
    }
}
