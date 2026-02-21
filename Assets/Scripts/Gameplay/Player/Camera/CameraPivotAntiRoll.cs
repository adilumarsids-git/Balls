using UnityEngine;

public class CameraPivotAntiRoll : MonoBehaviour
{
    void LateUpdate()
    {
        // Keep only yaw from parent, remove pitch/roll
        var e = transform.parent.rotation.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, e.y, 0f);
    }
}
