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

        [Networked(OnChanged = nameof(OnColorChanged))]
        private int ColorIndex { get; set; }

        public override void Spawned()
        {
            if (targetRenderer == null)
                targetRenderer = GetComponentInChildren<Renderer>();

            if (Object.HasStateAuthority)
                ColorIndex = WalletProfile.SelectedColorIndex;

            ApplyColor(ColorIndex);
        }

        private static void OnColorChanged(Changed<NetworkPlayerAppearance> changed)
        {
            changed.Behaviour.ApplyColor(changed.Behaviour.ColorIndex);
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
        }
    }
}
