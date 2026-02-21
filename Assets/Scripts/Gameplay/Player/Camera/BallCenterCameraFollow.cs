using Fusion;
using UnityEngine;

/// Local-player-only camera follow for Shared mode.
/// - Attaches to the local player's ball only.
/// - Keeps camera behind the ball using ball forward on XZ.
/// - Exposes planar forward/right basis for camera-relative input.
public class BallCenterCameraFollow : NetworkBehaviour
{
    [Header("Camera Placement")]
    [SerializeField] private float distanceBehindBall = 10f;
    [SerializeField] private float height = 8f;
    [SerializeField] private float positionLerp = 12f;
    [SerializeField] private float rotationLerp = 14f;
    [SerializeField] private float snapDistance = 8f;

    private Transform _cam;
    private bool _active;

    public static BallCenterCameraFollow LocalInstance { get; private set; }

    public override void Spawned()
    {
        // Only local player controls the camera.
        if (!Object.HasInputAuthority)
            return;

        var mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogWarning("[BallCenterCameraFollow] No Camera.main found. Ensure Main Camera is tagged MainCamera.");
            return;
        }

        _cam = mainCam.transform;
        _active = true;
        LocalInstance = this;

        SnapNow();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (LocalInstance == this)
            LocalInstance = null;

        _active = false;
    }

    public bool TryGetPlanarBasis(out Vector3 forward, out Vector3 right)
    {
        forward = Vector3.zero;
        right = Vector3.zero;

        if (!_active || _cam == null)
            return false;

        forward = Vector3.ProjectOnPlane(_cam.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);

        forward.Normalize();
        right = Vector3.Cross(Vector3.up, forward).normalized;
        return true;
    }

    private void LateUpdate()
    {
        if (!_active || _cam == null)
            return;

        Vector3 planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (planarForward.sqrMagnitude < 0.0001f)
            planarForward = Vector3.forward;

        planarForward.Normalize();

        Vector3 targetPos = transform.position - planarForward * distanceBehindBall + Vector3.up * height;
        float dist = Vector3.Distance(_cam.position, targetPos);

        if (dist > snapDistance)
        {
            _cam.position = targetPos;
        }
        else
        {
            float pt = 1f - Mathf.Exp(-positionLerp * Time.deltaTime);
            _cam.position = Vector3.Lerp(_cam.position, targetPos, pt);
        }

        Vector3 lookDir = transform.position - _cam.position;
        if (lookDir.sqrMagnitude < 0.0001f)
            lookDir = planarForward;

        Quaternion targetRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
        float rt = 1f - Mathf.Exp(-rotationLerp * Time.deltaTime);
        _cam.rotation = Quaternion.Slerp(_cam.rotation, targetRot, rt);
    }

    private void SnapNow()
    {
        if (_cam == null)
            return;

        Vector3 planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (planarForward.sqrMagnitude < 0.0001f)
            planarForward = Vector3.forward;

        planarForward.Normalize();

        _cam.position = transform.position - planarForward * distanceBehindBall + Vector3.up * height;
        Vector3 lookDir = transform.position - _cam.position;
        if (lookDir.sqrMagnitude < 0.0001f)
            lookDir = planarForward;

        _cam.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
    }
}
