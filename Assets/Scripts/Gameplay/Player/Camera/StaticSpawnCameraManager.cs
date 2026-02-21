using UnityEngine;

namespace Project.Gameplay.Player.Camera
{
    public class StaticSpawnCameraManager : MonoBehaviour
    {
        [SerializeField] private Camera[] spawnCameras = new Camera[4];

        private static StaticSpawnCameraManager _instance;

        private void Awake()
        {
            _instance = this;
            DisableAllCameras();
        }

        public static Camera GetActiveCamera()
        {
            return _instance != null ? _instance.GetCurrentEnabledCamera() : null;
        }

        public static void SetActiveCameraForSpawn(int spawnIndex)
        {
            if (_instance == null)
            {
                Debug.LogWarning("[StaticSpawnCameraManager] No manager found in scene.");
                return;
            }

            _instance.ActivateCamera(spawnIndex);
        }

        private void ActivateCamera(int spawnIndex)
        {
            if (spawnCameras == null || spawnCameras.Length == 0)
            {
                Debug.LogWarning("[StaticSpawnCameraManager] No spawn cameras configured.");
                return;
            }

            int clampedIndex = Mathf.Clamp(spawnIndex, 0, spawnCameras.Length - 1);
            for (int i = 0; i < spawnCameras.Length; i++)
            {
                if (spawnCameras[i] == null)
                    continue;

                spawnCameras[i].enabled = i == clampedIndex;
            }
        }

        private Camera GetCurrentEnabledCamera()
        {
            if (spawnCameras == null)
                return null;

            for (int i = 0; i < spawnCameras.Length; i++)
            {
                if (spawnCameras[i] != null && spawnCameras[i].enabled)
                    return spawnCameras[i];
            }

            return null;
        }

        private void DisableAllCameras()
        {
            if (spawnCameras == null)
                return;

            for (int i = 0; i < spawnCameras.Length; i++)
            {
                if (spawnCameras[i] != null)
                    spawnCameras[i].enabled = false;
            }
        }
    }
}
