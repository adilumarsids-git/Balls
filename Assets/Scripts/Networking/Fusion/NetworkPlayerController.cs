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

        [Header("Local Camera")]
        [SerializeField] private float cameraDistance = 9f;
        [SerializeField] private float cameraHeight = 5f;
        [SerializeField] private float cameraLookHeight = 1.2f;
        [SerializeField] private float cameraPositionLerp = 10f;
        [SerializeField] private float cameraRotationLerp = 12f;

        [Networked] private NetworkBool AuthorityRequested { get; set; }

        private NetworkRigidbody3D nrb;
        private Rigidbody rb;
        private PlayerStats stats;
        private float noBrakeTimer;
        private Camera localCamera;
        private Vector3 cameraForward;
        private bool cameraInitialized;

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

            if (Object.HasInputAuthority)
                SetupLocalCamera();
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
            bool hasMoveInput = move.sqrMagnitude > 0.0001f;
            Vector2 inputDir = hasMoveInput ? move.normalized : LastMoveDir;
            if (hasMoveInput)
                LastMoveDir = inputDir;

            float speedBonus = stats != null ? stats.SpeedBonus : 0f;
            float boostMultiplier = input.Boost ? gameConfig.boostMultiplier : 1f;

            float targetSpeed = (moveConfig.baseSpeed + speedBonus) * SpeedMul * boostMultiplier;
            float targetMax = (moveConfig.maxSpeed + speedBonus) * SpeedMul * boostMultiplier;
            float accel = moveConfig.acceleration * SpeedMul;

            Vector2 planarVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
            float planarSpeed = planarVelocity.magnitude;

            // Realistic movement feel: keep some inertia, add traction and lateral friction instead of hard snaps.
            Vector2 desiredVelocity = hasMoveInput ? inputDir * targetSpeed : Vector2.zero;
            Vector2 velocityDelta = desiredVelocity - planarVelocity;

            // Acceleration scales down as we approach max speed to avoid sudden speed jumps.
            float speedRatio = targetMax > 0.0001f ? Mathf.Clamp01(planarSpeed / targetMax) : 0f;
            float accelScale = Mathf.Lerp(1f, 0.35f, speedRatio);
            Vector2 accelForce = Vector2.ClampMagnitude(velocityDelta * accel * accelScale, accel);
            rb.AddForce(new Vector3(accelForce.x, 0f, accelForce.y), ForceMode.Acceleration);

            // Lateral friction: preserves momentum but suppresses unnatural side-sliding.
            Vector2 heading = planarSpeed > 0.001f ? planarVelocity.normalized : inputDir;
            Vector2 forwardVel = heading * Vector2.Dot(planarVelocity, heading);
            Vector2 lateralVel = planarVelocity - forwardVel;
            float lateralGrip = hasMoveInput ? moveConfig.linearDrag : moveConfig.linearDrag * 1.5f;
            Vector2 lateralFriction = -lateralVel * lateralGrip;
            rb.AddForce(new Vector3(lateralFriction.x, 0f, lateralFriction.y), ForceMode.Acceleration);

            // When input is released, blend drag + stop damping for a smooth roll-down.
            if (!hasMoveInput && noBrakeTimer <= 0f)
            {
                Vector2 drag = -planarVelocity * moveConfig.linearDrag;
                Vector2 brake = -planarVelocity * moveConfig.stopDamping;
                Vector2 stopForce = drag + brake;
                rb.AddForce(new Vector3(stopForce.x, 0f, stopForce.y), ForceMode.Acceleration);
            }

            Vector2 clamped = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
            if (clamped.magnitude > targetMax)
            {
                clamped = clamped.normalized * targetMax;
                rb.linearVelocity = new Vector3(clamped.x, rb.linearVelocity.y, clamped.y);
            }
        }

        private void LateUpdate()
        {
            if (Object == null || !Object.HasInputAuthority)
                return;

            if (localCamera == null)
            {
                SetupLocalCamera();
                if (localCamera == null)
                    return;
            }

            Vector3 focus = transform.position + Vector3.up * cameraLookHeight;
            Vector3 desiredPos = focus - cameraForward * cameraDistance + Vector3.up * (cameraHeight - cameraLookHeight);

            float posT = 1f - Mathf.Exp(-cameraPositionLerp * Time.deltaTime);
            float rotT = 1f - Mathf.Exp(-cameraRotationLerp * Time.deltaTime);

            Transform camTransform = localCamera.transform;
            if (!cameraInitialized)
            {
                camTransform.position = desiredPos;
                camTransform.rotation = Quaternion.LookRotation((focus - desiredPos).normalized, Vector3.up);
                cameraInitialized = true;
                return;
            }

            camTransform.position = Vector3.Lerp(camTransform.position, desiredPos, posT);

            Quaternion desiredRot = Quaternion.LookRotation((focus - camTransform.position).normalized, Vector3.up);
            camTransform.rotation = Quaternion.Slerp(camTransform.rotation, desiredRot, rotT);
        }

        private void SetupLocalCamera()
        {
            if (localCamera == null)
                localCamera = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();

            cameraForward = ResolveSpawnForward();
            cameraInitialized = false;
        }

        private Vector3 ResolveSpawnForward()
        {
            var spawnParent = GameObject.Find("SpawnPoints");
            if (spawnParent != null)
            {
                var points = spawnParent.GetComponentsInChildren<Transform>(true);
                int usable = points.Length - 1;
                if (usable > 0)
                {
                    int playerKey = Object != null ? Mathf.Abs(Object.InputAuthority.RawEncoded) : 0;
                    int index = (playerKey % usable) + 1;
                    Transform assignedSpawn = points[index];

                    Vector3 spawnForward = Vector3.ProjectOnPlane(assignedSpawn.forward, Vector3.up);
                    if (spawnForward.sqrMagnitude > 0.0001f)
                        return spawnForward.normalized;
                }

                Vector3 toCenter = Vector3.ProjectOnPlane(spawnParent.transform.position - transform.position, Vector3.up);
                if (toCenter.sqrMagnitude > 0.0001f)
                    return toCenter.normalized;
            }

            Vector3 forwardOnPlane = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            return forwardOnPlane.sqrMagnitude > 0.0001f ? forwardOnPlane.normalized : Vector3.forward;
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

            var otherController = collision.rigidbody.GetComponentInParent<NetworkPlayerController>();
            if (otherController == null || otherController.Object == null)
                return;

            // Single deterministic resolver to avoid double-applying when both sides receive collision callbacks.
            if (Object.InputAuthority.RawEncoded >= otherController.Object.InputAuthority.RawEncoded)
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

            // Apply equal/opposite impulse split by mass.
            float reducedMass = (myMass * otherMass) / (myMass + otherMass);
            float impulse = relImpact * reducedMass * bumpExaggeration;

            Vector2 myDelta = away * (impulse / myMass);
            Vector2 otherDelta = -away * (impulse / otherMass);

            ApplyBumpDelta(myDelta);

            otherController.RPC_ApplyBumpToAuthority(otherDelta);
        }


        private void ApplyBumpDelta(Vector2 planarDelta)
        {
            if (rb == null)
                return;

            Vector2 planar = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
            planar += planarDelta;
            planar *= bumpDamping;

            rb.linearVelocity = new Vector3(planar.x, rb.linearVelocity.y, planar.y);
            noBrakeTimer = Mathf.Max(noBrakeTimer, postBumpNoBrakeSeconds);
            rb.WakeUp();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_ApplyBumpToAuthority(Vector2 planarDelta)
        {
            ApplyBumpDelta(planarDelta);
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
