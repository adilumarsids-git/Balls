using Fusion;
using UnityEngine;

/// Stable camera follow for a rolling ball:
/// - Offset is WORLD SPACE (does not rotate with the ball)
/// - Rotation uses a stable world up (no roll)
/// - Optional fixed angle mode (recommended for top-down arena)
public class BallCamera : NetworkBehaviour
{
    [Header("Follow Target")]
    public Vector3 WorldOffset = new Vector3(0f, 12f, -8f);
    public float PositionLerpSpeed = 10f;

    [Tooltip("If farther than this from target pos, snap instead of lerp.")]
    public float SnapDistance = 6f;

    [Header("Rotation")]
    [Tooltip("If true, camera looks at the ball (stable world up). If false, uses fixed rotation.")]
    public bool UseLookAt = true;

    [Tooltip("Used when UseLookAt = false")]
    public Vector3 FixedEulerAngles = new Vector3(60f, 0f, 0f);

    public float RotationLerpSpeed = 10f;

    private Transform _cam;
    private bool _active;

    public override void Spawned()
    {
        if (!Object.HasInputAuthority)
            return;

        var mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogWarning("[BallCamera] No Camera.main found. Tag your scene camera as MainCamera.");
            return;
        }

        _cam = mainCam.transform;
        _active = true;

        // snap once on spawn
        SnapNow();
    }

    private void LateUpdate()
    {
        if (!_active || _cam == null)
            return;

        // ✅ WORLD SPACE offset (does not rotate with the ball)
        Vector3 targetPos = transform.position + WorldOffset;

        float dist = Vector3.Distance(_cam.position, targetPos);

        // Position
        if (dist > SnapDistance)
        {
            _cam.position = targetPos;
        }
        else
        {
            float t = 1f - Mathf.Exp(-PositionLerpSpeed * Time.deltaTime);
            _cam.position = Vector3.Lerp(_cam.position, targetPos, t);
        }

        // Rotation
        Quaternion targetRot;

        if (UseLookAt)
        {
            // ✅ Stable up vector prevents roll
            Vector3 dir = (transform.position - _cam.position);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;

            targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);

            // Optional: clamp roll hard to 0 (extra safety)
            Vector3 e = targetRot.eulerAngles;
            targetRot = Quaternion.Euler(e.x, e.y, 0f);
        }
        else
        {
            targetRot = Quaternion.Euler(FixedEulerAngles);
        }

        float rt = 1f - Mathf.Exp(-RotationLerpSpeed * Time.deltaTime);
        _cam.rotation = Quaternion.Slerp(_cam.rotation, targetRot, rt);
    }

    private void SnapNow()
    {
        if (_cam == null) return;

        _cam.position = transform.position + WorldOffset;

        if (UseLookAt)
        {
            Vector3 dir = (transform.position - _cam.position);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            Vector3 e = rot.eulerAngles;
            _cam.rotation = Quaternion.Euler(e.x, e.y, 0f);
        }
        else
        {
            _cam.rotation = Quaternion.Euler(FixedEulerAngles);
        }
    }
}
