using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Core.Bootstrap
{
    public class ProjectBootstrap : MonoBehaviour
    {
        [SerializeField] private int menuSceneBuildIndex = 1; // 01_Menu

        private static bool booted;

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
            // If we are in bootstrap scene, go to menu
            if (SceneManager.GetActiveScene().buildIndex == 0)
            {
                SceneManager.LoadScene(menuSceneBuildIndex);
            }
        }
    }
}
