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

    [Tooltip("If true, rotate the offset by the player's spawn yaw so camera starts behind player orientation.")]
    public bool offsetRelativeToPlayerYaw = true;

    [Header("Static Camera Rotation")]
    [Tooltip("If true, uses Fixed Euler Angles. If false, camera looks at center point once on spawn.")]
    public bool useFixedRotation = false;

    [Tooltip("Used only when Use Fixed Rotation is enabled.")]
    public Vector3 fixedEulerAngles = new Vector3(45f, 0f, 0f);

    [Header("Aim Assist")]
    [Tooltip("Recommended: force camera to look at the local ball on spawn to avoid wrong sky/horizon aim.")]
    public bool forceLookAtBall = true;

    [Tooltip("Looks slightly below the ball center for a subtle downward tilt.")]
    public float lookBelowBall = 0.5f;

    [Header("Follow (Position Only)")]
    [Tooltip("If enabled, camera follows player position only. Rotation remains unchanged after spawn.")]
    public bool followPlayerPosition = true;

    [Range(1f, 40f)]
    [Tooltip("Position follow smoothness. Higher = tighter follow.")]
    public float followLerpSpeed = 12f;

    [Tooltip("If camera drifts farther than this, snap instead of lerp.")]
    public float followSnapDistance = 8f;

    private Transform cam;
    private bool isActive;
    private Vector3 runtimeOffset;

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
        SetupSpawnCameraPose();
        isActive = true;
    }

    private void LateUpdate()
    {
        if (!isActive || cam == null || !followPlayerPosition)
            return;

        Vector3 targetPos = transform.position + runtimeOffset;
        float dist = Vector3.Distance(cam.position, targetPos);

        if (dist > followSnapDistance)
        {
            cam.position = targetPos;
        }
        else
        {
            float t = 1f - Mathf.Exp(-followLerpSpeed * Time.deltaTime);
            cam.position = Vector3.Lerp(cam.position, targetPos, t);
        }

        // Intentionally no rotation update here.
        // This keeps camera from rotating/orbiting while still following player position.
    }

    private void ResolveCenterPoint()
    {
        if (centerPoint != null || string.IsNullOrWhiteSpace(centerPointName))
            return;

        var go = GameObject.Find(centerPointName);
        if (go != null)
            centerPoint = go.transform;
    }

    private void SetupSpawnCameraPose()
    {
        runtimeOffset = cameraOffset;
        if (offsetRelativeToPlayerYaw)
        {
            float yaw = transform.eulerAngles.y;
            runtimeOffset = Quaternion.Euler(0f, yaw, 0f) * cameraOffset;
        }

        cam.position = transform.position + runtimeOffset;

        if (forceLookAtBall)
        {
            Vector3 ballAimPoint = transform.position + Vector3.down * Mathf.Abs(lookBelowBall);
            Vector3 toBall = ballAimPoint - cam.position;

            if (toBall.sqrMagnitude < 0.0001f)
                toBall = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

            if (toBall.sqrMagnitude < 0.0001f)
                toBall = Vector3.forward;

            cam.rotation = Quaternion.LookRotation(toBall.normalized, Vector3.up);
            return;
        }

        if (useFixedRotation)
        {
            cam.rotation = Quaternion.Euler(fixedEulerAngles);
            return;
        }

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
