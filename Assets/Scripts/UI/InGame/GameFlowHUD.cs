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
        [SerializeField] private string eliminatedText = "You fell! Waiting for the round to finish...";
        [SerializeField] private string roundFormat = "Round {0}/{1}";

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
            if (flow.IsLocalPlayerEliminated())
            {
                statusText.text = eliminatedText;
                return;
            }

            string roundText = string.Format(roundFormat, flow.CurrentRound, flow.TotalRounds);
            if (Time.time < goUntil)
                statusText.text = $"{goText}\n{roundText}";
            else
                statusText.text = roundText;
        }
        // Optional: if you want GO flash exactly when playing starts,
        // call this from a small script that detects state change.
        public void FlashGo(float duration = 0.8f)
        {
            goUntil = Time.time + duration;
        }
    }
}
