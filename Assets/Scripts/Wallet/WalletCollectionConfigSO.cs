using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Project.Wallet
{
    [CreateAssetMenu(menuName = "Project/Config/Wallet Collection Config", fileName = "WalletCollectionConfig")]
    public class WalletCollectionConfigSO : ScriptableObject
    {
        [SerializeField] private bool allowDefaultSkin = true;
        [SerializeField] private List<string> collectionIds = new List<string>();
        [SerializeField] private List<string> collectionSymbols = new List<string>();
        [SerializeField] private bool allowNameKeywordFallbackWhenCollectionMissing = true;
        [SerializeField] private bool allowCatalogFallbackWhenCollectionMismatch = true;

        public bool AllowDefaultSkin => allowDefaultSkin;
        public IReadOnlyList<string> CollectionIds => collectionIds;
        public IReadOnlyList<string> CollectionSymbols => collectionSymbols;
        public bool AllowNameKeywordFallbackWhenCollectionMissing => allowNameKeywordFallbackWhenCollectionMissing;
        public bool AllowCatalogFallbackWhenCollectionMismatch => allowCatalogFallbackWhenCollectionMismatch;

        public bool Matches(string symbol, string collectionKey, string collectionName, bool hasCatalogKeywordMatch)
        {
            var hasConfiguredFilters = HasConfiguredFilters();
            if (!hasConfiguredFilters)
                return true;

            if (collectionSymbols != null)
            {
                foreach (var entry in collectionSymbols)
                {
                    if (string.IsNullOrWhiteSpace(entry)) continue;
                    if (IsTextMatch(symbol, entry))
                        return true;
                    if (IsTextMatch(collectionName, entry))
                        return true;
                }
            }

            if (collectionIds != null)
            {
                foreach (var entry in collectionIds)
                {
                    if (string.IsNullOrWhiteSpace(entry)) continue;
                    if (IsTextMatch(collectionKey, entry))
                        return true;
                }
            }

            var hasCollectionMetadata = !string.IsNullOrWhiteSpace(symbol)
                                        || !string.IsNullOrWhiteSpace(collectionKey)
                                        || !string.IsNullOrWhiteSpace(collectionName);

            if (!hasCollectionMetadata && allowNameKeywordFallbackWhenCollectionMissing && hasCatalogKeywordMatch)
                return true;

            if (hasCollectionMetadata && allowCatalogFallbackWhenCollectionMismatch && hasCatalogKeywordMatch)
                return true;

            return false;
        }

        private bool HasConfiguredFilters()
        {
            var symbolCount = collectionSymbols != null ? collectionSymbols.Count : 0;
            var idCount = collectionIds != null ? collectionIds.Count : 0;
            return symbolCount > 0 || idCount > 0;
        }

        private static bool IsTextMatch(string actual, string expected)
        {
            if (string.IsNullOrWhiteSpace(actual) || string.IsNullOrWhiteSpace(expected))
                return false;

            if (string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                return true;

            var normalizedActual = Normalize(actual);
            var normalizedExpected = Normalize(expected);
            if (string.IsNullOrWhiteSpace(normalizedActual) || string.IsNullOrWhiteSpace(normalizedExpected))
                return false;

            return normalizedActual.Contains(normalizedExpected, StringComparison.OrdinalIgnoreCase)
                   || normalizedExpected.Contains(normalizedActual, StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var buffer = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                if (char.IsLetterOrDigit(c))
                    buffer.Append(char.ToLowerInvariant(c));
            }

            return buffer.Length == 0 ? string.Empty : buffer.ToString();
        }
    }
}
