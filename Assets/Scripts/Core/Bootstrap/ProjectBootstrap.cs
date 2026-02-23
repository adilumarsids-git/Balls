using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Core.Bootstrap
{
    public class ProjectBootstrap : MonoBehaviour
    {
        [SerializeField] private int walletSceneBuildIndex = 1; // 01_WalletConnect
        [SerializeField] private int menuSceneBuildIndex = 2; // 02_Menu

        private static bool booted;

        public static void ResetBootStateForRestart()
        {
            booted = false;
        }

        private void Awake()
        {
            if (booted)
            {
                Destroy(gameObject);
                return;
            }

            booted = true;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // If we are in bootstrap scene, go to wallet connect
            if (SceneManager.GetActiveScene().buildIndex == 0)
            {
                SceneManager.LoadScene(walletSceneBuildIndex);
            }
        }
    }
}