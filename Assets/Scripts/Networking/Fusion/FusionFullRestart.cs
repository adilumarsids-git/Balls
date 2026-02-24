using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FusionFullRestart : NetworkBehaviour
{
    public static bool IsRestarting { get; private set; }

    public static void ClearRestartFlag()
    {
        IsRestarting = false;
    }

    [Header("Restart Target")]
    [Tooltip("Bootstrap scene build index (you said Scene 0).")]
    public int bootstrapSceneIndex = 0;

    [Tooltip("Small delay so winner UI can show before restart.")]
    public float delaySeconds = 0.5f;

    private bool _restartStarted;

    public void RequestRestartAll()
    {
        if (_restartStarted) return;

        if (!Object.HasStateAuthority)
        {
            Debug.LogWarning("[FusionFullRestart] RequestRestartAll() must be called on StateAuthority.");
            return;
        }

        _restartStarted = true;
        IsRestarting = true;

        // Run locally on host immediately using non-network orchestrator.
        FusionRestartOrchestrator.Run(bootstrapSceneIndex, delaySeconds);

        // Tell all peers to do the same.
        RPC_RestartAllClients(bootstrapSceneIndex, delaySeconds);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_RestartAllClients(int sceneIndex, float delay)
    {
        if (_restartStarted) return;
        _restartStarted = true;
        IsRestarting = true;
        FusionRestartOrchestrator.Run(sceneIndex, delay);
    }

    private sealed class FusionRestartOrchestrator : MonoBehaviour
    {
        private static FusionRestartOrchestrator _instance;
        private bool _running;

        public static void Run(int sceneIndex, float delay)
        {
            if (_instance == null)
            {
                var go = new GameObject("FusionRestartOrchestrator");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<FusionRestartOrchestrator>();
            }

            _instance.Begin(sceneIndex, delay);
        }

        private void Begin(int sceneIndex, float delay)
        {
            if (_running) return;
            _running = true;
            StartCoroutine(RestartRoutine(sceneIndex, delay));
        }

        private IEnumerator RestartRoutine(int sceneIndex, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);

            yield return ShutdownAllRunners();

            DestroyAllDontDestroyOnLoadObjects();

            Project.Core.Bootstrap.ProjectBootstrap.ResetBootStateForRestart();
            SceneManager.LoadScene(sceneIndex, LoadSceneMode.Single);

            _running = false;
        }

        private static IEnumerator ShutdownAllRunners()
        {
            var runners = UnityEngine.Object.FindObjectsOfType<NetworkRunner>(true);

            foreach (var r in runners)
            {
                if (r == null) continue;
                r.Shutdown();
            }

            float timeout = 5f;
            while (timeout > 0f)
            {
                bool anyRunning = false;
                var stillThere = UnityEngine.Object.FindObjectsOfType<NetworkRunner>(true);
                foreach (var r in stillThere)
                {
                    if (r != null && r.IsRunning)
                    {
                        anyRunning = true;
                        break;
                    }
                }

                if (!anyRunning)
                    break;

                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private static void DestroyAllDontDestroyOnLoadObjects()
        {
            var ddolScene = SceneManager.GetSceneByName("DontDestroyOnLoad");
            if (!ddolScene.IsValid()) return;

            var roots = ddolScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                UnityEngine.Object.Destroy(roots[i]);
        }
    }
}
