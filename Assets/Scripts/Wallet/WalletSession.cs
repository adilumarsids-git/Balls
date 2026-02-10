using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Types;
using Solana.Unity.SDK;
using Solana.Unity.SDK.Nft;
using UnityEngine;
using Project.Core;

namespace Project.Wallet
{
    public class WalletSession : MonoBehaviour
    {
        public static WalletSession Instance { get; private set; }

        [Header("NFT Filters")]
        [SerializeField] private string collectionSymbol = "";
        [SerializeField] private List<string> allowedMintAddresses = new List<string>();

        [Header("Catalog")]
        [SerializeField] private BallColorCatalogSO colorCatalog;

        [Header("Editor Fallback")]
        [SerializeField] private string editorWalletAddress;

        private readonly List<NftInfo> ownedNfts = new List<NftInfo>();
        private TaskCompletionSource<string> connectTcs;
        private TaskCompletionSource<List<NftInfo>> fetchTcs;

        public bool IsConnected { get; private set; }
        public string WalletAddress { get; private set; }
        public IReadOnlyList<NftInfo> OwnedNfts => ownedNfts;
        public NftInfo SelectedNft { get; private set; }

        public static WalletSession FindOrCreate()
        {
            if (Instance != null)
                return Instance;

            var existing = FindObjectOfType<WalletSession>();
            if (existing != null)
                return existing;

            var go = new GameObject(nameof(WalletSession));
            return go.AddComponent<WalletSession>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (colorCatalog == null)
            {
                var catalogs = Resources.FindObjectsOfTypeAll<BallColorCatalogSO>();
                if (catalogs.Length > 0)
                    colorCatalog = catalogs[0];
            }
        }

        public async Task ConnectAsync()
        {
            if (IsConnected)
                return;

            if (connectTcs != null)
            {
                await connectTcs.Task;
                return;
            }

            connectTcs = new TaskCompletionSource<string>();

            try
            {
                if (Web3.Instance == null)
                    throw new System.InvalidOperationException("Web3 instance not available. Ensure WalletController exists.");

                if (Application.isEditor && !string.IsNullOrWhiteSpace(editorWalletAddress))
                {
                    OnWalletConnected(editorWalletAddress);
                }
                else
                {
                    var account = await Web3.Instance.LoginWalletAdapter();
                    if (account == null)
                        throw new System.Exception("Wallet connection failed.");

                    OnWalletConnected(account.PublicKey.ToString());
                }
            }
            catch (System.Exception ex)
            {
                OnWalletError(ex.Message);
            }

            await connectTcs.Task;
        }

        public async Task<List<NftInfo>> FetchOwnedNftsAsync()
        {
            if (!IsConnected || string.IsNullOrWhiteSpace(WalletAddress))
                return new List<NftInfo>();

            if (ownedNfts.Count > 0)
                return new List<NftInfo>(ownedNfts);

            if (fetchTcs != null)
                return new List<NftInfo>(await fetchTcs.Task);

            fetchTcs = new TaskCompletionSource<List<NftInfo>>();

            try
            {
                var nfts = await LoadOwnedNftsAsync();
                ownedNfts.Clear();
                ownedNfts.AddRange(nfts);

                if (ownedNfts.Count > 0 && SelectedNft == null)
                    SelectedNft = ownedNfts[0];

                fetchTcs.TrySetResult(new List<NftInfo>(ownedNfts));
                return new List<NftInfo>(ownedNfts);
            }
            catch (System.Exception ex)
            {
                fetchTcs.TrySetException(ex);
                throw;
            }
            finally
            {
                fetchTcs = null;
            }
        }

        public void SelectNft(string mintOrId)
        {
            if (string.IsNullOrWhiteSpace(mintOrId))
                return;

            var match = ownedNfts.Find(nft =>
                string.Equals(nft.Mint, mintOrId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nft.SkinId, mintOrId, StringComparison.OrdinalIgnoreCase));

            if (match != null)
                SelectedNft = match;
        }

        public bool EnsureSelectedNft()
        {
            if (SelectedNft != null)
                return true;

            if (ownedNfts.Count == 0)
                return false;

            SelectedNft = ownedNfts[0];
            return true;
        }

        public int ResolveColorIndex(string skinId)
        {
            if (colorCatalog == null)
                return 0;

            if (string.IsNullOrWhiteSpace(skinId))
                return 0;

            return colorCatalog.TryGetIndexById(skinId, out int index) ? index : 0;
        }

        public void OnWalletConnected(string address)
        {
            IsConnected = !string.IsNullOrWhiteSpace(address);
            WalletAddress = address ?? string.Empty;
            WalletProfile.WalletAddress = WalletAddress;

            connectTcs?.TrySetResult(WalletAddress);
            connectTcs = null;
        }

        public void OnWalletError(string message)
        {
            IsConnected = false;
            connectTcs?.TrySetException(new Exception(message));
            connectTcs = null;
        }

        private async Task<List<NftInfo>> LoadOwnedNftsAsync()
        {
            if (Web3.Wallet == null)
                throw new System.InvalidOperationException("Wallet not initialized.");

            var tokenAccounts = await Web3.Wallet.GetTokenAccounts(Commitment.Processed);
            var results = new List<NftInfo>();

            if (tokenAccounts == null || tokenAccounts.Length == 0)
                return results;

            foreach (var tokenAccount in tokenAccounts)
            {
                if (!HasTokenBalance(tokenAccount))
                    continue;

                var mint = tokenAccount.Account.Data.Parsed.Info.Mint;
                if (!IsMintAllowed(mint))
                    continue;

                var nft = await Nft.TryGetNftData(mint, Web3.Instance.WalletBase.ActiveRpcClient, commitment: Commitment.Processed)
                    .AsUniTask();

                if (nft == null)
                    continue;

                var name = nft.metaplexData?.data?.offchainData?.name;
                var symbol = nft.metaplexData?.data?.offchainData?.symbol;
                var imageUrl = nft.metaplexData?.data?.offchainData?.default_image;

                if (!IsCollectionMatch(symbol))
                    continue;

                if (colorCatalog == null || !colorCatalog.TryGetSkinIdByName(name, out var skinId))
                    continue;

                results.Add(new NftInfo
                {
                    Name = name,
                    Mint = mint,
                    SkinId = skinId,
                    ImageUrl = imageUrl
                });
            }

            return results;
        }

        private bool HasTokenBalance(TokenAccount tokenAccount)
        {
            var amount = tokenAccount?.Account?.Data?.Parsed?.Info?.TokenAmount?.AmountUlong ?? 0;
            return amount > 0;
        }

        private bool IsMintAllowed(string mint)
        {
            if (string.IsNullOrWhiteSpace(mint))
                return false;

            if (allowedMintAddresses == null || allowedMintAddresses.Count == 0)
                return true;

            return allowedMintAddresses.Contains(mint);
        }

        private bool IsCollectionMatch(string symbol)
        {
            if (string.IsNullOrWhiteSpace(collectionSymbol))
                return true;

            return string.Equals(symbol, collectionSymbol, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
