using Fusion;
using UnityEngine;

public class BallCenterCameraFollow : NetworkBehaviour
{
    [Header("Center Point (Scene Object)")]
    [Tooltip("Drag your center point transform here. If left empty, we'll try to find by name.")]
    public Transform centerPoint;

    [Tooltip("If centerPoint is not assigned, we will try to find a GameObject by this name.")]
    public string centerPointName = "CenterPoint";

    [Header("Local Camera Placement (static after spawn)")]
    [Tooltip("How far behind the spawned ball the local camera starts (on XZ plane).")]
    public float distanceBehindBall = 10f;

    [Tooltip("Camera height above the spawned ball.")]
    public float height = 10f;

    [Tooltip("If true, camera looks to center. If false, camera keeps spawn-forward look direction.")]
    public bool lookAtCenter = true;

    private Transform cam;

    public override void Spawned()
    {
        // Only the local owning player controls a camera.
        if (!Object.HasInputAuthority)
            return;

        cam = Camera.main != null ? Camera.main.transform : null;
        if (cam == null)
        {
            Debug.LogWarning("[BallCenterCameraFollow] No Camera.main found. Ensure Main Camera is tagged MainCamera.");
            return;
        }

        ResolveCenterPoint();
        SnapLocalCameraAtSpawn();
    }

    private void ResolveCenterPoint()
    {
        if (centerPoint != null || string.IsNullOrWhiteSpace(centerPointName))
            return;

        var go = GameObject.Find(centerPointName);
        if (go != null)
            centerPoint = go.transform;
    }

    private void SnapLocalCameraAtSpawn()
    {
        // Use spawn-forward (already aligned to map center by spawner) and place camera behind it.
        Vector3 planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (planarForward.sqrMagnitude < 0.0001f)
            planarForward = Vector3.forward;

        Vector3 camPos = transform.position - planarForward * distanceBehindBall + Vector3.up * height;
        cam.position = camPos;

        if (lookAtCenter && centerPoint != null)
        {
            Vector3 dir = centerPoint.position - cam.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
                dir = planarForward;

            cam.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
        else
        {
            cam.rotation = Quaternion.LookRotation(planarForward, Vector3.up);
        }
    }
}
