using System.Runtime.InteropServices;
using UnityEngine;

namespace Project.Wallet
{
    public static class SolanaWalletBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void SolanaConnect(string gameObjectName);

        public static void Connect(string gameObjectName)
        {
            SolanaConnect(gameObjectName);
        }
#else
        public static void Connect(string gameObjectName)
        {
            Debug.Log("Solana wallet connect is only supported in WebGL builds.");
        }
#endif
    }
}
