using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Project.Utils;
using UnityEngine;
using UnityEngine.Networking;

namespace Project.Leaderboard
{
    public class NftLeaderboardService : MonoBehaviour
    {
        [SerializeField] private NftLeaderboardConfigSO config;

        private const string PrefViewerId = "leaderboard_viewer_id";

        private static NftLeaderboardService instance;

        public static NftLeaderboardService Instance => instance;

        public static NftLeaderboardService FindOrCreate()
        {
            if (instance != null)
                return instance;

            var existing = FindObjectOfType<NftLeaderboardService>();
            if (existing != null)
                return existing;

            var go = new GameObject(nameof(NftLeaderboardService));
            return go.AddComponent<NftLeaderboardService>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            if (config == null)
                config = Resources.Load<NftLeaderboardConfigSO>("Leaderboard/NftLeaderboardConfig");

            if (config == null)
            {
                var configs = Resources.FindObjectsOfTypeAll<NftLeaderboardConfigSO>();
                if (configs.Length > 0)
                    config = configs[0];
            }
        }

        public async Task SubmitWinAsync(string nftMintOrId, string displayName)
        {
            if (!IsConfigured() || string.IsNullOrWhiteSpace(nftMintOrId))
                return;

            string nftKey = BuildNftKey(nftMintOrId);
            string session = await LoginWithCustomIdAsync(nftKey, true);
            if (string.IsNullOrWhiteSpace(session))
                throw new Exception("PlayFab login failed for NFT leaderboard update.");

            int currentValue = await GetStatisticValueAsync(session, config.StatisticName);
            await UpdateStatisticAsync(session, config.StatisticName, currentValue + 1);

            if (!string.IsNullOrWhiteSpace(displayName))
                await UpdateDisplayNameAsync(session, displayName);
        }

        public async Task<List<NftLeaderboardEntry>> GetTopAsync(int maxEntries = -1)
        {
            var result = new List<NftLeaderboardEntry>();
            if (!IsConfigured())
                return result;

            string viewerCustomId = GetOrCreateViewerId();
            string session = await LoginWithCustomIdAsync(viewerCustomId, true);
            if (string.IsNullOrWhiteSpace(session))
                return result;

            int count = maxEntries > 0 ? maxEntries : config.MaxEntries;
            var payload = new Dictionary<string, object>
            {
                ["StatisticName"] = config.StatisticName,
                ["StartPosition"] = 0,
                ["MaxResultsCount"] = count
            };

            var response = await SendRequestAsync("/Client/GetLeaderboard", payload, session);
            if (!response.TryGetValue("data", out var dataObj) || dataObj is not Dictionary<string, object> data)
                return result;

            if (!data.TryGetValue("Leaderboard", out var leaderboardObj) || leaderboardObj is not IList board)
                return result;

            foreach (var item in board)
            {
                if (item is not Dictionary<string, object> row)
                    continue;

                result.Add(new NftLeaderboardEntry
                {
                    Position = ReadInt(row, "Position"),
                    Wins = ReadInt(row, "StatValue"),
                    DisplayName = ReadString(row, "DisplayName"),
                    NftKey = ReadString(row, "PlayFabId")
                });
            }

            return result;
        }

        private bool IsConfigured()
        {
            return config != null && !string.IsNullOrWhiteSpace(config.TitleId) && !string.IsNullOrWhiteSpace(config.StatisticName);
        }

        private string GetOrCreateViewerId()
        {
            var existing = PlayerPrefs.GetString(PrefViewerId, string.Empty);
            if (!string.IsNullOrWhiteSpace(existing))
                return existing;

            var generated = "viewer_" + Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(PrefViewerId, generated);
            PlayerPrefs.Save();
            return generated;
        }

