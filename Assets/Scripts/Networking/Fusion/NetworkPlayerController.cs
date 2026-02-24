using Fusion;
using UnityEngine;
using Project.Data;
using Project.Gameplay.Player.Stats;

namespace Project.Networking.Fusion
{
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

        [Header("Collision Bump (IMPORTANT)")]
        [SerializeField] private float bumpStrength = 4.5f;
        [SerializeField] private float bumpSpeedFactor = 0.65f;
        [SerializeField] private float minBumpImpulse = 1.25f;
        [SerializeField] private float maxBumpImpulse = 10.0f;
        [SerializeField] private float bumpCooldownSeconds = 0.08f;

        private Rigidbody rb;
        private PlayerStats stats;
        private Vector3 _baseScale;

        // ===== Networked INPUT =====
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

        // ===== Networked Bump Event =====
        [Networked] private Vector3 BumpImpulse { get; set; }
        [Networked] private int BumpTick { get; set; }
        [Networked] private TickTimer BumpCooldown { get; set; }

        private int _lastAppliedBumpTick = -1;
        private bool _isNetworkSpawned;

        public override void Spawned()
        {
            _isNetworkSpawned = true;
            rb = GetComponent<Rigidbody>();
            stats = GetComponent<PlayerStats>();
            _baseScale = transform.localScale;

            rb.useGravity = true;

            // Recommended for shared/forecast
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            if (LastMoveDir == Vector2.zero)
                LastMoveDir = Vector2.up;

            if (Object.HasStateAuthority && PlayerName.ToString().Length == 0)
                SetPlayerName($"P{Object.InputAuthority.RawEncoded}");
        }

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

            // Only local owner drives input
            if (!Object.HasInputAuthority) return;

            NetInput input;
            if (!GetInput(out input))
            {
                if (Object.HasStateAuthority)
                {
                    MoveInput = Vector2.zero;
                    BoostHeld = false;
                }
                return;
            }

