using Fusion;
using UnityEngine;

public class BallCenterCameraFollow : NetworkBehaviour
{
    [Header("Center Point (Scene Object)")]
    [Tooltip("Drag your center point transform here. If left empty, we'll try to find by name/tag.")]
    public Transform centerPoint;

    [Tooltip("If centerPoint is not assigned, we will try to find a GameObject by this name.")]
    public string centerPointName = "CenterPoint";

    [Header("Camera Placement")]
    public float distanceBehindBall = 10f; // how far behind
    public float height = 8f;              // how high
    public float positionLerp = 10f;       // smooth follow
    public float snapDistance = 8f;        // snap if too far (prevents long catch-up)

    [Header("Look")]
    public bool lookAtCenter = true;       // always look at center
    public bool lookAtBallInstead = false; // optional, if you prefer camera to look at ball

    private Transform cam;
    private bool active;

    public override void Spawned()
    {
        // Only local player controls camera
        if (!Object.HasInputAuthority) return;

        cam = Camera.main != null ? Camera.main.transform : null;
        if (cam == null)
        {
            Debug.LogWarning("[BallCenterCameraFollow] No Camera.main found. Ensure Main Camera is tagged MainCamera.");
            return;
        }

        // Resolve center point if not assigned
        if (centerPoint == null)
        {
            var go = GameObject.Find(centerPointName);
            if (go != null) centerPoint = go.transform;
        }

        if (centerPoint == null)
        {
            Debug.LogWarning($"[BallCenterCameraFollow] Center point not found. Assign it in inspector or name it '{centerPointName}'.");
            return;
        }

        active = true;

        // Snap immediately on spawn
        SnapNow();
    }

    private void LateUpdate()
    {
        if (!active || cam == null || centerPoint == null) return;

        // Direction from center to ball (outward)
        Vector3 outward = (transform.position - centerPoint.position);
        outward.y = 0f;

        if (outward.sqrMagnitude < 0.0001f)
            outward = Vector3.forward;

        outward.Normalize();

        // Camera behind ball (further outward) + height
        Vector3 targetPos = transform.position + outward * distanceBehindBall + Vector3.up * height;

        float dist = Vector3.Distance(cam.position, targetPos);

        if (dist > snapDistance)
        {
            cam.position = targetPos;
        }
        else
        {
            float t = 1f - Mathf.Exp(-positionLerp * Time.deltaTime);
            cam.position = Vector3.Lerp(cam.position, targetPos, t);
        }

        // Rotation: always stable up, no roll
        if (lookAtBallInstead)
        {
            cam.rotation = Quaternion.LookRotation((transform.position - cam.position).normalized, Vector3.up);
        }
        else if (lookAtCenter)
        {
            cam.rotation = Quaternion.LookRotation((centerPoint.position - cam.position).normalized, Vector3.up);
        }
    }

    private void SnapNow()
    {
        Vector3 outward = (transform.position - centerPoint.position);
        outward.y = 0f;

        if (outward.sqrMagnitude < 0.0001f)
            outward = Vector3.forward;

        outward.Normalize();

        cam.position = transform.position + outward * distanceBehindBall + Vector3.up * height;

        if (lookAtBallInstead)
            cam.rotation = Quaternion.LookRotation((transform.position - cam.position).normalized, Vector3.up);
        else
            cam.rotation = Quaternion.LookRotation((centerPoint.position - cam.position).normalized, Vector3.up);
    }
}
