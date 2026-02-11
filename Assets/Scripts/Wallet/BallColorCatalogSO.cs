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

        public bool TryGetSkinIdByName(string nftName, out string skinId)
        {
            skinId = null;
            if (!TryGetIndexByKeyword(nftName, out int index))
                return false;

            if (index < 0 || index >= colors.Count || colors[index] == null)
                return false;

            skinId = string.IsNullOrWhiteSpace(colors[index].id)
                ? index.ToString()
                : colors[index].id;

            return true;
        }

        public bool TryGetIndexById(string id, out int index)
        {
            index = -1;
            if (string.IsNullOrWhiteSpace(id))
                return false;

            for (int i = 0; i < colors.Count; i++)
            {
                var entry = colors[i];
                if (entry == null) continue;

                if (string.Equals(entry.id, id, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    return true;
                }

                if (string.Equals(entry.displayName, id, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    return true;
                }

                if (string.Equals(i.ToString(), id, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    return true;
                }
            }

            return false;
        }
    }
}
