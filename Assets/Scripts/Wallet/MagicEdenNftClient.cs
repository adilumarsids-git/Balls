using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Project.Utils;

namespace Project.Wallet
{
    public class MagicEdenNftClient : MonoBehaviour
    {
        private const string ApiBase = "https://api-mainnet.magiceden.dev/v2";

        public IEnumerator FetchWalletTokens(string walletAddress, System.Action<IList> onComplete, System.Action<string> onError)
        {
            if (string.IsNullOrWhiteSpace(walletAddress))
            {
                onError?.Invoke("Wallet address is empty.");
                yield break;
            }

            string url = $"{ApiBase}/wallets/{walletAddress}/tokens?offset=0&limit=500";
            using (var request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(request.error);
                    yield break;
                }

                var data = MiniJson.Deserialize(request.downloadHandler.text) as IList;
                if (data == null)
                {
                    onError?.Invoke("Unexpected response from Magic Eden.");
                    yield break;
                }

                onComplete?.Invoke(data);
            }
        }

        public static IEnumerable<string> ExtractNftNames(IList tokenList, string collectionId)
        {
            if (tokenList == null) yield break;

            foreach (var item in tokenList)
            {
                if (item is not Dictionary<string, object> token)
                    continue;

                if (!IsCollectionMatch(token, collectionId))
                    continue;

                if (token.TryGetValue("name", out var nameObj) && nameObj is string name)
                    yield return name;
            }
        }

        private static bool IsCollectionMatch(Dictionary<string, object> token, string collectionId)
        {
            if (string.IsNullOrWhiteSpace(collectionId))
                return true;

            if (token.TryGetValue("collection", out var collectionObj) && collectionObj is string collection)
            {
                if (collection == collectionId)
                    return true;
            }

            if (token.TryGetValue("collectionSymbol", out var symbolObj) && symbolObj is string symbol)
            {
                if (symbol == collectionId)
                    return true;
            }

            return false;
        }
    }
}
