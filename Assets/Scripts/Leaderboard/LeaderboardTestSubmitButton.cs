using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Leaderboard
{
    /// <summary>
    /// Simple manual test helper to submit wins to PlayFab without requiring wallet NFTs.
    /// Wire this to your own debug button in the menu scene.
    /// </summary>
    public class LeaderboardTestSubmitButton : MonoBehaviour
    {
        [Header("UI (optional)")]
        [SerializeField] private Button submitButton;
        [SerializeField] private TMP_InputField nftKeyInput;
        [SerializeField] private TMP_InputField displayNameInput;
        [SerializeField] private TMP_Text statusText;

        [Header("Fallback values")]
        [SerializeField] private string fallbackNftKey = "blue_ball_test";
        [SerializeField] private string fallbackDisplayName = "Blue Ball";
        [SerializeField] [Min(1)] private int winsToSubmit = 1;

        private bool isSubmitting;

        private void Awake()
        {
            if (submitButton != null)
                submitButton.onClick.AddListener(SubmitOnceFromUi);
        }

        private void OnDestroy()
        {
            if (submitButton != null)
                submitButton.onClick.RemoveListener(SubmitOnceFromUi);
        }

        /// <summary>
        /// Public so you can hook this directly from a Unity Button OnClick.
        /// </summary>
        public void SubmitOnceFromUi()
        {
            if (isSubmitting)
                return;

            _ = SubmitAsync();
        }

        private async System.Threading.Tasks.Task SubmitAsync()
        {
            try
            {
                isSubmitting = true;
                SetStatus("Submitting test score...");

                string nftKey = ReadOrFallback(nftKeyInput, fallbackNftKey);
                string displayName = ReadOrFallback(displayNameInput, fallbackDisplayName);

                var service = NftLeaderboardService.FindOrCreate();
                int submitCount = Mathf.Max(1, winsToSubmit);
                for (int i = 0; i < submitCount; i++)
                    await service.SubmitWinAsync(nftKey, displayName);

                SetStatus($"Submitted {submitCount} win(s) for '{nftKey}'.");
            }
            catch (Exception ex)
            {
                SetStatus($"Submit failed: {ex.Message}");
                Debug.LogError($"[{nameof(LeaderboardTestSubmitButton)}] {ex}");
            }
            finally
            {
                isSubmitting = false;
            }
        }

        private static string ReadOrFallback(TMP_InputField field, string fallback)
        {
            if (field == null)
                return fallback;

            var value = field.text?.Trim();
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;
        }
    }
}
