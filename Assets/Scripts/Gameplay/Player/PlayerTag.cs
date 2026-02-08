using Fusion;
using UnityEngine;

namespace Project.Gameplay.Player
{
    [DisallowMultipleComponent]
    public class PlayerTag : MonoBehaviour
    {
        public NetworkObject NetObj { get; private set; }

        private void Awake()
        {
            NetObj = GetComponent<NetworkObject>();
        }
    }
}
