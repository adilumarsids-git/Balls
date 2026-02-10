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
        [SerializeField] private WalletCollectionConfigSO collectionConfig;
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

            if (collectionConfig == null)
            {
                var configs = Resources.FindObjectsOfTypeAll<WalletCollectionConfigSO>();
                if (configs.Length > 0)
                    collectionConfig = configs[0];
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
                    var publicKey = await LoginWalletAdapterAsync();
                    if (string.IsNullOrWhiteSpace(publicKey))
                        throw new System.Exception("Wallet connection failed.");

                    OnWalletConnected(publicKey);
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
                return EnsureDefaultIfNeeded(results);

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

                var collectionKey = ResolveCollectionKey(nft);
                var collectionName = ResolveCollectionName(nft);
                if (!IsCollectionMatch(symbol, collectionKey, collectionName))
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

            return EnsureDefaultIfNeeded(results);
        }

        private async Task<string> LoginWalletAdapterAsync()
        {
            if (Web3.Wallet?.Account?.PublicKey != null)
                return Web3.Wallet.Account.PublicKey.ToString();

            var tcs = new TaskCompletionSource<string>();

            void HandleWalletChange()
            {
                if (Web3.Wallet?.Account?.PublicKey != null)
                    tcs.TrySetResult(Web3.Wallet.Account.PublicKey.ToString());
            }

            Web3.OnWalletChangeState += HandleWalletChange;

            try
            {
                var account = await Web3.Instance.LoginWalletAdapter();
                if (account?.PublicKey != null)
                    tcs.TrySetResult(account.PublicKey.ToString());

                return await tcs.Task;
            }
            finally
            {
                Web3.OnWalletChangeState -= HandleWalletChange;
            }
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

        private bool IsCollectionMatch(string symbol, string collectionKey, string collectionName)
        {
            if (collectionConfig == null)
                return true;

            return collectionConfig.Matches(symbol, collectionKey, collectionName);
        }

        private List<NftInfo> EnsureDefaultIfNeeded(List<NftInfo> results)
        {
            if (results.Count > 0)
                return results;

            if (collectionConfig != null && !collectionConfig.AllowDefaultSkin)
                return results;

            var defaultNft = CreateDefaultNft();
            if (defaultNft != null)
                results.Add(defaultNft);

            return results;
        }

        private NftInfo CreateDefaultNft()
        {
            if (colorCatalog == null || colorCatalog.Colors.Count == 0)
                return null;

            var entry = colorCatalog.Colors[0];
            var skinId = string.IsNullOrWhiteSpace(entry.id) ? "0" : entry.id;
            var name = string.IsNullOrWhiteSpace(entry.displayName) ? "Default Ball" : entry.displayName;

            return new NftInfo
            {
                Name = name,
                Mint = string.Empty,
                SkinId = skinId,
                ImageUrl = null
            };
        }

        private static string ResolveCollectionKey(object nft)
        {
            var metaplexData = GetPropertyValue(nft, "metaplexData");
            var data = GetPropertyValue(metaplexData, "data");
            if (data == null)
                return null;

            var onchainData = GetPropertyValue(data, "onchainData");
            var collection = GetPropertyValue(onchainData, "collection");
            return GetPropertyValue(collection, "key")?.ToString();
        }

        private static string ResolveCollectionName(object nft)
        {
            var metaplexData = GetPropertyValue(nft, "metaplexData");
            var data = GetPropertyValue(metaplexData, "data");
            var offchain = GetPropertyValue(data, "offchainData");
            var collection = GetPropertyValue(offchain, "collection");
            var name = GetPropertyValue(collection, "name")?.ToString();
            if (!string.IsNullOrWhiteSpace(name))
                return name;

            return GetPropertyValue(collection, "family")?.ToString();
        }

        private static object GetPropertyValue(object target, string propertyName)
        {
            if (target == null || string.IsNullOrWhiteSpace(propertyName))
                return null;

            var prop = target.GetType().GetProperty(propertyName);
            return prop != null ? prop.GetValue(target) : null;
        }
    }
}
