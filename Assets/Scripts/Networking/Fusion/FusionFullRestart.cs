using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FusionFullRestart : NetworkBehaviour
{
    [Header("Restart Target")]
    [Tooltip("Bootstrap scene build index (you said Scene 0).")]
    public int bootstrapSceneIndex = 0;

    [Tooltip("Small delay so winner UI can show before restart.")]
    public float delaySeconds = 0.5f;

    private bool _restartStarted;

    /// <summary>
    /// Call this on the StateAuthority (Master Client object in Shared).
    /// This will restart ALL clients to Scene 0, fully fresh.
    /// </summary>
    public void RequestRestartAll()
    {
        if (_restartStarted) return;

        if (!Object.HasStateAuthority)
        {
            Debug.LogWarning("[FusionFullRestart] RequestRestartAll() must be called on StateAuthority.");
            return;
        }

        _restartStarted = true;
        RPC_RestartAllClients(bootstrapSceneIndex, delaySeconds);
    }

    /// <summary>
    /// Runs on every client. Each client tears down runner + DDOL and loads scene 0.
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_RestartAllClients(int sceneIndex, float delay)
    {
        if (_restartStarted) return;
        _restartStarted = true;
        StartCoroutine(RestartRoutine(sceneIndex, delay));
    }

    private IEnumerator RestartRoutine(int sceneIndex, float delay)
    {
        // optional delay for UX
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        // 1) Shutdown all Fusion runners in this client
        yield return ShutdownAllRunners();

        // 2) Destroy everything in DontDestroyOnLoad (true cold restart)
        DestroyAllDontDestroyOnLoadObjects();

        // 3) Load bootstrap scene fresh
        SceneManager.LoadScene(sceneIndex, LoadSceneMode.Single);
    }

    private static IEnumerator ShutdownAllRunners()
    {
        var runners = UnityEngine.Object.FindObjectsOfType<NetworkRunner>(true);

        foreach (var r in runners)
        {
            if (r == null) continue;

            // Shutdown tears down networking + simulation
            r.Shutdown();
        }

        // Wait until runners are actually not running (a few frames is usually enough)
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
        // Unity keeps DontDestroyOnLoad objects in a special hidden scene.
        var ddolScene = SceneManager.GetSceneByName("DontDestroyOnLoad");
        if (!ddolScene.IsValid()) return;

        var roots = ddolScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            // This will destroy ALL persistent objects from previous run
            UnityEngine.Object.Destroy(roots[i]);
        }
    }
}