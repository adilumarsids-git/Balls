using Fusion;
using UnityEngine;

public class AttachLocalCameraToBall : NetworkBehaviour
{
    [SerializeField] private Transform cameraPivot; // drag CameraPivot here

    public override void Spawned()
    {
        if (FindObjectOfType<Project.Gameplay.Player.Camera.StaticSpawnCameraManager>() != null)
        {
            enabled = false;
            return;
        }

        // Only local player attaches the camera
        if (!Object.HasInputAuthority) return;

        if (cameraPivot == null)
        {
            Debug.LogWarning("[AttachLocalCameraToBall] cameraPivot is not assigned.");
            return;
        }

        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[AttachLocalCameraToBall] No Camera.main found. Tag your scene camera MainCamera.");
            return;
        }

        // Parent camera to pivot (keeps it stable, no crazy spinning calculations)
        Transform camT = cam.transform;
        camT.SetParent(cameraPivot, worldPositionStays: false);

        // Optional: ensure exact pose
        camT.localPosition = Vector3.zero;
        camT.localRotation = Quaternion.identity;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        // Optional cleanup: unparent when local player despawns
        if (!Object.HasInputAuthority) return;

        var cam = Camera.main;
        if (cam != null)
            cam.transform.SetParent(null, worldPositionStays: true);
    }
}
