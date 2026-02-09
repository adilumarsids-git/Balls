using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Project.Networking.Fusion;

namespace Project.UI.Lobby
{
    public class LobbyMenuUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private FusionLauncher launcher;

        [Header("Inputs")]
        [SerializeField] private TMP_InputField roomNameInput;

        [Header("Buttons")]
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;

        [Header("Room List")]
        [SerializeField] private Transform listParent;
        [SerializeField] private SessionListItemUI listItemPrefab;

        [SerializeField] private TMP_Dropdown mapDropdown;
        [SerializeField] private int mapABuildIndex = 4;
        [SerializeField] private int mapBBuildIndex = 5;


        private readonly List<SessionListItemUI> spawned = new List<SessionListItemUI> ();
        [SerializeField] private TMP_InputField nameInput;

        private void Awake()
        {
            launcher = FindObjectOfType<FusionLauncher>();

            if (launcher == null)
            {
                Debug.LogError("FusionLauncher not found! Is NetworkBootstrap in Bootstrap scene?");
                return;
            }
            refreshButton.onClick.AddListener(() => _ = Refresh());
            hostButton.onClick.AddListener(() => _ = Host());
            joinButton.onClick.AddListener(() => _ = JoinByName());
        }

        private async void OnEnable()
        {
            nameInput.text = Project.Core.LocalProfile.GetName();
            launcher.SessionListChanged += OnSessionListChanged;
            await Refresh();
        }

        private void OnDisable()
        {
            launcher.SessionListChanged -= OnSessionListChanged;
        }

        private async Task Refresh()
        {
            await launcher.JoinPublicLobby();
            // session list will arrive via callback
        }

        private async Task Host()
        {
            Project.Core.LocalProfile.SetName(nameInput.text);
            string room = roomNameInput.text.Trim();
            int mapIndex = (mapDropdown != null && mapDropdown.value == 1)
                ? mapBBuildIndex
                : mapABuildIndex;

            await launcher.Host(room, mapIndex, maxPlayers: 4);
        }

        private async Task JoinByName()
        {
            string room = roomNameInput.text.Trim();
            if (string.IsNullOrEmpty(room)) return;

            await launcher.Join(room);
        }

        private void OnSessionListChanged(IReadOnlyList<SessionInfo> sessions)
        {
            // Clear
            for (int i = 0; i < spawned.Count; i++)
                Destroy(spawned[i].gameObject);
            spawned.Clear();

            // Rebuild
            foreach (var s in sessions)
            {
                if (!s.IsVisible || !s.IsOpen) continue;

                var item = Instantiate(listItemPrefab, listParent);
                item.Bind(s, launcher);
                spawned.Add(item);
            }
        }
    }
}
