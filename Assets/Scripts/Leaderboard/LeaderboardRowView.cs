using TMPro;
using UnityEngine;

namespace Project.Leaderboard
{
    public class LeaderboardRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text rankText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text winsText;

        public void Bind(int rank, string displayName, int wins)
        {
            if (rankText != null)
                rankText.text = rank.ToString();

            if (nameText != null)
                nameText.text = string.IsNullOrWhiteSpace(displayName) ? "Unknown" : displayName;

            if (winsText != null)
                winsText.text = wins.ToString();
        }
    }
}
