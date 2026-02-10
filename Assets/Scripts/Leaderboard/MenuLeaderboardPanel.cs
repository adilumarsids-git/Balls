using System;
using System.Text;
using Project.Wallet;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Leaderboard
{
    public class MenuLeaderboardPanel : MonoBehaviour
    {
        [SerializeField] private Button openButton;
        [SerializeField] private Button refreshButton;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text contentText;
        [SerializeField] private TMP_Text statusText;

        private NftLeaderboardService service;

        private void Awake()
        {
            service = NftLeaderboardService.FindOrCreate();
            EnsureUi();

            if (openButton != null)
                openButton.onClick.AddListener(TogglePanel);

            if (refreshButton != null)
                refreshButton.onClick.AddListener(() => _ = RefreshAsync());

            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        private void EnsureUi()
        {
            if (openButton == null)
                openButton = FindButton("LeaderboardButton");

            if (refreshButton == null)
                refreshButton = FindButton("LeaderboardRefreshButton");

            if (panelRoot == null)
            {
                var go = GameObject.Find("LeaderboardPanel");
                if (go != null)
                    panelRoot = go;
            }

            if (contentText == null)
                contentText = FindText("LeaderboardContentText");

            if (statusText == null)
                statusText = FindText("LeaderboardStatusText");

            if (openButton == null || refreshButton == null || panelRoot == null || contentText == null || statusText == null)
                CreateFallbackUi();
        }

        private async System.Threading.Tasks.Task RefreshAsync()
        {
            UpdateStatus("Loading leaderboard...");
            try
            {
                var top = await service.GetTopAsync();
                if (top.Count == 0)
                {
                    contentText.text = "No entries yet.";
                    UpdateStatus("Leaderboard is empty.");
                    return;
                }

                var sb = new StringBuilder();
                foreach (var row in top)
                {
                    var label = string.IsNullOrWhiteSpace(row.DisplayName) ? row.NftKey : row.DisplayName;
                    sb.AppendLine($"#{row.Position + 1}  {label}  -  {row.Wins} wins");
                }

                contentText.text = sb.ToString();
                UpdateStatus($"Top {top.Count} loaded.");
            }
            catch (Exception ex)
            {
                contentText.text = string.Empty;
                UpdateStatus($"Leaderboard failed: {ex.Message}");
            }
        }

        private void TogglePanel()
        {
            if (panelRoot == null)
                return;

            var next = !panelRoot.activeSelf;
            panelRoot.SetActive(next);
            if (next)
                _ = RefreshAsync();
        }

        private void UpdateStatus(string text)
        {
            if (statusText != null)
                statusText.text = text;
        }

        private Button FindButton(string name)
        {
            var go = GameObject.Find(name);
            return go != null ? go.GetComponent<Button>() : null;
        }

        private TMP_Text FindText(string name)
        {
            var go = GameObject.Find(name);
            return go != null ? go.GetComponent<TMP_Text>() : null;
        }

        private void CreateFallbackUi()
        {
            var canvasGo = new GameObject("LeaderboardCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            openButton = CreateButton(canvasGo.transform, "LeaderboardButton", "Leaderboard", new Vector2(120f, -30f));
            panelRoot = CreatePanel(canvasGo.transform, "LeaderboardPanel", new Vector2(0f, -20f), new Vector2(580f, 360f));
            refreshButton = CreateButton(panelRoot.transform, "LeaderboardRefreshButton", "Refresh", new Vector2(0f, -30f));
            contentText = CreateText(panelRoot.transform, "LeaderboardContentText", string.Empty, new Vector2(0f, -90f), new Vector2(520f, 220f));
            statusText = CreateText(panelRoot.transform, "LeaderboardStatusText", "Closed", new Vector2(0f, -330f), new Vector2(520f, 30f));

            panelRoot.SetActive(false);
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 anchoredPos, Vector2 size)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            var rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var image = panel.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.7f);
            return panel;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(200f, 36f);
            go.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.95f);
            var button = go.AddComponent<Button>();

            var text = new GameObject("Text");
            text.transform.SetParent(go.transform, false);
            var textRt = text.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var tmp = text.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.text = label;
            tmp.color = Color.white;

            return button;
        }

        private static TMP_Text CreateText(Transform parent, string name, string textValue, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = textValue;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.enableWordWrapping = true;
            return tmp;
        }
    }
}
