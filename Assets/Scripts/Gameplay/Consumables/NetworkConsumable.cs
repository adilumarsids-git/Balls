using Fusion;
using UnityEngine;
using Project.Networking.Fusion;
using Project.Gameplay.Player;

namespace Project.Game.Consumables
{
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkConsumable : NetworkBehaviour
    {
        [Networked] public NetworkBool IsActive { get; private set; } = true;

        // Optional: you can sync a type id later (different consumables)
        [SerializeField] private float sizeMultiplier = 1.15f;
        [SerializeField] private float speedMultiplier = 1.10f;
        [SerializeField] private float massMultiplier = 1.10f;

        private void OnTriggerEnter(Collider other)
        {
            if (!IsActive) return;

            // Only the player who owns their physics should try to report pickup
            var playerTag = other.GetComponentInParent<PlayerTag>();
            if (playerTag == null) return;

            var playerObj = playerTag.NetObj;
            if (playerObj == null) return;

            // In Shared, player is simulated on its StateAuthority peer
            if (!playerObj.HasStateAuthority) return;

            var flow = NetworkGameFlowManager.Instance;
            if (flow == null || !flow.IsReady) return;
            if (flow.State != MatchFlowState.Playing) return;

            // Report pickup to master (single source of truth)
            flow.RPC_ReportConsumablePickup(Object, playerObj, sizeMultiplier, speedMultiplier, massMultiplier);
        }

        public void SetActiveVisual(bool active)
        {
            // Local visual control
            gameObject.SetActive(active);
        }
    }
}
