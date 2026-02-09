using System.Collections.Generic;
using UnityEngine;

namespace Project.Core
{
    public static class WalletProfile
    {
        private const string KeyWalletAddress = "wallet_address";
        private const string KeySelectedColor = "wallet_color_index";

        private static readonly List<int> ownedColorIndices = new List<int>();

        public static string WalletAddress
        {
            get => PlayerPrefs.GetString(KeyWalletAddress, "");
            set
            {
                PlayerPrefs.SetString(KeyWalletAddress, value ?? "");
                PlayerPrefs.Save();
            }
        }

        public static int SelectedColorIndex
        {
            get => PlayerPrefs.GetInt(KeySelectedColor, 0);
            set
            {
                PlayerPrefs.SetInt(KeySelectedColor, value);
                PlayerPrefs.Save();
            }
        }

        public static IReadOnlyList<int> OwnedColorIndices => ownedColorIndices;

        public static void SetOwnedColors(IEnumerable<int> indices)
        {
            ownedColorIndices.Clear();
            if (indices == null) return;

            foreach (var index in indices)
                ownedColorIndices.Add(index);

            ownedColorIndices.Sort();
        }
    }
}
