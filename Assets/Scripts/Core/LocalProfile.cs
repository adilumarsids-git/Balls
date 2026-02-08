using UnityEngine;

namespace Project.Core
{
    public static class LocalProfile
    {
        private const string KeyName = "player_name";

        public static string GetName() =>
            PlayerPrefs.GetString(KeyName, "");

        public static void SetName(string name)
        {
            PlayerPrefs.SetString(KeyName, name);
            PlayerPrefs.Save();
        }
    }
}
