using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.Wallet
{
    public class WalletSceneController : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private int menuSceneBuildIndex = 2;

        [Header("UI")]
        [SerializeField] private Button connectButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private TMP_Text walletAddressText;
        [SerializeField] private TMP_Text statusText;

        private WalletSession walletSession;

        protected virtual void Awake()
        {
            walletSession = WalletSession.FindOrCreate();
            EnsureEventSystem();
            FindUiIfMissing();

            if (connectButton != null)
                connectButton.onClick.AddListener(OnConnectClicked);

            if (continueButton != null)
                continueButton.onClick.AddListener(OnContinueClicked);

            SetContinueVisible(false);
        }

        protected virtual void Start()
        {
            if (walletSession != null && walletSession.IsConnected)
            {
                UpdateStatus("Successfully connected.");
                UpdateWalletAddress(walletSession.WalletAddress);
                SetContinueVisible(true);
                connectButton.gameObject.SetActive(false);
            }
            else
            {
                UpdateStatus("Not connected.");
                UpdateWalletAddress(string.Empty);
            }
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
            if (connectButton == null)
                connectButton = FindButtonByName("ConnectButton");

            if (continueButton == null)
                continueButton = FindButtonByName("ContinueButton");

            if (statusText == null)
                statusText = FindTextByName("StatusText");

            if (walletAddressText == null)
                walletAddressText = FindTextByName("WalletAddressText") ?? FindInputTextByName("WalletInputField");

            if (connectButton == null || continueButton == null || statusText == null || walletAddressText == null)
                CreateFallbackUi();
        }

        private Button FindButtonByName(string name)
        {
            var go = GameObject.Find(name);
            return go != null ? go.GetComponent<Button>() : null;
        }

        private TMP_Text FindTextByName(string name)
        {
            var go = GameObject.Find(name);
            return go != null ? go.GetComponent<TMP_Text>() : null;
        }

        private TMP_Text FindInputTextByName(string name)
        {
            var go = GameObject.Find(name);
            if (go == null)
                return null;

            var input = go.GetComponent<TMP_InputField>();
            if (input != null)
            {
                input.interactable = false;
                return input.textComponent;
            }

            return go.GetComponent<TMP_Text>();
        }

        private void CreateFallbackUi()
        {
            var canvasGO = new GameObject("WalletSceneCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.AddComponent<GraphicRaycaster>();

            var panel = CreatePanel(canvas.transform);
            statusText = CreateText(panel, "StatusText", "Not connected.");
            walletAddressText = CreateText(panel, "WalletAddressText", "");
            connectButton = CreateButton(panel, "ConnectButton", "Connect");
            continueButton = CreateButton(panel, "ContinueButton", "Continue");

            connectButton.onClick.AddListener(OnConnectClicked);
            continueButton.onClick.AddListener(OnContinueClicked);
        }

        private async void OnConnectClicked()
        {
            if (walletSession == null)
                return;

            UpdateStatus("Connecting...");

            try
            {
                await walletSession.ConnectAsync();
                UpdateStatus("Successfully connected.");
                UpdateWalletAddress(walletSession.WalletAddress);
                SetContinueVisible(true);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Connection failed: {ex.Message}");
                SetContinueVisible(false);
            }
        }

        private void OnContinueClicked()
        {
            if (walletSession == null || !walletSession.IsConnected)
                return;

            SceneManager.LoadScene(menuSceneBuildIndex);
        }

        private void UpdateStatus(string text)
        {
            if (statusText != null)
                statusText.text = text;
        }

        private void UpdateWalletAddress(string address)
        {
            if (walletAddressText != null)
                walletAddressText.text = address ?? string.Empty;
        }

        private void SetContinueVisible(bool visible)
        {
            if (continueButton != null)
                continueButton.gameObject.SetActive(visible);
        }

        private static RectTransform CreatePanel(Transform parent)
        {
            var panel = new GameObject("Panel");
            panel.transform.SetParent(parent, false);
            var rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(600, 320);
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
            rect.anchoredPosition = name == "StatusText" ? new Vector2(0f, -20f) : new Vector2(0f, -70f);
            rect.sizeDelta = new Vector2(520, 40);
            var textComp = go.AddComponent<TextMeshProUGUI>();
            textComp.alignment = TextAlignmentOptions.Center;
            textComp.color = Color.white;
            textComp.text = text;
            return textComp;
        }

        private static Button CreateButton(RectTransform parent, string name, string label)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(260, 40);
            rect.anchoredPosition = label == "Continue" ? new Vector2(0f, -120f) : new Vector2(0f, -20f);

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
    }
}
