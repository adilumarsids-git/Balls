using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Wallet
{
    [CreateAssetMenu(menuName = "Project/Config/Ball Color Catalog", fileName = "BallColorCatalog")]
    public class BallColorCatalogSO : ScriptableObject
    {
        [Serializable]
        public class ColorEntry
        {
            public string id;
            public string displayName;
            public Color color = Color.white;
            public string[] nftKeywords;
        }

        [SerializeField] private List<ColorEntry> colors = new List<ColorEntry>();

        public IReadOnlyList<ColorEntry> Colors => colors;

        public bool TryGetIndexByKeyword(string nftName, out int index)
        {
            index = -1;
            if (string.IsNullOrWhiteSpace(nftName)) return false;

            for (int i = 0; i < colors.Count; i++)
            {
                var entry = colors[i];
                if (entry == null || entry.nftKeywords == null) continue;

                foreach (var keyword in entry.nftKeywords)
                {
                    if (string.IsNullOrWhiteSpace(keyword)) continue;
                    if (nftName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        index = i;
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
