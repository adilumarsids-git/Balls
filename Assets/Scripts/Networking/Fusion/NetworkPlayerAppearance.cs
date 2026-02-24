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
        [SerializeField] private MeshFilter targetMeshFilter;

        [Networked]
        private NetworkString<_64> SelectedNftId { get; set; }

        [Networked]
        private NetworkString<_64> SelectedNftMint { get; set; }

        private string lastAppliedNftId;
        private bool _isNetworkSpawned;

        public override void Spawned()
        {
            _isNetworkSpawned = true;
            if (targetRenderer == null)
                targetRenderer = GetComponentInChildren<Renderer>();

            if (targetMeshFilter == null)
                targetMeshFilter = GetComponentInChildren<MeshFilter>();

            if (Object.HasStateAuthority)
            {
                var session = WalletSession.Instance ?? WalletSession.FindOrCreate();
                var skinId = session != null ? session.SelectedNft?.SkinId : null;
                var mint = session != null ? session.SelectedNft?.Mint : null;
                SelectedNftId = string.IsNullOrWhiteSpace(skinId) ? string.Empty : skinId;
                SelectedNftMint = string.IsNullOrWhiteSpace(mint) ? string.Empty : mint;
            }

            ApplyAppearance(SelectedNftId.ToString());
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            _isNetworkSpawned = false;
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

            if (targetMeshFilter != null && entry.mesh != null)
                targetMeshFilter.sharedMesh = entry.mesh;

            lastAppliedNftId = skinId;
        }


        public string GetLeaderboardNftKey()
        {
            string mint;
            if (TryGetSelectedNftMint(out mint) && !string.IsNullOrWhiteSpace(mint))
                return mint;

            string id;
            if (TryGetSelectedNftId(out id) && !string.IsNullOrWhiteSpace(id))
                return id;

            return !string.IsNullOrWhiteSpace(lastAppliedNftId) ? lastAppliedNftId : string.Empty;
        }

        public override void Render()
        {
            string currentId;
            if (!TryGetSelectedNftId(out currentId))
                return;

            if (!string.Equals(lastAppliedNftId, currentId, System.StringComparison.Ordinal))
                ApplyAppearance(currentId);
        }

        public string GetLeaderboardNftDisplayName()
        {
            // Use the catalog "Display Name" for the selected SkinId
            string id;
            if (!TryGetSelectedNftId(out id) || string.IsNullOrWhiteSpace(id))
                id = lastAppliedNftId;

            if (colorCatalog != null && !string.IsNullOrWhiteSpace(id))
            {
                if (colorCatalog.TryGetIndexById(id, out int index))
                {
                    var entry = colorCatalog.Colors[index];
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.displayName))
                        return entry.displayName;
                }
            }

            // Fallbacks
            return !string.IsNullOrWhiteSpace(id) ? id : "Unknown NFT";
        }

        public bool TryGetSelectedNftId(out string id)
        {
            id = null;
            if (!_isNetworkSpawned || Object == null)
                return false;

            id = SelectedNftId.ToString();
            return true;
        }

        public bool TryGetSelectedNftMint(out string mint)
        {
            mint = null;
            if (!_isNetworkSpawned || Object == null)
                return false;

            mint = SelectedNftMint.ToString();
            return true;
        }


    }
}
