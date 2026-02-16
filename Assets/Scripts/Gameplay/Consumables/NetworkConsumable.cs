using Fusion;
using UnityEngine;
using Project.Networking.Fusion;
using Project.Gameplay.Player;
using Project.Data;

namespace Project.Game.Consumables
{
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkConsumable : NetworkBehaviour
    {
        [Networked] public NetworkBool IsActive { get; private set; } = true;

        [SerializeField] private ConsumableConfigSO config;

        private Renderer[] cachedRenderers;
        private Collider[] cachedColliders;

        public override void Spawned()
        {
            CacheComponents();
            SetActiveVisual(IsActive);
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

            // Only the player who owns their physics should try to report pickup
            var playerTag = other.GetComponentInParent<PlayerTag>();
            if (playerTag == null) return;

            var playerObj = playerTag.NetObj;
            if (playerObj == null) return;

            // In Host mode, only the server simulates authoritative collisions.
            if (!playerObj.HasStateAuthority) return;

            var flow = NetworkGameFlowManager.Instance;
            if (flow == null || !flow.IsReady) return;
            if (flow.State != MatchFlowState.Playing) return;

            if (config == null)
            {
                Debug.LogWarning("[Consumable] Missing ConsumableConfigSO, skipping pickup.");
                return;
            }

            float sizeMultiplier = 1f + (config.sizeAdd * config.sizeScaleFactor);
            float speedMultiplier = 1f + config.speedAdd;
            float massMultiplier = 1f + config.massAdd;

            // Report pickup to server (single source of truth)
            flow.ReportConsumablePickup(Object, playerObj, sizeMultiplier, speedMultiplier, massMultiplier);
        }

        public void SetActiveState(bool active)
        {
            if (Object.HasStateAuthority)
                IsActive = active;

            SetActiveVisual(active);
        }

        public void SetActiveVisual(bool active)
        {
            CacheComponents();
            if (cachedRenderers != null)
            {
                foreach (var r in cachedRenderers)
                    r.enabled = active;
            }

            if (cachedColliders != null)
            {
                foreach (var c in cachedColliders)
                    c.enabled = active;
            }
        }
    }
}
