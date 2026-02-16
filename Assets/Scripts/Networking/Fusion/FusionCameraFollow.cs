using UnityEngine;
using Fusion;

public class FusionCameraFollow : MonoBehaviour
{
    [Header("Follow Settings")]
    public Vector3 offset = new Vector3(0, 6, -8);
    public float positionSmooth = 8f;

    [Header("Rotation Settings")]
    public bool useAutoLookAt = true;
    public float rotationSmooth = 12f;

    [Tooltip("Used only if Auto LookAt is OFF")]
    public Vector3 manualRotationEuler = new Vector3(20f, 0f, 0f);

    private Transform target;

    void LateUpdate()
    {
        if (target == null)
        {
            FindLocalPlayer();
            if (target == null) return;
        }

        // Follow Position
        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            positionSmooth * Time.deltaTime
        );

        // Rotation
        if (useAutoLookAt)
        {
            Quaternion lookRotation = Quaternion.LookRotation(
                target.position - transform.position
            );

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                lookRotation,
                rotationSmooth * Time.deltaTime
            );
        }
        else
        {
            // Manual rotation from Inspector
            transform.rotation = Quaternion.Euler(manualRotationEuler);
        }
    }

    void FindLocalPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        foreach (GameObject player in players)
        {
            NetworkObject netObj = player.GetComponent<NetworkObject>();

            if (netObj != null && netObj.HasInputAuthority)
            {
                target = player.transform;
                break;
            }
        }
    }
}
