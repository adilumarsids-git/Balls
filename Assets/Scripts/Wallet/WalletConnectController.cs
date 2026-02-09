using System.Collections;
using System.Reflection;
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
        [SerializeField] private int menuSceneBuildIndex = 2;
        [SerializeField] private MonoBehaviour walletController;

        [Header("Runtime UI")]
        [SerializeField] private TMP_InputField walletInputField;
        [SerializeField] private Button connectButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private TMP_Text statusText;

        private void Awake()
        {
            gameObject.name = "WalletConnectController";
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
            if (walletInputField != null && connectButton != null && continueButton != null && statusText != null)
                return;

            var canvasGO = new GameObject("WalletConnectCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.AddComponent<GraphicRaycaster>();

            var panel = CreatePanel(canvas.transform);
            statusText = CreateText(panel, "StatusText", "Connect your wallet");
            walletInputField = CreateInput(panel, "WalletInput", "Wallet address");
            connectButton = CreateButton(panel, "ConnectButton", "Connect Wallet");
            continueButton = CreateButton(panel, "ContinueButton", "Continue");

            connectButton.onClick.AddListener(OnConnectClicked);
            continueButton.onClick.AddListener(OnContinueClicked);
            continueButton.interactable = false;
        }

        public void OnWalletConnected(string address)
        {
            WalletProfile.WalletAddress = address;
            walletInputField.text = address;
            UpdateStatus("Wallet connected successfully.");
            continueButton.interactable = true;
        }

        public void OnWalletError(string message)
        {
            UpdateStatus($"Wallet error: {message}");
        }

        private void OnConnectClicked()
        {
            if (TryStartSdkConnect())
                return;
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

        private void OnContinueClicked()
        {
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

        private bool TryStartSdkConnect()
        {
            if (walletController == null)
                walletController = FindWalletController();

            if (walletController == null)
                return false;

            var method = walletController.GetType().GetMethod("ConnectWallet", BindingFlags.Public | BindingFlags.Instance)
                ?? walletController.GetType().GetMethod("Connect", BindingFlags.Public | BindingFlags.Instance)
                ?? walletController.GetType().GetMethod("Login", BindingFlags.Public | BindingFlags.Instance);

            if (method == null)
            {
                UpdateStatus("Wallet controller found, but no connect method.");
                return false;
            }

            method.Invoke(walletController, null);
            StartCoroutine(PollWalletAddress(walletController));
            UpdateStatus("Connecting wallet...");
            return true;
        }

        private MonoBehaviour FindWalletController()
        {
            var behaviours = FindObjectsOfType<MonoBehaviour>(true);
            foreach (var behaviour in behaviours)
            {
                if (behaviour == null) continue;
                if (behaviour.GetType().Name == "WalletController")
                    return behaviour;
            }

            return null;
        }

        private IEnumerator PollWalletAddress(MonoBehaviour controller)
        {
            const float timeout = 20f;
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                var address = TryReadWalletAddress(controller);
                if (!string.IsNullOrWhiteSpace(address))
                {
                    OnWalletConnected(address);
                    yield break;
                }

                elapsed += 0.5f;
                yield return new WaitForSeconds(0.5f);
            }

            UpdateStatus("Wallet connection timed out.");
        }

        private string TryReadWalletAddress(MonoBehaviour controller)
        {
            if (controller == null)
                return null;

            var type = controller.GetType();
            var candidates = new[]
            {
                "WalletAddress", "PublicKey", "publicKey", "Account", "AccountPublicKey", "Base58", "Address"
            };

            foreach (var name in candidates)
            {
                var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (prop != null)
                {
                    var value = prop.GetValue(controller);
                    if (value != null)
                        return value.ToString();
                }

                var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    var value = field.GetValue(controller);
                    if (value != null)
                        return value.ToString();
                }
            }

            return null;
        }
    }
}
