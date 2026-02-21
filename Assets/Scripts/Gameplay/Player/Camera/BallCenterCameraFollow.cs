using Fusion;
using UnityEngine;

/// Local-player-only camera follow for Shared mode.
/// - Camera follows ball position through a separate CameraTarget pivot.
/// - Camera never inherits ball roll/pitch rotation.
/// - Exposes planar forward/right basis for camera-relative movement input.
public class BallCenterCameraFollow : NetworkBehaviour
{
    [Header("Target Pivot")]
    [Tooltip("Optional external pivot. If not assigned, one is created at runtime (not parented to ball).")]
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private Vector3 targetOffset = Vector3.zero;

    [Header("Camera Follow")]
    [Tooltip("Camera offset from CameraTarget in pivot space (not ball space).")]
    [SerializeField] private Vector3 cameraLocalOffset = new Vector3(0f, 8f, -10f);
    [SerializeField] private float targetLerp = 16f;
    [SerializeField] private float positionLerp = 12f;
    [SerializeField] private float snapDistance = 10f;

    [Header("Camera Rotation")]
    [Tooltip("Keep camera rotation fixed and stable. Disable to use LookAt target with world-up.")]
    [SerializeField] private bool useFixedRotation = true;
    [SerializeField] private Vector3 fixedEulerAngles = new Vector3(25f, 0f, 0f);
    [SerializeField] private float rotationLerp = 14f;

    private Transform _cam;
    private bool _active;
    private bool _createdRuntimeTarget;
    private Quaternion _pivotYaw;

    public static BallCenterCameraFollow LocalInstance { get; private set; }

    public override void Spawned()
    {
        if (!Object.HasInputAuthority)
            return;

        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogWarning("[BallCenterCameraFollow] No Camera.main found. Ensure Main Camera is tagged MainCamera.");
            return;
        }

        _cam = mainCam.transform;
        EnsureCameraTarget();

        // Stable yaw is derived once from existing camera orientation.
        _pivotYaw = Quaternion.Euler(0f, _cam.eulerAngles.y, 0f);

        _active = true;
        LocalInstance = this;

        SnapNow();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (LocalInstance == this)
            LocalInstance = null;

        _active = false;

        if (_createdRuntimeTarget && cameraTarget != null)
        {
            Destroy(cameraTarget.gameObject);
            cameraTarget = null;
            _createdRuntimeTarget = false;
        }
    }

    public bool TryGetPlanarBasis(out Vector3 forward, out Vector3 right)
    {
        forward = Vector3.zero;
        right = Vector3.zero;

        if (!_active || _cam == null)
            return false;

        forward = Vector3.ProjectOnPlane(_cam.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.ProjectOnPlane(_pivotYaw * Vector3.forward, Vector3.up);

        forward.Normalize();
        right = Vector3.Cross(Vector3.up, forward).normalized;
        return true;
    }

    private void LateUpdate()
    {
        if (!_active || _cam == null || cameraTarget == null)
            return;

        Vector3 desiredTargetPos = transform.position + targetOffset;
        float targetT = 1f - Mathf.Exp(-targetLerp * Time.deltaTime);
        cameraTarget.position = Vector3.Lerp(cameraTarget.position, desiredTargetPos, targetT);

        Vector3 desiredCamPos = cameraTarget.position + (_pivotYaw * cameraLocalOffset);
        float dist = Vector3.Distance(_cam.position, desiredCamPos);

        if (dist > snapDistance)
        {
            _cam.position = desiredCamPos;
        }
        else
        {
            float posT = 1f - Mathf.Exp(-positionLerp * Time.deltaTime);
            _cam.position = Vector3.Lerp(_cam.position, desiredCamPos, posT);
        }

        Quaternion targetRot = ResolveCameraRotation();
        float rotT = 1f - Mathf.Exp(-rotationLerp * Time.deltaTime);
        _cam.rotation = Quaternion.Slerp(_cam.rotation, targetRot, rotT);
    }

    private void SnapNow()
    {
        if (_cam == null || cameraTarget == null)
            return;

        cameraTarget.position = transform.position + targetOffset;
        _cam.position = cameraTarget.position + (_pivotYaw * cameraLocalOffset);
        _cam.rotation = ResolveCameraRotation();
    }

    private Quaternion ResolveCameraRotation()
    {
        if (useFixedRotation)
        {
            float yaw = _pivotYaw.eulerAngles.y + fixedEulerAngles.y;
            return Quaternion.Euler(fixedEulerAngles.x, yaw, fixedEulerAngles.z);
        }

        Vector3 lookDir = cameraTarget.position - _cam.position;
        if (lookDir.sqrMagnitude < 0.0001f)
            lookDir = _pivotYaw * Vector3.forward;

        return Quaternion.LookRotation(lookDir.normalized, Vector3.up);
    }

    private void EnsureCameraTarget()
    {
        if (cameraTarget != null)
            return;

        GameObject go = new GameObject($"{name}_CameraTarget");
        cameraTarget = go.transform;
        cameraTarget.SetParent(null, true); // must not inherit ball rotation
        _createdRuntimeTarget = true;
    }
}
