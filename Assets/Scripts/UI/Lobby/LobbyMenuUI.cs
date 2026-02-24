using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Project.Networking.Fusion;
using Project.Wallet;

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

        private bool _recoveringLauncher;

        private void Awake()
        {
            launcher = FindObjectOfType<FusionLauncher>();

            if (launcher == null)
            {
                Debug.LogWarning("FusionLauncher not found in menu scene. Attempting bootstrap recovery.");
                StartBootstrapRecovery();
                return;
            }
            refreshButton.onClick.AddListener(() => _ = Refresh());
            hostButton.onClick.AddListener(() => _ = Host());
            joinButton.onClick.AddListener(() => _ = JoinByName());
        }

        private async void OnEnable()
        {
            if (nameInput != null)
                nameInput.text = Project.Core.LocalProfile.GetName();

            if (!EnsureLauncherAvailable())
                return;

            launcher.SessionListChanged += OnSessionListChanged;
            await Refresh();
        }

        private void OnDisable()
        {
            if (launcher != null)
                launcher.SessionListChanged -= OnSessionListChanged;
        }

        private async Task Refresh()
        {
            if (!EnsureLauncherAvailable()) return;
            await launcher.JoinPublicLobby();
            // session list will arrive via callback
        }

        private async Task Host()
        {
            if (!EnsureLauncherAvailable()) return;
            Project.Core.LocalProfile.SetName(nameInput != null ? nameInput.text : string.Empty);
            if (!EnsureSelectedNft())
                return;
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

            if (!EnsureSelectedNft())
                return;
            if (!EnsureLauncherAvailable()) return;
            await launcher.Join(room);
        }


        private bool EnsureLauncherAvailable()
        {
            if (launcher != null)
                return true;

            launcher = FindObjectOfType<FusionLauncher>();
            if (launcher != null)
                return true;

            StartBootstrapRecovery();
            return false;
        }

        private void StartBootstrapRecovery()
        {
            if (_recoveringLauncher)
                return;

            _recoveringLauncher = true;
            // Scene 0 contains NetworkBootstrap (FusionLauncher + Runner).
            SceneManager.LoadScene(0);
        }

        private bool EnsureSelectedNft()
        {
            var session = WalletSession.Instance ?? WalletSession.FindOrCreate();
            if (session != null && !session.EnsureSelectedNft())
            {
                Debug.LogWarning("No NFT selected for this wallet session. Hosting/Joining is blocked.");
                return false;
            }

            return true;
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
                if (s.PlayerCount >= s.MaxPlayers) continue;

                var item = Instantiate(listItemPrefab, listParent);
                item.Bind(s, launcher);
                spawned.Add(item);
            }
        }
    }
}
