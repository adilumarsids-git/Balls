using Fusion;
using UnityEngine;
using Project.Gameplay.Player;
using Project.Data;
using Project.Networking.Fusion;

namespace Project.Game.Consumables
{
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkConsumable : NetworkBehaviour
    {
        [Networked] public NetworkBool IsActive { get; private set; } = true;

        [SerializeField] private ConsumableConfigSO config;

        private Renderer[] cachedRenderers;
        private Collider[] cachedColliders;

        private bool _lastActive;

        public override void Spawned()
        {
            CacheComponents();
            _lastActive = IsActive;
            SetActiveVisual(IsActive);
        }

        public override void Render()
        {
            if (_lastActive != IsActive)
            {
                _lastActive = IsActive;
                SetActiveVisual(IsActive);
            }
        }

        private void CacheComponents()
        {
            if (cachedRenderers == null || cachedRenderers.Length == 0)
                cachedRenderers = GetComponentsInChildren<Renderer>(true);

            if (cachedColliders == null || cachedColliders.Length == 0)
                cachedColliders = GetComponentsInChildren<Collider>(true);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsActive) return;

            var playerTag = other.GetComponentInParent<PlayerTag>();
            if (playerTag == null) return;

            var playerObj = playerTag.NetObj;
            if (playerObj == null) return;

            // Only the player's StateAuthority reports pickup (prevents doubles)
            if (!playerObj.HasStateAuthority) return;

            var flow = NetworkGameFlowManager.Instance;
            if (flow == null || !flow.IsReady) return;
            if (flow.State != MatchFlowState.Playing) return;

            if (config == null) return;

            float sizeMul = 1f + (config.sizeAdd * config.sizeScaleFactor);
            float speedMul = 1f + config.speedAdd;
            float massMul = 1f + config.massAdd;

            // ✅ process pickup on consumable authority (closest player via DistanceBasedAuthority)
            RPC_RequestPickup(playerObj, sizeMul, speedMul, massMul);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestPickup(NetworkObject playerObj, float sizeMul, float speedMul, float massMul)
        {
            if (!IsActive) return;

            var flow = NetworkGameFlowManager.Instance;
            if (flow == null || !flow.IsReady) return;
            if (flow.State != MatchFlowState.Playing) return;

            IsActive = false; // instant hide replication

            var ctrl = playerObj != null ? playerObj.GetComponent<NetworkPlayerController>() : null;
            if (ctrl != null)
                ctrl.RPC_ApplyConsumableToAuthority(sizeMul, speedMul, massMul);

            // master schedules respawn (not time critical)
            flow.RPC_ReportConsumablePickup(Object, playerObj, sizeMul, speedMul, massMul);
        }

        public void SetActiveState(bool active)
        {
            if (Object.HasStateAuthority)
                IsActive = active;

            _lastActive = IsActive;
            SetActiveVisual(active);
        }

        private void SetActiveVisual(bool active)
        {
            CacheComponents();

            foreach (var r in cachedRenderers) r.enabled = active;
            foreach (var c in cachedColliders) c.enabled = active;
        }
    }
}
