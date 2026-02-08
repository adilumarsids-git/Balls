using UnityEngine;
using Project.Networking.Fusion;

namespace Project.UI.InGame
{
    public class GameFlowStateWatcher : MonoBehaviour
    {
        [SerializeField] private GameFlowHUD hud;

        private MatchFlowState lastState;

        private void Update()
        {
            var flow = NetworkGameFlowManager.Instance;
            if (flow == null || hud == null) return;

            if (flow.State != lastState)
            {
                if (flow.State == MatchFlowState.Playing)
                    hud.FlashGo();

                lastState = flow.State;
            }
        }
    }
}
