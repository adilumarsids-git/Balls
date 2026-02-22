using System.Collections;
using Fusion;
using Project.Core.Bootstrap;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Networking.Fusion
{
    /// <summary>
    /// Performs a full client-side restart and returns to Bootstrap scene.
    /// WebGL-safe: avoids deleting engine/plugin DDOL roots directly.
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

            // 1) Shutdown all active runners first.
            var runners = FindObjectsOfType<NetworkRunner>(true);
            for (int i = 0; i < runners.Length; i++)
            {
                if (runners[i] != null && runners[i].IsRunning)
                    _ = runners[i].Shutdown();
            }

            // Wait briefly for shutdown callbacks to complete.
            float t = 0f;
            const float maxWait = 1.5f;
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

            // 2) Reset bootstrap static so scene 0 behaves like first app launch.
            ProjectBootstrap.ResetBootStateForRestart();

            // 3) Clear only project-owned DDOL roots (safe for WebGL runtime).
            DestroyProjectOwnedDontDestroyObjects();

            // 4) Give a frame for destroy queue to process then relaunch bootstrap.
            yield return null;
            SceneManager.LoadScene(bootstrapSceneBuildIndex, LoadSceneMode.Single);
        }

        private static void DestroyProjectOwnedDontDestroyObjects()
        {
            var ddolScene = GetDontDestroyOnLoadScene();
            if (!ddolScene.IsValid())
                return;

            var roots = ddolScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (root == null)
                    continue;

                // Destroy only app scripts from Assembly-CSharp.
                // Avoid touching engine/plugin internals that can crash WebGL when force-destroyed.
                var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
                bool hasProjectScript = false;
                for (int b = 0; b < behaviours.Length; b++)
                {
                    var mb = behaviours[b];
                    if (mb == null)
                        continue;

                    var asm = mb.GetType().Assembly.GetName().Name;
                    if (asm == "Assembly-CSharp")
                    {
                        hasProjectScript = true;
                        break;
                    }
                }

                if (hasProjectScript)
                    Destroy(root);
            }
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
