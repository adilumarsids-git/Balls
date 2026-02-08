using Fusion;
using UnityEngine;
using Project.Data;
using Project.Gameplay.Player.Stats;
using Fusion.Addons.Physics;

namespace Project.Networking.Fusion
{
    [RequireComponent(typeof(NetworkRigidbody3D))]
    public class NetworkPlayerController : NetworkBehaviour
    {
        [SerializeField] private TopDownMovementConfigSO moveConfig;
        [SerializeField] private GameConfigSO gameConfig;
        [Networked] private NetworkBool AuthorityRequested { get; set; }

        private NetworkRigidbody3D nrb;
        private Rigidbody rb;
        private PlayerStats stats;

        // Burst timers (networked)
        [Networked] private TickTimer BoostActive { get; set; }
        [Networked] private TickTimer BoostCooldown { get; set; }

        // Remember last direction for boost when input is tiny
        [Networked] private Vector2 LastMoveDir { get; set; }
        [Networked] public NetworkString<_16> PlayerName { get; private set; }
        [Networked] private float SpeedMul { get; set; } = 1f;
        [Networked] private float SizeMul { get; set; } = 1f;
        [Networked] private float MassMul { get; set; } = 1f;

        public override void Spawned()
        {
            nrb = GetComponent<NetworkRigidbody3D>();
            rb = nrb.Rigidbody;
            stats = GetComponent<PlayerStats>();

            rb.useGravity = true;
            if (LastMoveDir == Vector2.zero)
                LastMoveDir = Vector2.up;
            if (Object.HasStateAuthority && PlayerName.ToString().Length == 0)
            {
                SetPlayerName($"P{Object.InputAuthority.RawEncoded}");
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


        public override void FixedUpdateNetwork()
        {
            var flow = NetworkGameFlowManager.Instance;
            if (flow != null && flow.State != MatchFlowState.Playing)
            {
                if (Object.HasStateAuthority && rb != null)
                    rb.linearVelocity = Vector3.zero;
                return;
            }

            if (moveConfig == null || gameConfig == null) return;

            // Only the peer with StateAuthority should drive physics for this object.
            // In Shared Mode we will assign StateAuthority = InputAuthority for each player object.
            if (!Object.HasStateAuthority) return;

            if (!GetInput(out NetInput input))
                input = default;

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

            // Apply locally (StateAuthority simulates)
            transform.localScale *= sizeMul;

            if (rb != null)
                rb.mass *= massMul;
        }


    }
}
