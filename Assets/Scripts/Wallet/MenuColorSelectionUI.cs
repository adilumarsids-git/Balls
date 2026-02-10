using Project.Leaderboard;

namespace Project.Wallet
{
    public class MenuColorSelectionUI : MenuSceneNftSelector
    {
        protected override void Awake()
        {
            base.Awake();

            // Keep leaderboard service available, but UI is now manually wired by designer.
            NftLeaderboardService.FindOrCreate();
        }

        protected override void Start()
        {
            base.Start();
        }
    }
}
