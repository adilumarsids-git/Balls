using UnityEngine;

// Script used to control behaviour of the car chase camera as well as the ability to switch to the menu camera mode.
public class VehicleCamera : MonoBehaviour {
  public Transform CameraTarget;
  public Vector3 CameraOffset = new Vector3(0f, 3f, -6f);
  public float LerpSpeed = 8f;

  [Tooltip("When in car chase cam mode, this is the max distance the " +
    "camera can be from the target position before it will just snap " +
    "to the target position without lerping. This is so the camera " +
    "does not lerp when the car is reset.")]
  public float MaxLerpDistance = 5f;

  private Transform _camera;
  private Vector3 _initialCameraPosition;
  private Quaternion _initialCameraRotation;

  private bool _chaseCameraActive = false;
  private bool _canSnap = false;

  public void Init() {
    _camera = Camera.main.transform;

    // Save the initial camera position and rotation so that it can be used to lerp to
    // when the chase camera mode is turned off
    _initialCameraPosition = Camera.main.transform.position;
    _initialCameraRotation = Camera.main.transform.rotation;
  }

  public void SetChaseCameraActive(bool value) {
    _chaseCameraActive = value;

    if (!_chaseCameraActive) {
      // Prevent snapping to the target when not using the chase camera.
      // This is so we can smoothly lerp between the modes without snapping.
      _canSnap = false;
    }
  }
  public bool IsChaseCameraActive() {
    return _chaseCameraActive;
  }

  private void LateUpdate() {

    if (!_camera) {
      return;
    }

    // Lerp to a position offset of the car when the chase camera mode is active. 
    // Otherwise, lerp to the original position and rotation the camera was in the scene.
    if (_chaseCameraActive) {

      var targetPos = CameraTarget.TransformPoint(CameraOffset);
      var targetDistance = Vector3.Distance(_camera.position, targetPos);

      // Lerp the camera if its a reasonable distance, otherwise snap the camera to the correct position.
      // This is so that camera will snap and not lerp when the car is teleported via the "Reset" button press.
      if (!_canSnap || targetDistance < MaxLerpDistance) {
        _camera.position = Vector3.Lerp(_camera.position, targetPos, LerpSpeed * Time.deltaTime);
      } else {
        _camera.position = targetPos;
      }

      _camera.LookAt(CameraTarget);

      // If the camera has been close to the target, then snapping can begin again if it has been false.
      // This is to prevent the camera from snapping when animating down to the chase camera view.
      if (targetDistance < MaxLerpDistance) {
        _canSnap = true;
      }

    } else {

      _camera.SetPositionAndRotation(
        Vector3.Lerp(_camera.position, _initialCameraPosition, LerpSpeed * Time.deltaTime),
        Quaternion.Lerp(_camera.rotation, _initialCameraRotation, LerpSpeed * Time.deltaTime)
      );

    }
  }
}