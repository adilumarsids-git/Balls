using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Leaderboard
{
    public class MenuLeaderboardPanel : MonoBehaviour
    {
        [Header("Required UI")]
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button refreshButton;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Transform contentRoot;
        [SerializeField] private LeaderboardRowView rowPrefab;

        [Header("Optional")]
        [SerializeField] private TMP_Text statusText;

        private readonly List<LeaderboardRowView> spawnedRows = new List<LeaderboardRowView>();
        private NftLeaderboardService service;

        private void Awake()
        {
            service = NftLeaderboardService.FindOrCreate();

            if (openButton != null)
                openButton.onClick.AddListener(OpenPanel);

            if (closeButton != null)
                closeButton.onClick.AddListener(ClosePanel);

            if (refreshButton != null)
                refreshButton.onClick.AddListener(OnRefreshClicked);

            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (openButton != null)
                openButton.onClick.RemoveListener(OpenPanel);

            if (closeButton != null)
                closeButton.onClick.RemoveListener(ClosePanel);

            if (refreshButton != null)
                refreshButton.onClick.RemoveListener(OnRefreshClicked);
        }

        public void OpenPanel()
        {
            if (panelRoot == null)
                return;

            panelRoot.SetActive(true);
            _ = RefreshAsync();
        }

        public void ClosePanel()
        {
            if (panelRoot == null)
                return;

            panelRoot.SetActive(false);
        }

        private void OnRefreshClicked()
        {
            _ = RefreshAsync();
        }

        public async System.Threading.Tasks.Task RefreshAsync()
        {
            if (contentRoot == null || rowPrefab == null)
            {
                UpdateStatus("Leaderboard UI refs missing.");
                return;
            }

            ClearRows();
            UpdateStatus("Loading...");

            try
            {
                var top = await service.GetTopAsync();
                if (top.Count == 0)
                {
                    // user requested: show nothing if no data
                    UpdateStatus(string.Empty);
                    return;
                }

                for (int i = 0; i < top.Count; i++)
                {
                    var rowData = top[i];
                    var row = Instantiate(rowPrefab, contentRoot);
                    row.Bind(rowData.Position + 1, rowData.DisplayName, rowData.Wins);
                    spawnedRows.Add(row);
                }

                UpdateStatus(string.Empty);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Leaderboard failed: {ex.Message}");
            }
        }

        private void ClearRows()
        {
            for (int i = 0; i < spawnedRows.Count; i++)
            {
                if (spawnedRows[i] != null)
                    Destroy(spawnedRows[i].gameObject);
            }

            spawnedRows.Clear();
        }

        private void UpdateStatus(string text)
        {
            if (statusText != null)
                statusText.text = text;
        }
    }
}
