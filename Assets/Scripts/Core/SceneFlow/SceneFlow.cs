using System.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Project.Core.SceneFlow
{
    public static class SceneFlow
    {
        private const string MENU = "01_Menu";
        private const string LOBBY = "02_Lobby";

        public static async void LoadMenu() => await LoadSingle(MENU);
        public static async void LoadLobby() => await LoadSingle(LOBBY);

        public static async void LoadMap(string mapSceneName) => await LoadSingle(mapSceneName);

        private static async Task LoadSingle(string sceneName)
        {
            if (SceneManager.GetActiveScene().name == sceneName)
                return;

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            while (!op.isDone)
                await Task.Yield();
        }
    }
}