        private async Task<string> LoginWithCustomIdAsync(string customId, bool createAccount)
        {
            var payload = new Dictionary<string, object>
            {
                ["TitleId"] = config.TitleId,
                ["CustomId"] = customId,
                ["CreateAccount"] = createAccount
            };

            var response = await SendRequestAsync("/Client/LoginWithCustomID", payload, null);
            if (!response.TryGetValue("data", out var dataObj) || dataObj is not Dictionary<string, object> data)
                return null;

            if (!data.TryGetValue("SessionTicket", out var ticketObj) || ticketObj is not string ticket)
                return null;

            return ticket;
        }

        private async Task<int> GetStatisticValueAsync(string sessionTicket, string statisticName)
        {
            var payload = new Dictionary<string, object>
            {
                ["StatisticNames"] = new[] { statisticName }
            };

            var response = await SendRequestAsync("/Client/GetPlayerStatistics", payload, sessionTicket);
            if (!response.TryGetValue("data", out var dataObj) || dataObj is not Dictionary<string, object> data)
                return 0;

            if (!data.TryGetValue("Statistics", out var statsObj) || statsObj is not IList stats)
                return 0;

            foreach (var stat in stats)
            {
                if (stat is not Dictionary<string, object> row)
                    continue;

                var name = ReadString(row, "StatisticName");
                if (!string.Equals(name, statisticName, StringComparison.OrdinalIgnoreCase))
                    continue;

                return ReadInt(row, "Value");
            }

            return 0;
        }

        private async Task UpdateStatisticAsync(string sessionTicket, string statisticName, int value)
        {
            var payload = new Dictionary<string, object>
            {
                ["Statistics"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["StatisticName"] = statisticName,
                        ["Value"] = value
                    }
                }
            };

            await SendRequestAsync("/Client/UpdatePlayerStatistics", payload, sessionTicket);
        }

        private async Task UpdateDisplayNameAsync(string sessionTicket, string displayName)
        {
            var trimmed = displayName.Trim();
            if (trimmed.Length > 25)
                trimmed = trimmed.Substring(0, 25);

            var payload = new Dictionary<string, object>
            {
                ["DisplayName"] = trimmed
            };

            await SendRequestAsync("/Client/UpdateUserTitleDisplayName", payload, sessionTicket);
        }

        private async Task<Dictionary<string, object>> SendRequestAsync(string path, Dictionary<string, object> payload, string sessionTicket)
        {
            string body = MiniJson.Serialize(payload);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
            string url = $"https://{config.TitleId}.playfabapi.com{path}";

            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrWhiteSpace(sessionTicket))
                request.SetRequestHeader("X-Authorization", sessionTicket);

            var operation = request.SendWebRequest();
            while (!operation.isDone)
                await Task.Yield();

            if (request.result != UnityWebRequest.Result.Success)
                throw new Exception($"PlayFab request failed ({path}): {request.error}");

            var root = MiniJson.Deserialize(request.downloadHandler.text) as Dictionary<string, object>;
            if (root == null)
                throw new Exception($"PlayFab response parse failed ({path}).");

            if (root.TryGetValue("errorMessage", out var errObj) && errObj is string errorMessage && !string.IsNullOrWhiteSpace(errorMessage))
                throw new Exception($"PlayFab error ({path}): {errorMessage}");

            return root;
        }

        private static string BuildNftKey(string mintOrId)
        {
            return $"nft_{mintOrId.Trim()}";
        }

        private static int ReadInt(Dictionary<string, object> row, string key)
        {
            if (!row.TryGetValue(key, out var value) || value == null)
                return 0;

            return value switch
            {
                int i => i,
                long l => (int)l,
                double d => (int)d,
                float f => (int)f,
                _ => int.TryParse(value.ToString(), out var parsed) ? parsed : 0
            };
        }

        private static string ReadString(Dictionary<string, object> row, string key)
        {
            if (!row.TryGetValue(key, out var value) || value == null)
                return string.Empty;

            return value.ToString() ?? string.Empty;
        }
    }
}
