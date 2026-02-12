using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.Wallet
{
    public class MenuSceneNftSelector : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private int walletSceneBuildIndex = 1;
        [SerializeField] private bool redirectIfDisconnected = true;
        [SerializeField] private bool enableDebugLogs = true;

        [Header("UI")]
        [SerializeField] private TMP_Dropdown nftDropdown;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private GameObject nftGatePanel;
        [SerializeField] private TMP_Text nftGateMessageText;
        [SerializeField] private Button buyNftsButton;
        [SerializeField] private string buyNftsUrl = "https://magiceden.io/marketplace/Dwy6E8TqvEvSRqKFxaFXFWo7BiY2HFMfjEDHsZHa8WtX";

        private readonly List<NftInfo> availableNfts = new List<NftInfo>();
        private WalletSession walletSession;

        protected virtual void Awake()
        {
            walletSession = WalletSession.FindOrCreate();
            EnsureEventSystem();
            FindUiIfMissing();

            if (buyNftsButton != null)
            {
                buyNftsButton.onClick.RemoveListener(OpenBuyNftsUrl);
                buyNftsButton.onClick.AddListener(OpenBuyNftsUrl);
            }

            SetGatePanelState(true, "Loading and checking for NFTs...", false);
        }

        protected virtual async void Start()
        {
            await InitializeAsync();
        }

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
                return;

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        private void FindUiIfMissing()
        {
            if (nftDropdown == null)
                nftDropdown = FindDropdownByName("ColorDropDown") ?? FindDropdownByName("NftDropdown");

            if (statusText == null)
                statusText = FindTextByName("NftStatusText");

            if (nftDropdown == null)
                CreateDropdownUi();
        }

        private TMP_Dropdown FindDropdownByName(string name)
        {
            var go = GameObject.Find(name);
            return go != null ? go.GetComponent<TMP_Dropdown>() : null;
        }

        private TMP_Text FindTextByName(string name)
        {
            var go = GameObject.Find(name);
            return go != null ? go.GetComponent<TMP_Text>() : null;
        }

        private void CreateDropdownUi()
        {
            var canvasGO = new GameObject("MenuNftCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.AddComponent<GraphicRaycaster>();

            var dropdownGO = new GameObject("NftDropdown");
            dropdownGO.transform.SetParent(canvas.transform, false);
            var rect = dropdownGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-30f, -30f);
            rect.sizeDelta = new Vector2(240f, 40f);
            dropdownGO.AddComponent<Image>().color = Color.white;
            nftDropdown = dropdownGO.AddComponent<TMP_Dropdown>();
            nftDropdown.captionText = CreateLabel(dropdownGO.transform, "Label");
            nftDropdown.template = CreateTemplate(dropdownGO.transform);
        }

        private async System.Threading.Tasks.Task InitializeAsync()
        {
            if (walletSession == null)
            {
                LogError("WalletSession is null in MenuSceneNftSelector.");
                SetGatePanelState(true, "Wallet session unavailable.", false);
                return;
            }

            if (!walletSession.IsConnected)
            {
                UpdateStatus("Please connect your wallet.");
                SetGatePanelState(true, "Please connect your wallet first.", false);
                LogWarning($"Wallet not connected in menu. redirectIfDisconnected={redirectIfDisconnected}");

                if (redirectIfDisconnected)
                    SceneManager.LoadScene(walletSceneBuildIndex);

                return;
            }

            UpdateStatus("Loading NFTs...");
            SetGatePanelState(true, "Loading and checking for NFTs...", false);

            try
            {
                var nfts = await walletSession.FetchOwnedNftsAsync();
                availableNfts.Clear();
                availableNfts.AddRange(nfts);
                LogDebug($"InitializeAsync fetched NFTs count: {availableNfts.Count}");
                PopulateDropdown();
            }
            catch (Exception ex)
            {
                UpdateStatus($"NFT load failed: {ex.Message}");
                SetGatePanelState(true, "NFT check failed. Please retry.", false);
                LogError($"NFT load failed: {ex}");
            }
        }

        private void PopulateDropdown()
        {
            if (nftDropdown == null)
                return;

            nftDropdown.options.Clear();

            if (availableNfts.Count == 0)
            {
                UpdateStatus("No NFTs found.");
                SetGatePanelState(true, "Sorry, you don't have any NFTs in your wallet.", true);
                LogWarning("No NFTs found after wallet fetch.");
                nftDropdown.interactable = false;
                return;
            }

            foreach (var nft in availableNfts)
                nftDropdown.options.Add(new TMP_Dropdown.OptionData(nft.Name));

            nftDropdown.onValueChanged.RemoveAllListeners();
            nftDropdown.onValueChanged.AddListener(OnSelectionChanged);

            int selectedIndex = 0;
            if (walletSession.SelectedNft != null)
            {
                var currentIndex = availableNfts.FindIndex(n => n.Mint == walletSession.SelectedNft.Mint);
                if (currentIndex >= 0)
                    selectedIndex = currentIndex;
            }

            nftDropdown.value = selectedIndex;
            nftDropdown.RefreshShownValue();
            nftDropdown.interactable = true;
            OnSelectionChanged(selectedIndex);

            UpdateStatus($"Loaded {availableNfts.Count} NFTs.");
            SetGatePanelState(false, "NFTs found. Welcome to the game.", false);

            for (int i = 0; i < availableNfts.Count; i++)
            {
                var nft = availableNfts[i];
                LogDebug($"Dropdown NFT[{i}] name='{nft.Name}', mint='{nft.Mint}', skinId='{nft.SkinId}'");
            }
        }

        private void OnSelectionChanged(int index)
        {
            if (index < 0 || index >= availableNfts.Count)
                return;

            walletSession.SelectNft(availableNfts[index].Mint);
            var selected = availableNfts[index];
            LogDebug($"Selected NFT index={index}, name='{selected.Name}', mint='{selected.Mint}', skinId='{selected.SkinId}'");
        }

        private void UpdateStatus(string text)
        {
            if (statusText != null)
                statusText.text = text;

            LogDebug($"Status: {text}");
        }

        private void SetGatePanelState(bool visible, string message, bool showBuyButton)
        {
            if (nftGatePanel != null)
                nftGatePanel.SetActive(visible);

            if (nftGateMessageText != null)
                nftGateMessageText.text = message;

            if (buyNftsButton != null)
                buyNftsButton.gameObject.SetActive(showBuyButton);
        }

        private void OpenBuyNftsUrl()
        {
            if (string.IsNullOrWhiteSpace(buyNftsUrl))
                return;

            Application.OpenURL(buyNftsUrl);
        }

        private void LogDebug(string message)
        {
            if (!enableDebugLogs)
                return;

            Debug.Log($"[MenuSceneNftSelector] {message}");
        }

        private void LogWarning(string message)
        {
            if (!enableDebugLogs)
                return;

            Debug.LogWarning($"[MenuSceneNftSelector] {message}");
        }

        private void LogError(string message)
        {
            Debug.LogError($"[MenuSceneNftSelector] {message}");
        }

        private static TMP_Text CreateLabel(Transform parent, string name)
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

        private static RectTransform CreateTemplate(Transform parent)
        {
            var template = new GameObject("Template");
            template.transform.SetParent(parent, false);
            template.SetActive(false);
            var rect = template.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, 150f);

            template.AddComponent<Image>().color = Color.white;
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
            item.AddComponent<Toggle>();
            item.AddComponent<Image>().color = new Color(0.95f, 0.95f, 0.95f, 1f);
            var label = new GameObject("Item Label");
            label.transform.SetParent(item.transform, false);
            var text = label.AddComponent<TextMeshProUGUI>();
            text.color = Color.black;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(10f, 0f);
            labelRect.offsetMax = new Vector2(-10f, 0f);

            return rect;
        }
    }
}
