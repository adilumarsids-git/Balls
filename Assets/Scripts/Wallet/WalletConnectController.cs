using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Project.Core;

namespace Project.Wallet
{
    public class WalletConnectController : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private BallColorCatalogSO colorCatalog;
        [SerializeField] private string collectionId = "Dwy6E8TqvEvSRqKFxaFXFWo7BiY2HFMfjEDHsZHa8WtX";
        [SerializeField] private int menuSceneBuildIndex = 2;

        [Header("Runtime UI")]
        [SerializeField] private TMP_InputField walletInputField;
        [SerializeField] private Button connectButton;
        [SerializeField] private TMP_Dropdown colorDropdown;
        [SerializeField] private Button continueButton;
        [SerializeField] private TMP_Text statusText;

        private MagicEdenNftClient nftClient;

        private void Awake()
        {
            gameObject.name = "WalletConnectController";
            nftClient = gameObject.AddComponent<MagicEdenNftClient>();
            EnsureEventSystem();
            EnsureUi();
        }

        private void Start()
        {
            UpdateStatus("Connect your Solana wallet.");
        }

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
                return;

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        private void EnsureUi()
        {
            if (walletInputField != null && connectButton != null && colorDropdown != null && continueButton != null && statusText != null)
                return;

            var canvasGO = new GameObject("WalletConnectCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.AddComponent<GraphicRaycaster>();

            var panel = CreatePanel(canvas.transform);
            statusText = CreateText(panel, "StatusText", "Connect your wallet");
            walletInputField = CreateInput(panel, "WalletInput", "Enter wallet address (Editor)");
            connectButton = CreateButton(panel, "ConnectButton", "Connect Wallet");
            colorDropdown = CreateDropdown(panel, "ColorDropdown");
            continueButton = CreateButton(panel, "ContinueButton", "Continue");

            connectButton.onClick.AddListener(OnConnectClicked);
            continueButton.onClick.AddListener(OnContinueClicked);
            colorDropdown.interactable = false;
            continueButton.interactable = false;
        }

        public void OnWalletConnected(string address)
        {
            WalletProfile.WalletAddress = address;
            walletInputField.text = address;
            UpdateStatus("Wallet connected. Fetching NFTs...");
            StartCoroutine(LoadNfts(address));
        }

        public void OnWalletError(string message)
        {
            UpdateStatus($"Wallet error: {message}");
        }

        private void OnConnectClicked()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            SolanaWalletBridge.Connect(gameObject.name);
#else
            var address = walletInputField.text.Trim();
            if (string.IsNullOrWhiteSpace(address))
            {
                UpdateStatus("Enter a wallet address to continue.");
                return;
            }
            OnWalletConnected(address);
#endif
        }

        private IEnumerator LoadNfts(string walletAddress)
        {
            bool done = false;
            IList tokens = null;
            string error = null;

            yield return nftClient.FetchWalletTokens(walletAddress,
                list =>
                {
                    tokens = list;
                    done = true;
                },
                err =>
                {
                    error = err;
                    done = true;
                });

            while (!done)
                yield return null;

            if (!string.IsNullOrWhiteSpace(error))
            {
                UpdateStatus($"NFT fetch failed: {error}");
                yield break;
            }

            ApplyOwnedColors(tokens);
        }

        private void ApplyOwnedColors(IList tokens)
        {
            var owned = new HashSet<int>();
            foreach (var name in MagicEdenNftClient.ExtractNftNames(tokens, collectionId))
            {
                if (colorCatalog != null && colorCatalog.TryGetIndexByKeyword(name, out int index))
                    owned.Add(index);
            }

            if (owned.Count == 0)
            {
                owned.Add(0);
                UpdateStatus("No matching NFTs found. Default color unlocked.");
            }
            else
            {
                UpdateStatus("NFT colors loaded. Choose your ball color.");
            }

            WalletProfile.SetOwnedColors(owned);
            PopulateDropdown(WalletProfile.OwnedColorIndices);
        }

        private void PopulateDropdown(IReadOnlyList<int> owned)
        {
            colorDropdown.options.Clear();
            var colors = colorCatalog != null ? colorCatalog.Colors : null;

            for (int i = 0; i < owned.Count; i++)
            {
                int index = owned[i];
                string label = $"Color {index}";
                if (colors != null && index >= 0 && index < colors.Count)
                    label = colors[index].displayName;
                colorDropdown.options.Add(new TMP_Dropdown.OptionData(label));
            }

            colorDropdown.value = 0;
            colorDropdown.RefreshShownValue();
            colorDropdown.interactable = true;
            continueButton.interactable = true;
        }

