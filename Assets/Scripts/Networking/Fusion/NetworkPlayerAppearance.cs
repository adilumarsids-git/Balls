using Fusion;
using UnityEngine;
using Project.Core;
using Project.Wallet;

namespace Project.Networking.Fusion
{
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkPlayerAppearance : NetworkBehaviour
    {
        [SerializeField] private BallColorCatalogSO colorCatalog;
        [SerializeField] private Renderer targetRenderer;

        [Networked]
        private int ColorIndex { get; set; }

        private int lastAppliedColorIndex = -1;

        public override void Spawned()
        {
            if (targetRenderer == null)
                targetRenderer = GetComponentInChildren<Renderer>();

            if (Object.HasStateAuthority)
                ColorIndex = WalletProfile.SelectedColorIndex;

            ApplyColor(ColorIndex);
        }

        private void ApplyColor(int index)
        {
            if (targetRenderer == null || colorCatalog == null)
                return;

            var colors = colorCatalog.Colors;
            if (index < 0 || index >= colors.Count)
                index = 0;

            var entry = colors[index];
            if (entry == null)
                return;

            if (targetRenderer.material != null)
                targetRenderer.material.color = entry.color;

            lastAppliedColorIndex = index;
        }

        public override void Render()
        {
            if (lastAppliedColorIndex != ColorIndex)
                ApplyColor(ColorIndex);
        }
    }
}
