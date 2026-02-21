using Fusion;
using UnityEngine;

public class DistanceBasedAuthority : NetworkBehaviour
{
    [SerializeField, Tooltip("Seconds between authority requests. For pickups use 0.05–0.15.")]
    private float requestInterval = 0.10f;

    private double _lastAuthRequest;

    private void FixedUpdate()
    {
        if (!Object || Runner == null) return;

        // If we already have StateAuthority, stop.
        if (Object.HasStateAuthority) return;

        // Rate limit requests
        if ((_lastAuthRequest + requestInterval) > Time.realtimeSinceStartupAsDouble) return;

        // Find current authority player object
        var authorityPlayerObject = Runner.GetPlayerObject(Object.StateAuthority);

        // If none exists, master should claim it so it has *some* authority
        if (authorityPlayerObject == null || !authorityPlayerObject.IsValid)
        {
            if (Runner.IsSharedModeMasterClient)
            {
                Object.RequestStateAuthority();
                _lastAuthRequest = Time.realtimeSinceStartupAsDouble;
            }
            return;
        }

        // Find local player object
        var localPlayerObject = Runner.GetPlayerObject(Runner.LocalPlayer);
        if (localPlayerObject == null || !localPlayerObject.IsValid) return;

        float authDist2 = (transform.position - authorityPlayerObject.transform.position).sqrMagnitude;
        float localDist2 = (transform.position - localPlayerObject.transform.position).sqrMagnitude;

        if (localDist2 < authDist2)
        {
            Object.RequestStateAuthority();
            _lastAuthRequest = Time.realtimeSinceStartupAsDouble;
        }
    }
}
