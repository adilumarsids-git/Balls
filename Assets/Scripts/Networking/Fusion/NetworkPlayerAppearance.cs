using Fusion;
using UnityEngine;
using Project.Wallet;

namespace Project.Networking.Fusion
{
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkPlayerAppearance : NetworkBehaviour
    {
        [SerializeField] private BallColorCatalogSO colorCatalog;
        [SerializeField] private Renderer targetRenderer;

        [Networked]
        private NetworkString<_64> SelectedNftId { get; set; }

        private string lastAppliedNftId;

        public override void Spawned()
        {
            if (targetRenderer == null)
                targetRenderer = GetComponentInChildren<Renderer>();

            if (Object.HasStateAuthority)
            {
                var session = WalletSession.Instance ?? WalletSession.FindOrCreate();
                var skinId = session != null ? session.SelectedNft?.SkinId : null;
                SelectedNftId = string.IsNullOrWhiteSpace(skinId) ? string.Empty : skinId;
            }

            ApplyAppearance(SelectedNftId.ToString());
        }

        private void ApplyAppearance(string skinId)
        {
            if (targetRenderer == null || colorCatalog == null)
                return;

            var colors = colorCatalog.Colors;
            int index = 0;
            if (!string.IsNullOrWhiteSpace(skinId))
                colorCatalog.TryGetIndexById(skinId, out index);

            if (index < 0 || index >= colors.Count)
                index = 0;

            var entry = colors[index];
            if (entry == null)
                return;

            if (targetRenderer.material != null)
                targetRenderer.material.color = entry.color;

            lastAppliedNftId = skinId;
        }

        public override void Render()
        {
            var currentId = SelectedNftId.ToString();
            if (!string.Equals(lastAppliedNftId, currentId, System.StringComparison.Ordinal))
                ApplyAppearance(currentId);
        }
    }
}
