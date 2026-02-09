using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Project.Core;

namespace Project.Wallet
{
    public class MenuColorSelectionUI : MonoBehaviour
    {
        [SerializeField] private BallColorCatalogSO colorCatalog;
        [SerializeField] private TMP_Dropdown dropdown;

        private void Awake()
        {
            EnsureEventSystem();
            if (dropdown == null)
                CreateDropdown();
        }

        private void Start()
        {
            PopulateOptions();
        }

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
                return;

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        private void CreateDropdown()
        {
            var canvasGO = new GameObject("MenuColorCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.AddComponent<GraphicRaycaster>();

            var dropdownGO = new GameObject("ColorDropdown");
            dropdownGO.transform.SetParent(canvas.transform, false);
            var rect = dropdownGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-30f, -30f);
            rect.sizeDelta = new Vector2(240f, 40f);
            dropdownGO.AddComponent<Image>().color = Color.white;
            dropdown = dropdownGO.AddComponent<TMP_Dropdown>();
            dropdown.captionText = CreateLabel(dropdownGO.transform, "Label");
            dropdown.template = CreateTemplate(dropdownGO.transform);
        }

        private void PopulateOptions()
        {
            dropdown.options.Clear();
            var owned = WalletProfile.OwnedColorIndices;
            var colors = colorCatalog != null ? colorCatalog.Colors : null;

            if (owned.Count == 0)
            {
                WalletProfile.SetOwnedColors(new[] { 0 });
                owned = WalletProfile.OwnedColorIndices;
            }

            foreach (var index in owned)
            {
                string label = $"Color {index}";
                if (colors != null && index >= 0 && index < colors.Count)
                    label = colors[index].displayName;
                dropdown.options.Add(new TMP_Dropdown.OptionData(label));
            }

            dropdown.value = 0;
            dropdown.onValueChanged.AddListener(OnSelectionChanged);
            OnSelectionChanged(0);
        }

        private void OnSelectionChanged(int dropdownIndex)
        {
            var owned = WalletProfile.OwnedColorIndices;
            if (owned.Count == 0)
                WalletProfile.SelectedColorIndex = 0;
            else if (dropdownIndex >= 0 && dropdownIndex < owned.Count)
                WalletProfile.SelectedColorIndex = owned[dropdownIndex];
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