        private void OnContinueClicked()
        {
            var owned = WalletProfile.OwnedColorIndices;
            if (owned.Count == 0)
                return;

            int selected = WalletProfile.OwnedColorIndices[0];
            if (colorDropdown.options.Count > 0)
            {
                int idx = colorDropdown.value;
                if (idx >= 0 && idx < owned.Count)
                    selected = owned[idx];
            }

            WalletProfile.SelectedColorIndex = selected;
            SceneManager.LoadScene(menuSceneBuildIndex);
        }

        private void UpdateStatus(string text)
        {
            if (statusText != null)
                statusText.text = text;
        }

        private static RectTransform CreatePanel(Transform parent)
        {
            var panel = new GameObject("Panel");
            panel.transform.SetParent(parent, false);
            var rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(600, 400);
            var img = panel.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.6f);
            return rect;
        }

        private static TMP_Text CreateText(RectTransform parent, string name, string text)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -20f);
            rect.sizeDelta = new Vector2(520, 40);
            var textComp = go.AddComponent<TextMeshProUGUI>();
            textComp.alignment = TextAlignmentOptions.Center;
            textComp.color = Color.white;
            textComp.text = text;
            return textComp;
        }

        private static TMP_InputField CreateInput(RectTransform parent, string name, string placeholder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(520, 40);
            rect.anchoredPosition = new Vector2(0f, 60f);

            var image = go.AddComponent<Image>();
            image.color = Color.white;
            var input = go.AddComponent<TMP_InputField>();
            input.textComponent = CreateInputText(go.transform, "Text");
            input.placeholder = CreatePlaceholder(go.transform, placeholder);
            return input;
        }

        private static TMP_Text CreateInputText(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(10f, 6f);
            rect.offsetMax = new Vector2(-10f, -6f);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.color = Color.black;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            return text;
        }

        private static TMP_Text CreatePlaceholder(Transform parent, string textValue)
        {
            var go = new GameObject("Placeholder");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(10f, 6f);
            rect.offsetMax = new Vector2(-10f, -6f);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.color = new Color(0.4f, 0.4f, 0.4f, 1f);
            text.text = textValue;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            return text;
        }

        private static Button CreateButton(RectTransform parent, string name, string label)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(260, 40);
            rect.anchoredPosition = label == "Continue" ? new Vector2(0f, -120f) : new Vector2(0f, 10f);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.2f, 0.6f, 1f, 1f);
            var button = go.AddComponent<Button>();

            var text = new GameObject("Text");
            text.transform.SetParent(go.transform, false);
            var textRect = text.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var textComp = text.AddComponent<TextMeshProUGUI>();
            textComp.text = label;
            textComp.alignment = TextAlignmentOptions.Center;
            textComp.color = Color.white;

            return button;
        }

        private static TMP_Dropdown CreateDropdown(RectTransform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(260, 40);
            rect.anchoredPosition = new Vector2(0f, -50f);

            var image = go.AddComponent<Image>();
            image.color = Color.white;
            var dropdown = go.AddComponent<TMP_Dropdown>();
            dropdown.captionText = CreateDropdownText(go.transform, "Label");
            dropdown.template = CreateDropdownTemplate(go.transform);
            return dropdown;
        }

        private static TMP_Text CreateDropdownText(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(10f, 6f);
            rect.offsetMax = new Vector2(-10f, -6f);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.color = Color.black;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            return text;
        }

        private static RectTransform CreateDropdownTemplate(Transform parent)
        {
            var template = new GameObject("Template");
            template.transform.SetParent(parent, false);
            template.SetActive(false);
            var rect = template.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, 150f);

            var image = template.AddComponent<Image>();
            image.color = Color.white;

            var scrollRect = template.AddComponent<ScrollRect>();
            scrollRect.vertical = true;
            scrollRect.horizontal = false;

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(template.transform, false);
            var viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = new Vector2(0f, 0f);
            viewportRect.anchorMax = new Vector2(1f, 1f);
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewport.AddComponent<Image>().color = Color.white;
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 0f);
            content.AddComponent<VerticalLayoutGroup>();
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;

            var item = new GameObject("Item");
            item.transform.SetParent(content.transform, false);
            var itemRect = item.AddComponent<RectTransform>();
            itemRect.sizeDelta = new Vector2(0f, 30f);
            item.AddComponent<Toggle>();
            item.AddComponent<Image>().color = new Color(0.95f, 0.95f, 0.95f, 1f);
            var itemLabel = new GameObject("Item Label");
            itemLabel.transform.SetParent(item.transform, false);
            var labelText = itemLabel.AddComponent<TextMeshProUGUI>();
            labelText.color = Color.black;
            labelText.alignment = TextAlignmentOptions.MidlineLeft;
            var labelRect = itemLabel.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(10f, 0f);
            labelRect.offsetMax = new Vector2(-10f, 0f);

            return rect;
        }
    }
}
