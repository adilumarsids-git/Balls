using Fusion;
using UnityEngine;
using Project.Networking.Fusion;
using Project.Gameplay.Player; // your PlayerTag namespace

namespace Project.Gameplay.RingOut
{
    public class RingOutZone : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            // Find player root
            var tag = other.GetComponentInParent<PlayerTag>();
            if (tag == null) return;

            var netObj = tag.NetObj != null ? tag.NetObj : tag.GetComponent<NetworkObject>();
            if (netObj == null) return;

            // ✅ CRITICAL: Only the peer that simulates this player (StateAuthority)
            // should report the ringout, otherwise master won’t see it for other players.
            if (!netObj.HasStateAuthority) return;

            var flow = NetworkGameFlowManager.Instance;
            if (flow == null || !flow.IsReady) return;
            if (flow.State != MatchFlowState.Playing) return;


            Debug.Log($"[RingOut] Local detected fall: {netObj.name} | InputAuth={netObj.InputAuthority} | StateAuth={netObj.StateAuthority}");
            flow.RPC_ReportRingOut(netObj);
        }
    }
}