            // IMPORTANT: only StateAuthority writes Networked values
            if (Object.HasStateAuthority)
            {
                MoveInput = input.Move;
                BoostHeld = input.Boost;

                bool boostPressed = BoostHeld && !LastBoostHeld;
                LastBoostHeld = BoostHeld;

                if (boostPressed && (!BoostCooldownTimer.IsRunning || BoostCooldownTimer.Expired(Runner)))
                {
                    BoostActiveTimer = TickTimer.CreateFromSeconds(Runner, gameConfig.boostBurstDuration);
                    BoostCooldownTimer = TickTimer.CreateFromSeconds(Runner, gameConfig.boostCooldown);
                }
            }
        }


        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            _isNetworkSpawned = false;
        }

        private void FixedUpdate()
        {
            if (rb == null || moveConfig == null || gameConfig == null) return;

            // Apply size locally for everyone
            transform.localScale = _baseScale * SizeMul;

            // Apply bump impulse deterministically
            ApplyBumpIfNeeded();

            Vector2 input = MoveInput;
            bool hasInput = input.sqrMagnitude > 0.0001f;

            Vector2 inputDir = hasInput ? input.normalized : LastMoveDir;
            if (hasInput)
                LastMoveDir = inputDir;

            Vector2 v = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
            float speed = v.magnitude;

            bool boostActive = BoostActiveTimer.IsRunning && Runner != null && !BoostActiveTimer.Expired(Runner);
            float boostMultiplier = boostActive ? gameConfig.boostMultiplier : 1f;

            float speedBonus = stats != null ? stats.SpeedBonus : 0f;

            float baseSpeed = (moveConfig.baseSpeed + speedBonus) * SpeedMul * boostMultiplier;
            float maxSpeed = (moveConfig.maxSpeed + speedBonus) * SpeedMul * boostMultiplier;
            float accel = moveConfig.acceleration * SpeedMul;

            Vector2 desiredV = hasInput ? (inputDir * baseSpeed) : v;

            Vector2 vDir = (speed > 0.001f) ? (v / speed) : inputDir;
            float dirDot = Vector2.Dot(vDir, inputDir);

            float resp = steeringResponsiveness;
            if (hasInput && dirDot < 0f)
                resp *= reverseResponsiveness;

            if (hasInput && maxSpeed > 0.01f)
            {
                float speed01 = Mathf.Clamp01(speed / maxSpeed);
                float turnPenalty = Mathf.Lerp(1f, 1f - highSpeedTurnReduction, speed01);
                resp *= Mathf.Max(0.05f, turnPenalty);
            }

            Vector2 dv = desiredV - v;
            float maxDv = accel * resp * Time.fixedDeltaTime;
            Vector2 dvClamped = Vector2.ClampMagnitude(dv, maxDv);
            rb.AddForce(new Vector3(dvClamped.x, 0f, dvClamped.y), ForceMode.VelocityChange);

            v = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
            speed = v.magnitude;

            if (speed > 0.001f)
            {
                Vector2 forward = v / speed;
                Vector2 lateral = v - forward * Vector2.Dot(v, forward);

                float grip = moveConfig.linearDrag * sidewaysGripMultiplier;
                Vector2 lateralFriction = -lateral * grip;
                rb.AddForce(new Vector3(lateralFriction.x, 0f, lateralFriction.y), ForceMode.Acceleration);
            }

            if (rollingResistance > 0f && speed > 0.001f)
            {
                Vector2 resist = -v * rollingResistance;
                rb.AddForce(new Vector3(resist.x, 0f, resist.y), ForceMode.Acceleration);
            }

            v = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
            if (v.magnitude > maxSpeed)
            {
                v = v.normalized * maxSpeed;
                rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.y);
            }

            // Apply MassMul (your old code wasn’t really using it properly)
            rb.mass = Mathf.Max(0.1f, 1f * MassMul);
        }

        private void ApplyBumpIfNeeded()
        {
            if (BumpTick <= 0) return;
            if (_lastAppliedBumpTick == BumpTick) return;

            _lastAppliedBumpTick = BumpTick;

            if (BumpImpulse.sqrMagnitude > 0.000001f)
                rb.AddForce(BumpImpulse, ForceMode.Impulse);
        }

        private void OnCollisionEnter(Collision collision) => TryBump(collision);
        private void OnCollisionStay(Collision collision) => TryBump(collision);

        private void TryBump(Collision collision)
        {
            if (!Object || !Object.HasStateAuthority) return;
            if (Runner == null) return;

            if (BumpCooldown.IsRunning && !BumpCooldown.Expired(Runner))
                return;

            var otherCtrl = collision.collider.GetComponentInParent<NetworkPlayerController>();
            if (otherCtrl == null || otherCtrl == this) return;

            // Deterministic single-sender rule (prevents double-impulse)
            if (otherCtrl.Object != null && Object.Id.Raw > otherCtrl.Object.Id.Raw)
                return;

            var contact = collision.GetContact(0);

            // normal points from other to this, we want direction from this -> other
            Vector3 dirToOther = -contact.normal;
            dirToOther.y = 0f;

            if (dirToOther.sqrMagnitude < 0.0001f)
                dirToOther = (otherCtrl.transform.position - transform.position);

            dirToOther.y = 0f;
            dirToOther.Normalize();

            Vector3 myV = rb.linearVelocity; myV.y = 0f;
            Vector3 otherV = otherCtrl.rb != null ? otherCtrl.rb.linearVelocity : Vector3.zero; otherV.y = 0f;

            float relSpeed = (myV - otherV).magnitude;

            float impulseMag = bumpStrength + (relSpeed * bumpSpeedFactor);
            impulseMag = Mathf.Clamp(impulseMag, minBumpImpulse, maxBumpImpulse);

            Vector3 impulseToOther = dirToOther * impulseMag;
            Vector3 impulseToMe = -dirToOther * impulseMag;

            SetBumpImpulseAuthority(impulseToMe);
            otherCtrl.RPC_ReceiveBumpImpulse(impulseToOther);

            BumpCooldown = TickTimer.CreateFromSeconds(Runner, bumpCooldownSeconds);
        }

        private void SetBumpImpulseAuthority(Vector3 impulse)
        {
            if (!Object.HasStateAuthority) return;

            BumpImpulse = impulse;
            BumpTick = Runner != null ? Runner.Tick.Raw : (BumpTick + 1);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_ReceiveBumpImpulse(Vector3 impulse, RpcInfo info = default)
        {
            SetBumpImpulseAuthority(impulse);
        }


        public bool TryGetPlayerName(out string name)
        {
            name = null;

            if (!_isNetworkSpawned || Object == null || Runner == null || !Runner.IsRunning)
                return false;

            var value = PlayerName.ToString();
            if (string.IsNullOrWhiteSpace(value))
                return false;

            name = value;
            return true;
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

        public void ResetRoundModifiersAuthority()
        {
            if (!Object.HasStateAuthority) return;

            SpeedMul = 1f;
            SizeMul = 1f;
            MassMul = 1f;

            BoostHeld = false;
            LastBoostHeld = false;
            BoostActiveTimer = default;
            BoostCooldownTimer = default;
            BumpImpulse = Vector3.zero;
            BumpTick = 0;
            BumpCooldown = default;

            if (rb != null)
            {
                rb.mass = 1f;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_ResetRoundModifiers()
        {
            ResetRoundModifiersAuthority();
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