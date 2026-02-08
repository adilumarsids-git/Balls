using Fusion;
using UnityEngine;

namespace Project.Networking.Fusion
{
    public struct NetInput : INetworkInput
    {
        public Vector2 Move;          // x = horizontal, y = vertical
        public NetworkBool Boost;     // press-to-burst
    }
}
