using UnityEngine;

namespace Project.Leaderboard
{
    [CreateAssetMenu(menuName = "Project/Config/NFT Leaderboard Config", fileName = "NftLeaderboardConfig")]
    public class NftLeaderboardConfigSO : ScriptableObject
    {
        [SerializeField] private string titleId;
        [SerializeField] private string statisticName = "NFT_WINS";
        [SerializeField] private int maxEntries = 20;

        public string TitleId => titleId;
        public string StatisticName => statisticName;
        public int MaxEntries => Mathf.Max(1, maxEntries);
    }
}
