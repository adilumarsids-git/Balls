using Project.Leaderboard;

namespace Project.Wallet
{
    public class MenuColorSelectionUI : MenuSceneNftSelector
    {
        protected override void Awake()
        {
            base.Awake();

            NftLeaderboardService.FindOrCreate();
            if (GetComponent<MenuLeaderboardPanel>() == null)
                gameObject.AddComponent<MenuLeaderboardPanel>();
        }

        protected override void Start()
        {
            base.Start();
        }
    }
}
