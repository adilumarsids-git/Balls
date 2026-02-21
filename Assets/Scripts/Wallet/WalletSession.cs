using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Project.Utils;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Types;
using Solana.Unity.SDK;
using Solana.Unity.SDK.Nft;
using UnityEngine;
using UnityEngine.Networking;
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

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        [Header("RPC Network")]
        [SerializeField] private bool autoSwitchDevnetToMainnet = true;
        [SerializeField] private string mainnetRpcUrl = "https://api.mainnet-beta.solana.com";

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
                {
                    colorCatalog = SelectBestCatalog(catalogs);
                    Debug.Log($"[WalletSession] Auto-selected BallColorCatalog: '{colorCatalog.name}' (from {catalogs.Length} catalog asset(s)).");
                }
            }

            if (collectionConfig == null)
            {
                var configs = Resources.FindObjectsOfTypeAll<WalletCollectionConfigSO>();
                if (configs.Length > 0)
                    collectionConfig = configs[0];
            }
        }

        private static BallColorCatalogSO SelectBestCatalog(BallColorCatalogSO[] catalogs)
        {
            if (catalogs == null || catalogs.Length == 0)
                return null;

            BallColorCatalogSO best = catalogs[0];
            var bestScore = ScoreCatalog(best);

            for (int i = 1; i < catalogs.Length; i++)
            {
                var candidate = catalogs[i];
                var score = ScoreCatalog(candidate);
                if (score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            return best;
        }

        private static int ScoreCatalog(BallColorCatalogSO catalog)
        {
            if (catalog == null || catalog.Colors == null)
                return -1;

            var score = catalog.Colors.Count * 10;
            foreach (var entry in catalog.Colors)
            {
                if (entry == null)
                    continue;

                var id = entry.id ?? string.Empty;
                var name = entry.displayName ?? string.Empty;
                if (id.Equals("red", StringComparison.OrdinalIgnoreCase) || name.IndexOf("red", StringComparison.OrdinalIgnoreCase) >= 0)
                    score += 2;
                if (id.Equals("blue", StringComparison.OrdinalIgnoreCase) || name.IndexOf("blue", StringComparison.OrdinalIgnoreCase) >= 0)
                    score += 2;
                if (id.Equals("green", StringComparison.OrdinalIgnoreCase) || name.IndexOf("green", StringComparison.OrdinalIgnoreCase) >= 0)
                    score += 2;
            }

            return score;
        }

        public async Task ConnectAsync()
        {
            if (IsConnected)
            {
                LogDebug($"ConnectAsync skipped (already connected): {WalletAddress}");
                return;
            }

            var pendingConnect = connectTcs;
            if (pendingConnect != null)
            {
                await pendingConnect.Task;
                return;
            }

            connectTcs = new TaskCompletionSource<string>();
            pendingConnect = connectTcs;

            try
            {
                if (Web3.Instance == null)
                    throw new System.InvalidOperationException("Web3 instance not available. Ensure WalletController exists.");

                LogDebug("ConnectAsync started.");
                EnsureRpcNetworkForNfts();

                if (Application.isEditor && !string.IsNullOrWhiteSpace(editorWalletAddress))
                {
                    LogDebug($"Using editor fallback wallet address: {editorWalletAddress}");
                    OnWalletConnected(editorWalletAddress);
                }
                else
                {
                    LogDebug("Opening wallet adapter login...");
                    var publicKey = await LoginWalletAdapterAsync();
                    if (string.IsNullOrWhiteSpace(publicKey))
                        throw new System.Exception("Wallet connection failed.");

                    LogDebug($"Wallet adapter returned public key: {publicKey}");
                    OnWalletConnected(publicKey);
                }
            }
            catch (System.Exception ex)
            {
                LogError($"ConnectAsync failed: {ex}");
                OnWalletError(ex.Message);
            }

            try
            {
                await pendingConnect.Task;
            }
            finally
            {
                if (ReferenceEquals(connectTcs, pendingConnect))
                    connectTcs = null;
            }
        }

        public async Task<List<NftInfo>> FetchOwnedNftsAsync()
        {
            if (!IsConnected || string.IsNullOrWhiteSpace(WalletAddress))
            {
                LogWarning("FetchOwnedNftsAsync skipped because wallet is not connected.");
                return new List<NftInfo>();
            }

            if (ownedNfts.Count > 0)
            {
                LogDebug($"Returning cached NFTs: {ownedNfts.Count}");
                return new List<NftInfo>(ownedNfts);
            }

            if (fetchTcs != null)
                return new List<NftInfo>(await fetchTcs.Task);

            fetchTcs = new TaskCompletionSource<List<NftInfo>>();

            try
            {
                LogDebug($"Fetching owned NFTs for wallet: {WalletAddress}");
                var nfts = await LoadOwnedNftsAsync();
                ownedNfts.Clear();
                ownedNfts.AddRange(nfts);

                if (ownedNfts.Count > 0 && SelectedNft == null)
                    SelectedNft = ownedNfts[0];

                fetchTcs.TrySetResult(new List<NftInfo>(ownedNfts));
                LogDebug($"FetchOwnedNftsAsync complete. Final count: {ownedNfts.Count}");
                return new List<NftInfo>(ownedNfts);
            }
            catch (System.Exception ex)
            {
                LogError($"FetchOwnedNftsAsync failed: {ex}");
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

            LogDebug($"Wallet connected. Address: {WalletAddress}");

            connectTcs?.TrySetResult(WalletAddress);
        }

        public void OnWalletError(string message)
        {
            IsConnected = false;
            LogError($"Wallet error: {message}");
            connectTcs?.TrySetException(new Exception(message));
        }

        private async Task<List<NftInfo>> LoadOwnedNftsAsync()
        {
            if (Web3.Wallet == null)
                throw new System.InvalidOperationException("Wallet not initialized.");

            EnsureRpcNetworkForNfts();
            var endpointBeforeQuery = GetCurrentRpcEndpoint();
            var tokenAccounts = await Web3.Wallet.GetTokenAccounts(Commitment.Processed);
            var results = new List<NftInfo>();

            LogDebug($"Token account query returned: {(tokenAccounts == null ? 0 : tokenAccounts.Length)} account(s). endpoint='{endpointBeforeQuery}'");

            if ((tokenAccounts == null || tokenAccounts.Length == 0)
                && endpointBeforeQuery.Contains("devnet", StringComparison.OrdinalIgnoreCase)
                && autoSwitchDevnetToMainnet)
            {
                LogWarning("Token accounts are empty on Devnet endpoint. Retrying after mainnet switch attempt...");
                if (TrySwitchRpcToMainnet())
                {
                    var endpointAfterSwitch = GetCurrentRpcEndpoint();
                    tokenAccounts = await Web3.Wallet.GetTokenAccounts(Commitment.Processed);
                    LogDebug($"Retry token account query returned: {(tokenAccounts == null ? 0 : tokenAccounts.Length)} account(s). endpoint='{endpointAfterSwitch}'");
                }
            }

            if (tokenAccounts == null || tokenAccounts.Length == 0)
            {
                LogWarning("Token accounts empty. Trying DAS collectibles fallback...");
                try
                {
                    var dasNfts = await TryLoadCollectiblesViaDasAsync();
                    if (dasNfts.Count > 0)
                    {
                        results.AddRange(dasNfts);
                        LogDebug($"Added {dasNfts.Count} NFT(s) from DAS collectibles fallback API (tokenAccounts empty).");
                    }
                }
                catch (Exception ex)
                {
                    LogWarning($"DAS collectible fallback failed safely (tokenAccounts empty): {ex.Message}");
                }

                LogDebug($"LoadOwnedNftsAsync finished (tokenAccounts empty). Accepted NFTs before default fallback: {results.Count}");
                return EnsureDefaultIfNeeded(results);
            }


            foreach (var tokenAccount in tokenAccounts)
            {
                if (!HasTokenBalance(tokenAccount))
                {
                    LogDebug("Skipping token account with zero balance.");
                    continue;
                }

                var mint = tokenAccount.Account.Data.Parsed.Info.Mint;
                if (!IsMintAllowed(mint))
                {
                    LogDebug($"Skipping mint not in allow-list: {mint}");
                    continue;
                }

                var nft = await Nft.TryGetNftData(mint, Web3.Instance.WalletBase.ActiveRpcClient, commitment: Commitment.Processed)
                    .AsUniTask();

                if (nft == null)
                {
                    LogWarning($"NFT metadata fetch returned null for mint: {mint}");
                    continue;
                }

                var name = ResolveNftName(nft, mint);
                var symbol = ResolveNftSymbol(nft);
                var imageUrl = ResolveNftImageUrl(nft);

                var collectionKey = ResolveCollectionKey(nft);
                var collectionName = ResolveCollectionName(nft);
                var hasCatalogKeywordMatch = TryResolveSkinId(name, out var skinId);
                if (!IsCollectionMatch(symbol, collectionKey, collectionName, hasCatalogKeywordMatch))
                {
                    LogDebug($"Filtered mint {mint}: collection mismatch. name='{name}', symbol='{symbol}', collectionKey='{collectionKey}', collectionName='{collectionName}', catalogMatch={hasCatalogKeywordMatch}");
                    continue;
                }

                if (!hasCatalogKeywordMatch)
                {
                    LogDebug($"Filtered mint {mint}: NFT name '{name}' did not match any BallColorCatalog keyword.");
                    continue;
                }

                results.Add(new NftInfo
                {
                    Name = name,
                    Mint = mint,
                    SkinId = skinId,
                    ImageUrl = imageUrl
                });

                LogDebug($"Accepted NFT mint {mint}: name='{name}', skinId='{skinId}', symbol='{symbol}', collectionKey='{collectionKey}', collectionName='{collectionName}'");
            }

            if (results.Count == 0)
            {
                try
                {
                    var reflectionNfts = await TryLoadNftsViaWalletReflectionAsync();
                    if (reflectionNfts.Count > 0)
                    {
                        results.AddRange(reflectionNfts);
                        LogDebug($"Added {reflectionNfts.Count} NFT(s) from wallet reflection fallback API.");
                    }
                }
                catch (Exception ex)
                {
                    LogWarning($"Reflection collectible fallback failed safely: {ex.Message}");
                }
            }

            if (results.Count == 0)
            {
                try
                {
                    var dasNfts = await TryLoadCollectiblesViaDasAsync();
                    if (dasNfts.Count > 0)
                    {
                        results.AddRange(dasNfts);
                        LogDebug($"Added {dasNfts.Count} NFT(s) from DAS collectibles fallback API.");
                    }
                }
                catch (Exception ex)
                {
                    LogWarning($"DAS collectible fallback failed safely: {ex.Message}");
                }
            }

            LogDebug($"LoadOwnedNftsAsync finished. Accepted NFTs before default fallback: {results.Count}");
            return EnsureDefaultIfNeeded(results);
        }

        private async Task<List<NftInfo>> TryLoadNftsViaWalletReflectionAsync()
        {
            var output = new List<NftInfo>();
            var wallet = Web3.Wallet;
            if (wallet == null)
                return output;

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var methods = wallet.GetType().GetMethods(flags)
                .Where(m =>
                    m.Name.Contains("nft", StringComparison.OrdinalIgnoreCase) ||
                    m.Name.Contains("collectible", StringComparison.OrdinalIgnoreCase) ||
                    m.Name.Contains("asset", StringComparison.OrdinalIgnoreCase))
                .OrderBy(m => m.Name)
                .ToArray();

            foreach (var method in methods)
            {
                if (!TryBuildInvocationArgs(method, out var args))
                {
                    LogDebug($"Skipping wallet reflection method '{method.Name}' due to unsupported signature.");
                    continue;
                }

                try
                {
                    object invocation = method.Invoke(wallet, args);

                    var nftObjects = await AwaitToEnumerableAsync(invocation);
                    if (nftObjects == null)
                        continue;

                    foreach (var nftObject in nftObjects)
                        TryConvertCandidateNft(nftObject, output);

                    LogDebug($"Wallet reflection method '{method.Name}' returned {output.Count} accepted NFT(s).");
                    if (output.Count > 0)
                        return output;
                }
                catch (Exception ex)
                {
                    LogWarning($"Wallet reflection method '{method.Name}' failed: {ex.Message}");
                }
            }

            return output;
        }

        private bool TryBuildInvocationArgs(MethodInfo method, out object[] args)
        {
            var parameters = method.GetParameters();
            if (parameters.Length == 0)
            {
                args = null;
                return true;
            }

            args = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                var t = p.ParameterType;

                if (t == typeof(string))
                {
                    args[i] = WalletAddress;
                    continue;
                }

                if (t == typeof(int))
                {
                    var name = p.Name ?? string.Empty;
                    if (name.Contains("page", StringComparison.OrdinalIgnoreCase)) args[i] = 1;
                    else if (name.Contains("limit", StringComparison.OrdinalIgnoreCase)) args[i] = 100;
                    else args[i] = 0;
                    continue;
                }

                if (t == typeof(bool))
                {
                    args[i] = true;
                    continue;
                }

                if (t == typeof(Commitment))
                {
                    args[i] = Commitment.Processed;
                    continue;
                }

                if (t.IsEnum)
                {
                    try
                    {
                        args[i] = Enum.Parse(t, "Processed", true);
                    }
                    catch
                    {
                        args[i] = Enum.GetValues(t).GetValue(0);
                    }
                    continue;
                }

                if (p.HasDefaultValue)
                {
                    args[i] = p.DefaultValue;
                    continue;
                }

                return false;
            }

            return true;
        }

        private async Task<IEnumerable<object>> AwaitToEnumerableAsync(object invocation)
        {
            if (invocation == null)
                return null;

            if (invocation is Task task)
            {
                await task;
                var resultProp = task.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
                var resultValue = resultProp?.GetValue(task);
                return ToObjectEnumerable(resultValue);
            }

            var asTaskMethod = invocation.GetType().GetMethod("AsTask", BindingFlags.Public | BindingFlags.Instance);
            if (asTaskMethod != null)
            {
                var convertedTask = asTaskMethod.Invoke(invocation, null) as Task;
                if (convertedTask != null)
                {
                    await convertedTask;
                    var resultProp = convertedTask.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
                    var resultValue = resultProp?.GetValue(convertedTask);
                    return ToObjectEnumerable(resultValue);
                }
            }

            return ToObjectEnumerable(invocation);
        }

        private IEnumerable<object> ToObjectEnumerable(object value)
        {
            if (value == null)
                return null;

            if (value is IEnumerable<object> objects)
                return objects;

            if (value is System.Collections.IEnumerable enumerable)
            {
                var list = new List<object>();
                foreach (var item in enumerable)
                    list.Add(item);
                return list;
            }

            return new[] { value };
        }

        private void TryConvertCandidateNft(object nftObject, List<NftInfo> output)
        {
            if (nftObject == null)
                return;

            var mint = ResolveNftMint(nftObject);
            var name = ResolveNftName(nftObject, mint);
            var symbol = ResolveNftSymbol(nftObject);
            var imageUrl = ResolveNftImageUrl(nftObject);
            var collectionKey = ResolveCollectionKey(nftObject);
            var collectionName = ResolveCollectionName(nftObject);

            var hasCatalogKeywordMatch = TryResolveSkinId(name, out var skinId);
            if (!IsCollectionMatch(symbol, collectionKey, collectionName, hasCatalogKeywordMatch))
                return;

            if (!hasCatalogKeywordMatch)
                return;

            if (output.Exists(x => !string.IsNullOrWhiteSpace(x.Mint) && string.Equals(x.Mint, mint, StringComparison.OrdinalIgnoreCase)))
                return;

            output.Add(new NftInfo
            {
                Name = name,
                Mint = mint,
                SkinId = skinId,
                ImageUrl = imageUrl
            });
        }

        private async Task<List<NftInfo>> TryLoadCollectiblesViaDasAsync()
        {
            var output = new List<NftInfo>();

            if (string.IsNullOrWhiteSpace(WalletAddress))
                return output;

            var endpoint = GetCurrentRpcEndpoint();
            if (string.IsNullOrWhiteSpace(endpoint))
                endpoint = mainnetRpcUrl;

            try
            {
                var payload = new Dictionary<string, object>
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = "wallet-session-das",
                    ["method"] = "getAssetsByOwner",
                    ["params"] = new Dictionary<string, object>
                    {
                        ["ownerAddress"] = WalletAddress,
                        ["page"] = 1,
                        ["limit"] = 100
                    }
                };

                var body = MiniJson.Serialize(payload);
                using var request = new UnityWebRequest(endpoint, "POST");
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                var op = request.SendWebRequest();
                while (!op.isDone)
                    await Task.Yield();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    LogWarning($"DAS getAssetsByOwner failed: {request.error}");
                    return output;
                }

                var root = MiniJson.Deserialize(request.downloadHandler.text) as Dictionary<string, object>;
                if (root == null)
                    return output;

                if (root.TryGetValue("error", out var errorObj) && errorObj != null)
                {
                    LogWarning($"DAS error: {errorObj}");
                    return output;
                }

                if (!root.TryGetValue("result", out var resultObj) || resultObj is not Dictionary<string, object> result)
                    return output;

                if (!result.TryGetValue("items", out var itemsObj) || itemsObj is not System.Collections.IEnumerable items)
                    return output;

                foreach (var itemObj in items)
                {
                    if (itemObj is not Dictionary<string, object> item)
                        continue;

                    var mint = ReadString(item, "id");
                var content = ReadDict(item, "content");
                var metadata = ReadDict(content, "metadata");
                var name = ReadString(metadata, "name");
                var symbol = ReadString(metadata, "symbol");
                var imageUrl = ReadString(ReadDict(content, "links"), "image");

                var grouping = ReadEnumerable(item, "grouping");
                string collectionKey = string.Empty;
                if (grouping != null)
                {
                    foreach (var groupObj in grouping)
                    {
                        if (groupObj is not Dictionary<string, object> group)
                            continue;

                        var groupKey = ReadString(group, "group_key");
                        if (string.Equals(groupKey, "collection", StringComparison.OrdinalIgnoreCase))
                        {
                            collectionKey = ReadString(group, "group_value");
                            break;
                        }
                    }
                }

                var collectionName = ReadString(ReadDict(item, "collection"), "name");

                    var hasCatalogKeywordMatch = TryResolveSkinId(name, out var skinId);
                    if (!IsCollectionMatch(symbol, collectionKey, collectionName, hasCatalogKeywordMatch))
                        continue;
                    if (!hasCatalogKeywordMatch)
                        continue;

                    output.Add(new NftInfo
                    {
                        Name = name,
                        Mint = mint,
                        SkinId = skinId,
                        ImageUrl = imageUrl
                    });
                }
            }
            catch (Exception ex)
            {
                LogWarning($"DAS collectibles parsing failed safely: {ex.Message}");
            }

            return output;
        }

        private string ResolveFallbackSkinId()
        {
            if (colorCatalog == null || colorCatalog.Colors.Count == 0)
                return "0";

            var entry = colorCatalog.Colors[0];
            return string.IsNullOrWhiteSpace(entry.id) ? "0" : entry.id;
        }

        private bool TryResolveSkinId(string nftName, out string skinId)
        {
            skinId = null;

            if (colorCatalog != null && colorCatalog.TryGetSkinIdByName(nftName, out skinId))
                return true;

            var normalized = NormalizeName(nftName);
            if (string.IsNullOrWhiteSpace(normalized))
                return false;

            if (TryResolveSkinIdByColorToken("red", "RedBall", normalized, out skinId)) return true;
            if (TryResolveSkinIdByColorToken("blue", "BlueBall", normalized, out skinId)) return true;
            if (TryResolveSkinIdByColorToken("green", "GreenBall", normalized, out skinId)) return true;
            if (TryResolveSkinIdByColorToken("yellow", "YellowBall", normalized, out skinId)) return true;
            if (TryResolveSkinIdByColorToken("purple", "PurpleBall", normalized, out skinId)) return true;

            return false;
        }

        private bool TryResolveSkinIdByColorToken(string token, string fallbackSkinId, string normalizedNftName, out string skinId)
        {
            skinId = null;

            if (!normalizedNftName.Contains(token, StringComparison.OrdinalIgnoreCase))
                return false;

            if (colorCatalog == null || colorCatalog.Colors == null)
            {
                skinId = fallbackSkinId;
                return true;
            }

            foreach (var entry in colorCatalog.Colors)
            {
                if (entry == null)
                    continue;

                var idNorm = NormalizeName(entry.id);
                var displayNorm = NormalizeName(entry.displayName);
                if (idNorm.Contains(token, StringComparison.OrdinalIgnoreCase)
                    || displayNorm.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    skinId = string.IsNullOrWhiteSpace(entry.id) ? ResolveFallbackSkinId() : entry.id;
                    return true;
                }

                if (entry.nftKeywords == null)
                    continue;

                foreach (var keyword in entry.nftKeywords)
                {
                    var keywordNorm = NormalizeName(keyword);
                    if (keywordNorm.Contains(token, StringComparison.OrdinalIgnoreCase))
                    {
                        skinId = string.IsNullOrWhiteSpace(entry.id) ? ResolveFallbackSkinId() : entry.id;
                        return true;
                    }
                }
            }

            skinId = fallbackSkinId;
            return true;
        }

        private static string NormalizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var sb = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(char.ToLowerInvariant(c));
            }

            return sb.ToString();
        }

        private static Dictionary<string, object> ReadDict(Dictionary<string, object> source, string key)
        {
            if (source == null || !source.TryGetValue(key, out var value) || value is not Dictionary<string, object> dict)
                return null;
            return dict;
        }

        private static string ReadString(Dictionary<string, object> source, string key)
        {
            if (source == null || !source.TryGetValue(key, out var value) || value == null)
                return string.Empty;
            return value.ToString() ?? string.Empty;
        }

        private static System.Collections.IEnumerable ReadEnumerable(Dictionary<string, object> source, string key)
        {
            if (source == null || !source.TryGetValue(key, out var value) || value is not System.Collections.IEnumerable list)
                return null;
            return list;
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
                // IMPORTANT: this must be invoked directly from the user's button click flow.
                // Wallet adapters (Phantom) may block popup/deeplink if login starts outside user gesture.
                var account = await Web3.Instance.LoginWalletAdapter();
                if (account?.PublicKey != null)
                    return account.PublicKey.ToString();

                const int timeoutMs = 12000;
                var timeoutTask = Task.Delay(timeoutMs);
                var completed = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completed == tcs.Task)
                    return await tcs.Task;

                // Final snapshot check after timeout in case state landed right at boundary.
                return Web3.Wallet?.Account?.PublicKey?.ToString();
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

        private bool IsCollectionMatch(string symbol, string collectionKey, string collectionName, bool hasCatalogKeywordMatch)
        {
            if (collectionConfig == null)
                return true;

            return collectionConfig.Matches(symbol, collectionKey, collectionName, hasCatalogKeywordMatch);
        }

        private List<NftInfo> EnsureDefaultIfNeeded(List<NftInfo> results)
        {
            // Default skin fallback is disabled by design.
            return results;
        }

        private void LogDebug(string message)
        {
            if (!enableDebugLogs)
                return;

            Debug.Log($"[WalletSession] {message}");
        }

        private void LogWarning(string message)
        {
            if (!enableDebugLogs)
                return;

            Debug.LogWarning($"[WalletSession] {message}");
        }

        private void LogError(string message)
        {
            Debug.LogError($"[WalletSession] {message}");
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
            var key = GetPropertyValue(collection, "key")?.ToString();
            if (!string.IsNullOrWhiteSpace(key))
                return key;

            var offchain = GetPropertyValue(data, "offchainData");
            var offchainCollection = GetPropertyValue(offchain, "collection");
            return GetPropertyValue(offchainCollection, "key")?.ToString();
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

        private static string ResolveNftMint(object nft)
        {
            var mint = GetPropertyValue(nft, "mint")?.ToString();
            if (!string.IsNullOrWhiteSpace(mint))
                return mint;

            var metaplexData = GetPropertyValue(nft, "metaplexData");
            var data = GetPropertyValue(metaplexData, "data");
            var onchainData = GetPropertyValue(data, "onchainData");
            mint = GetPropertyValue(onchainData, "mint")?.ToString();
            if (!string.IsNullOrWhiteSpace(mint))
                return mint;

            var metadataAccount = GetPropertyValue(metaplexData, "metadataAccount");
            mint = GetPropertyValue(metadataAccount, "mint")?.ToString();
            if (!string.IsNullOrWhiteSpace(mint))
                return mint;

            var pubKey = GetPropertyValue(nft, "publicKey")?.ToString();
            return pubKey ?? string.Empty;
        }

        private static string ResolveNftName(object nft, string mintFallback)
        {
            var metaplexData = GetPropertyValue(nft, "metaplexData");
            var data = GetPropertyValue(metaplexData, "data");

            var offchain = GetPropertyValue(data, "offchainData");
            var offchainName = GetPropertyValue(offchain, "name")?.ToString();
            if (!string.IsNullOrWhiteSpace(offchainName))
                return offchainName;

            var onchainData = GetPropertyValue(data, "onchainData");
            var onchainName = GetPropertyValue(onchainData, "name")?.ToString();
            if (!string.IsNullOrWhiteSpace(onchainName))
                return onchainName;

            var dataName = GetPropertyValue(data, "name")?.ToString();
            if (!string.IsNullOrWhiteSpace(dataName))
                return dataName;

            return mintFallback ?? string.Empty;
        }

        private static string ResolveNftSymbol(object nft)
        {
            var metaplexData = GetPropertyValue(nft, "metaplexData");
            var data = GetPropertyValue(metaplexData, "data");

            var offchain = GetPropertyValue(data, "offchainData");
            var offchainSymbol = GetPropertyValue(offchain, "symbol")?.ToString();
            if (!string.IsNullOrWhiteSpace(offchainSymbol))
                return offchainSymbol;

            var onchainData = GetPropertyValue(data, "onchainData");
            var onchainSymbol = GetPropertyValue(onchainData, "symbol")?.ToString();
            if (!string.IsNullOrWhiteSpace(onchainSymbol))
                return onchainSymbol;

            return GetPropertyValue(data, "symbol")?.ToString();
        }

        private static string ResolveNftImageUrl(object nft)
        {
            var metaplexData = GetPropertyValue(nft, "metaplexData");
            var data = GetPropertyValue(metaplexData, "data");

            var offchain = GetPropertyValue(data, "offchainData");
            var defaultImage = GetPropertyValue(offchain, "default_image")?.ToString();
            if (!string.IsNullOrWhiteSpace(defaultImage))
                return defaultImage;

            var image = GetPropertyValue(offchain, "image")?.ToString();
            if (!string.IsNullOrWhiteSpace(image))
                return image;

            return null;
        }

        private static object GetPropertyValue(object target, string propertyName)
        {
            if (target == null || string.IsNullOrWhiteSpace(propertyName))
                return null;

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase;
            var prop = target.GetType().GetProperty(propertyName, flags);
            if (prop != null)
            {
                try
                {
                    return prop.GetValue(target);
                }
                catch
                {
                    return null;
                }
            }

            var field = target.GetType().GetField(propertyName, flags);
            if (field != null)
            {
                try
                {
                    return field.GetValue(target);
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        private void EnsureRpcNetworkForNfts()
        {
            var endpoint = GetCurrentRpcEndpoint();
            if (string.IsNullOrWhiteSpace(endpoint))
                return;

            if (!endpoint.Contains("devnet", StringComparison.OrdinalIgnoreCase))
                return;

            LogWarning($"Current RPC endpoint appears to be Devnet: {endpoint}");

            if (!autoSwitchDevnetToMainnet)
            {
                LogWarning("autoSwitchDevnetToMainnet is disabled. Set WalletController/Web3 network to Mainnet to fetch your NFTs.");
                return;
            }

            var switched = TrySwitchRpcToMainnet();
            if (switched)
            {
                var after = GetCurrentRpcEndpoint();
                LogDebug($"Attempted RPC switch to mainnet. Current endpoint: {after}");
            }
            else
            {
                LogWarning("Unable to auto-switch RPC via SDK reflection. Please set WalletController network to Mainnet manually.");
            }
        }

        private string GetCurrentRpcEndpoint()
        {
            try
            {
                var client = Web3.Instance?.WalletBase?.ActiveRpcClient;
                if (client == null)
                    return string.Empty;

                var candidates = new[] { "NodeAddress", "RpcNode", "NodeUri", "Url" };
                foreach (var name in candidates)
                {
                    var value = GetPropertyValue(client, name)?.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }

                var methods = client.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.GetParameters().Length == 0 &&
                                (m.Name.Contains("Address", StringComparison.OrdinalIgnoreCase) ||
                                 m.Name.Contains("Endpoint", StringComparison.OrdinalIgnoreCase) ||
                                 m.Name.Contains("Uri", StringComparison.OrdinalIgnoreCase)))
                    .ToArray();

                foreach (var method in methods)
                {
                    var value = method.Invoke(client, null)?.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }
            }
            catch (Exception ex)
            {
                LogWarning($"Failed to read current RPC endpoint: {ex.Message}");
            }

            return string.Empty;
        }

        private bool TrySwitchRpcToMainnet()
        {
            try
            {
                var web3 = Web3.Instance;
                if (web3 == null)
                    return false;

                if (TryInvokeNetworkMethod(web3, "SetRpcEndpoint", mainnetRpcUrl)) return true;
                if (TryInvokeNetworkMethod(web3, "SetRpcUrl", mainnetRpcUrl)) return true;
                if (TryInvokeNetworkMethod(web3, "SetRpcClient", mainnetRpcUrl)) return true;
                if (TryInvokeNetworkMethod(web3, "SetCluster", "mainnet-beta")) return true;
                if (TryInvokeNetworkMethod(web3, "SetNetwork", "mainnet-beta")) return true;
                if (TryInvokeNetworkMethod(web3, "ChangeRpc", mainnetRpcUrl)) return true;

                var walletBase = web3.WalletBase;
                if (walletBase != null)
                {
                    if (TryInvokeNetworkMethod(walletBase, "SetRpcEndpoint", mainnetRpcUrl)) return true;
                    if (TryInvokeNetworkMethod(walletBase, "SetRpcUrl", mainnetRpcUrl)) return true;
                    if (TryInvokeNetworkMethod(walletBase, "SetRpcClient", mainnetRpcUrl)) return true;
                    if (TryInvokeNetworkMethod(walletBase, "SetCluster", "mainnet-beta")) return true;
                    if (TryInvokeNetworkMethod(walletBase, "SetNetwork", "mainnet-beta")) return true;
                    if (TryInvokeNetworkMethod(walletBase, "ChangeRpc", mainnetRpcUrl)) return true;
                }
            }
            catch (Exception ex)
            {
                LogWarning($"TrySwitchRpcToMainnet error: {ex.Message}");
            }

            return false;
        }

        private bool TryInvokeNetworkMethod(object target, string methodName, string argument)
        {
            if (target == null)
                return false;

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var methods = target.GetType().GetMethods(flags)
                .Where(m => string.Equals(m.Name, methodName, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                if (parameters.Length != 1)
                    continue;

                var parameterType = parameters[0].ParameterType;

                try
                {
                    if (parameterType == typeof(string))
                    {
                        method.Invoke(target, new object[] { argument });
                        return true;
                    }

                    if (parameterType.IsEnum)
                    {
                        object enumValue;
                        try
                        {
                            enumValue = Enum.Parse(parameterType, "MainNet", true);
                        }
                        catch
                        {
                            try
                            {
                                enumValue = Enum.Parse(parameterType, "Mainnet", true);
                            }
                            catch
                            {
                                enumValue = Enum.GetValues(parameterType).GetValue(0);
                            }
                        }

                        method.Invoke(target, new[] { enumValue });
                        return true;
                    }
                }
                catch
                {
                    // try the next overload
                }
            }

            return false;
        }
    }
}
