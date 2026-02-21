using Fusion;
using UnityEngine;

public class BallCenterCameraFollow : NetworkBehaviour
{
    [Header("Center Point (Scene Object)")]
    [Tooltip("Drag your center point transform here. If left empty, we'll try to find by name.")]
    public Transform centerPoint;

    [Tooltip("If centerPoint is not assigned, we will try to find a GameObject by this name.")]
    public string centerPointName = "CenterPoint";

    [Header("Static Camera Position")]
    [Tooltip("Static camera offset from the spawned ball. X=right, Y=up, Z=forward.")]
    public Vector3 cameraOffset = new Vector3(0f, 10f, -10f);

    [Tooltip("If true, rotate the offset by the player's spawn yaw so camera stays behind that player orientation.")]
    public bool offsetRelativeToPlayerYaw = true;

    [Header("Static Camera Rotation")]
    [Tooltip("If true, uses Fixed Euler Angles. If false, camera looks at center point.")]
    public bool useFixedRotation = false;

    [Tooltip("Used only when Use Fixed Rotation is enabled.")]
    public Vector3 fixedEulerAngles = new Vector3(45f, 0f, 0f);

    private Transform cam;

    public override void Spawned()
    {
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
        Vector3 offset = cameraOffset;
        if (offsetRelativeToPlayerYaw)
        {
            float yaw = transform.eulerAngles.y;
            offset = Quaternion.Euler(0f, yaw, 0f) * cameraOffset;
        }

        cam.position = transform.position + offset;

        if (useFixedRotation)
        {
            cam.rotation = Quaternion.Euler(fixedEulerAngles);
            return;
        }

        // IMPORTANT: keep vertical component in look direction.
        // Flattening Y here can point the camera at horizon/sky when camera is elevated.
        Vector3 lookTarget = centerPoint != null ? centerPoint.position : transform.position;
        Vector3 dir = lookTarget - cam.position;

        if (dir.sqrMagnitude < 0.0001f)
        {
            Vector3 planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (planarForward.sqrMagnitude < 0.0001f)
                planarForward = Vector3.forward;

            dir = planarForward;
        }

        cam.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }
}
