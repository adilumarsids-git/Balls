using System.Threading.Tasks;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Project.Networking.Fusion;

namespace Project.UI.Lobby
{
    public class SessionListItemUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text playersText;
        [SerializeField] private Button joinButton;
        [SerializeField] private TMP_Text mapText;

        private string roomName;
        private FusionLauncher launcher;

        public void Bind(SessionInfo info, FusionLauncher fusionLauncher)
        {
            launcher = fusionLauncher;
            roomName = info.Name;

            titleText.text = roomName;
            playersText.text = $"{info.PlayerCount}/{info.MaxPlayers}";

            string mapLabel = "Map ?";
            if (info.Properties != null && info.Properties.TryGetValue("map", out var prop))
            {
                int mapIndex = prop;
                mapLabel = (mapIndex == 4) ? "Island B" : "Island A";
            }

            if (mapText != null)
                mapText.text = mapLabel;


            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(() => _ = Join());
        }

        private async Task Join()
        {
            await launcher.Join(roomName);
        }
    }
}
