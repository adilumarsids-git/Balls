using System.Collections;
using Fusion;
using Project.Core.Bootstrap;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Networking.Fusion
{
    /// <summary>
    /// Performs a full client-side restart by clearing DontDestroyOnLoad objects
    /// and loading the Bootstrap scene.
    /// </summary>
    public class MatchEndBootstrapRestart : MonoBehaviour
    {
        private bool _isRestarting;

        public void ScheduleRestart(int bootstrapSceneBuildIndex, float delaySeconds)
        {
            if (_isRestarting)
                return;

            _isRestarting = true;
            StartCoroutine(RestartRoutine(bootstrapSceneBuildIndex, Mathf.Max(0f, delaySeconds)));
        }

        private IEnumerator RestartRoutine(int bootstrapSceneBuildIndex, float delaySeconds)
        {
            if (delaySeconds > 0f)
                yield return new WaitForSeconds(delaySeconds);

            // First, shutdown all active runners gracefully so Photon/Fusion starts clean next boot.
            var runners = FindObjectsOfType<NetworkRunner>(true);
            for (int i = 0; i < runners.Length; i++)
            {
                if (runners[i] != null && runners[i].IsRunning)
                    _ = runners[i].Shutdown();
            }

            // Give shutdown callbacks a short window to finish.
            float t = 0f;
            const float maxWait = 1.0f;
            while (t < maxWait)
            {
                bool anyRunning = false;
                for (int i = 0; i < runners.Length; i++)
                {
                    if (runners[i] != null && runners[i].IsRunning)
                    {
                        anyRunning = true;
                        break;
                    }
                }

                if (!anyRunning)
                    break;

                t += Time.unscaledDeltaTime;
                yield return null;
            }

            // Reset bootstrap static so loading scene 0 behaves like a truly fresh launch.
            ProjectBootstrap.ResetBootStateForRestart();

            // Find the internal DontDestroyOnLoad scene and destroy all roots inside it.
            var ddolScene = GetDontDestroyOnLoadScene();
            if (ddolScene.IsValid())
            {
                var roots = ddolScene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    if (roots[i] != null)
                        Destroy(roots[i]);
                }
            }

            // Wait one frame so destroys are processed before loading bootstrap.
            yield return null;

            SceneManager.LoadScene(bootstrapSceneBuildIndex, LoadSceneMode.Single);
        }

        private static Scene GetDontDestroyOnLoadScene()
        {
            var temp = new GameObject("__DDOL_SCENE_PROBE__");
            DontDestroyOnLoad(temp);
            var scene = temp.scene;
            Destroy(temp);
            return scene;
        }
    }
}
