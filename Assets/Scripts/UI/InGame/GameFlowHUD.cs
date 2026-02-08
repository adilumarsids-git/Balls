using TMPro;
using UnityEngine;
using Project.Networking.Fusion;

namespace Project.UI.InGame
{
    public class GameFlowHUD : MonoBehaviour
    {
        [SerializeField] private TMP_Text statusText;

        [Header("Copy")]
        [SerializeField] private string waitingFormat = "Waiting for more players to join... ({0}/4)";
        [SerializeField] private string countdownFormat = "Game starts in {0}";
        [SerializeField] private string goText = "GO!";

        private float goUntil;

        private void Update()
        {
            var flow = NetworkGameFlowManager.Instance;
            if (statusText == null) return;

            // IMPORTANT: do not touch Networked props until flow is spawned/ready
            if (flow == null || !flow.IsReady)
            {
                statusText.text = "";
                return;
            }

            // GameOver first (so it doesn't get overwritten)
            if (flow.State == MatchFlowState.GameOver)
            {
                statusText.text = $"Winner: {flow.WinnerName}";
                return;
            }

            if (flow.State == MatchFlowState.WaitingForPlayers)
            {
                statusText.text = string.Format(waitingFormat, flow.CurrentPlayers);
                return;
            }

            if (flow.State == MatchFlowState.Countdown)
            {
                int sec = Mathf.CeilToInt(flow.GetCountdownRemaining());
                statusText.text = string.Format(countdownFormat, sec);
                return;
            }

            // Playing
            if (Time.time < goUntil)
                statusText.text = goText;
            else
                statusText.text = "";
        }
        // Optional: if you want GO flash exactly when playing starts,
        // call this from a small script that detects state change.
        public void FlashGo(float duration = 0.8f)
        {
            goUntil = Time.time + duration;
        }
    }
}
