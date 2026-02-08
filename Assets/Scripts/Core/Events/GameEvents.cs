using System;
using UnityEngine;

namespace Project.Core.Events
{
    public static class GameEvents
    {
        public static event Action<GameObject> PlayerEliminated;
        public static event Action<GameObject> RoundWinner;
        public static event Action<GameObject> ConsumablePicked;

        public static void RaisePlayerEliminated(GameObject p) => PlayerEliminated?.Invoke(p);
        public static void RaiseRoundWinner(GameObject p) => RoundWinner?.Invoke(p);
        public static void RaiseConsumablePicked(GameObject p) => ConsumablePicked?.Invoke(p);
    }
}
