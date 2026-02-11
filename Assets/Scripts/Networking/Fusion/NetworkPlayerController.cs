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

        [Header("Bump")]
        [SerializeField] private float minBumpImpactSpeed = 1.25f;
        [SerializeField] private float bumpExaggeration = 1.35f;
        [SerializeField] private float bumpDamping = 0.95f;
        [SerializeField] private float postBumpNoBrakeSeconds = 0.2f;

        [Networked] private NetworkBool AuthorityRequested { get; set; }

        private NetworkRigidbody3D nrb;
        private Rigidbody rb;
        private PlayerStats stats;
        private float noBrakeTimer;

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
                SetPlayerName($"P{Object.InputAuthority.RawEncoded}");

            if (Object.HasInputAuthority && !Object.HasStateAuthority && !AuthorityRequested)
            {
                AuthorityRequested = true;
                Object.RequestStateAuthority();
            }
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

            if (moveConfig == null || gameConfig == null || !Object.HasStateAuthority)
                return;

            if (!GetInput(out NetInput input))
                input = default;

            if (noBrakeTimer > 0f)
                noBrakeTimer = Mathf.Max(0f, noBrakeTimer - Runner.DeltaTime);

            Vector2 move = input.Move;
            if (move.sqrMagnitude > 0.0001f)
                LastMoveDir = move;

            float speedBonus = stats != null ? stats.SpeedBonus : 0f;
            float boostMultiplier = input.Boost ? gameConfig.boostMultiplier : 1f;

            float baseSpeed = (moveConfig.baseSpeed + speedBonus) * SpeedMul;
            float targetSpeed = baseSpeed * boostMultiplier;
            float targetMax = (moveConfig.maxSpeed + speedBonus) * SpeedMul * boostMultiplier;
            float accel = moveConfig.acceleration * SpeedMul * boostMultiplier;

            Vector2 planarVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
            Vector2 desired = move * targetSpeed;
            Vector2 delta = desired - planarVelocity;

            Vector2 force = Vector2.ClampMagnitude(delta * accel, accel);
            rb.AddForce(new Vector3(force.x, 0f, force.y), ForceMode.Acceleration);

            if (move.sqrMagnitude < 0.0001f && noBrakeTimer <= 0f)
            {
                Vector2 damp = -planarVelocity * moveConfig.stopDamping;
                rb.AddForce(new Vector3(damp.x, 0f, damp.y), ForceMode.Acceleration);
            }

            Vector2 clamped = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
            if (clamped.magnitude > targetMax)
            {
                clamped = clamped.normalized * targetMax;
                rb.linearVelocity = new Vector3(clamped.x, rb.linearVelocity.y, clamped.y);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!Object.HasStateAuthority || rb == null)
                return;

            if (collision.rigidbody == null)
                return;

            if (collision.gameObject.GetComponentInParent<Project.Gameplay.Player.PlayerTag>() == null)
                return;

            if (collision.contactCount == 0)
                return;

            Vector3 contactNormal = collision.contacts[0].normal;
            Vector2 away = new Vector2(contactNormal.x, contactNormal.z);
            if (away.sqrMagnitude < 0.0001f)
                return;

            away.Normalize();

            Vector2 myV = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
            Vector2 otherV = new Vector2(collision.rigidbody.linearVelocity.x, collision.rigidbody.linearVelocity.z);

            float relImpact = Mathf.Abs(Vector2.Dot(myV - otherV, away));
            if (relImpact < minBumpImpactSpeed)
                return;

            float myMass = Mathf.Max(0.1f, rb.mass);
            float otherMass = Mathf.Max(0.1f, collision.rigidbody.mass);

            // Robust symmetric shove: each peer applies equal/opposite-style delta-v
            // to its own authoritative body based on relative impact and reduced mass.
            float reducedMass = (myMass * otherMass) / (myMass + otherMass);
            float impulse = relImpact * reducedMass * bumpExaggeration;
            float deltaSpeed = impulse / myMass;

            Vector2 newMyPlanar = myV + away * deltaSpeed;
            newMyPlanar *= bumpDamping;

            rb.linearVelocity = new Vector3(newMyPlanar.x, rb.linearVelocity.y, newMyPlanar.y);
            noBrakeTimer = Mathf.Max(noBrakeTimer, postBumpNoBrakeSeconds);
            rb.WakeUp();
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

            transform.localScale *= sizeMul;

            if (rb != null)
                rb.mass *= massMul;
        }
    }
}
