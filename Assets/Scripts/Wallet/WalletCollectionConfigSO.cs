using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Wallet
{
    [CreateAssetMenu(menuName = "Project/Config/Wallet Collection Config", fileName = "WalletCollectionConfig")]
    public class WalletCollectionConfigSO : ScriptableObject
    {
        [SerializeField] private bool allowDefaultSkin = true;
        [SerializeField] private List<string> collectionIds = new List<string>();
        [SerializeField] private List<string> collectionSymbols = new List<string>();

        public bool AllowDefaultSkin => allowDefaultSkin;
        public IReadOnlyList<string> CollectionIds => collectionIds;
        public IReadOnlyList<string> CollectionSymbols => collectionSymbols;

        public bool Matches(string symbol, string collectionKey, string collectionName)
        {
            if (collectionSymbols != null)
            {
                foreach (var entry in collectionSymbols)
                {
                    if (string.IsNullOrWhiteSpace(entry)) continue;
                    if (!string.IsNullOrWhiteSpace(symbol) &&
                        string.Equals(symbol, entry, StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (!string.IsNullOrWhiteSpace(collectionName) &&
                        string.Equals(collectionName, entry, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            if (collectionIds != null)
            {
                foreach (var entry in collectionIds)
                {
                    if (string.IsNullOrWhiteSpace(entry)) continue;
                    if (!string.IsNullOrWhiteSpace(collectionKey) &&
                        string.Equals(collectionKey, entry, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            var symbolCount = collectionSymbols != null ? collectionSymbols.Count : 0;
            var idCount = collectionIds != null ? collectionIds.Count : 0;
            return symbolCount == 0 && idCount == 0;
        }
    }
}
