using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Target Tracking")]
    [Tooltip("The target kart to follow. If null, it will dynamically look for a KartController with isPlayer=true.")]
    public KartController target;
    
    [Header("Positioning Settings")]
    public float distance = 4.8f;
    public float height = 1.7f;
    public float lookAtOffset = 0.5f; // Look slightly above the kart center

    [Header("Damping / Smoothness")]
    [Tooltip("How smoothly the camera follows the kart's position.")]
    public float positionSmoothTime = 0.05f;
    [Tooltip("How smoothly the camera rotates to face the kart.")]
    public float rotationSmoothTime = 0.12f;
    [Tooltip("How smoothly the camera responds to the kart's direction changes.")]
    public float directionSmoothTime = 0.4f;

    [Header("Dynamic FOV")]
    public bool useDynamicFOV = true;
    public float minFOV = 60f;
    public float maxFOV = 74f;
    public float fovSpeedThreshold = 22f;

    // Movement smoothing vectors
    private Vector3 positionVelocity;
    private Vector3 smoothForward;
    private Camera cam;
    private Rigidbody targetRb;

    private void Start()
    {
        cam = GetComponent<Camera>();
        FindActivePlayerTarget();
        if (target != null)
        {
            smoothForward = target.transform.forward;
            targetRb = target.GetComponent<Rigidbody>();
        }
    }

    private void LateUpdate()
    {
        // If we don't have a target, or the current target is no longer marked as the player,
        // dynamically search for the new player-controlled kart!
        if (target == null || !target.isPlayer)
        {
            FindActivePlayerTarget();
            if (target == null) return;
        }

        FollowTarget();
    }

    private void FindActivePlayerTarget()
    {
        KartController[] karts = Object.FindObjectsByType<KartController>(FindObjectsSortMode.None);
        foreach (var kart in karts)
        {
            if (kart.isPlayer)
            {
                target = kart;
                targetRb = target.GetComponent<Rigidbody>();
                smoothForward = target.transform.forward;
                break;
            }
        }
    }

    private void FollowTarget()
    {
        if (target == null) return;

        // 1. Smoothly interpolate the tracking direction
        // This is the magic: by smoothing the forward vector, the camera doesn't swing violently on sharp turns or drifts.
        // Instead, the kart elegantly rotates inside the camera's frame, feeling exactly like Mario Kart!
        Vector3 targetForward = target.transform.forward;
        targetForward.y = 0f;
        targetForward.Normalize();

        if (targetForward.sqrMagnitude > 0.001f)
        {
            smoothForward = Vector3.Slerp(smoothForward, targetForward, Time.deltaTime / directionSmoothTime);
        }

        // 2. Determine target position based on smoothed forward direction
        Vector3 targetPosition = target.transform.position - (smoothForward * distance) + (Vector3.up * height);

        // Simple raycast to prevent camera clipping through ground or low scenery
        RaycastHit hit;
        if (Physics.Raycast(target.transform.position + Vector3.up * 0.5f, (targetPosition - target.transform.position).normalized, out hit, distance))
        {
            // If we hit something (except ourselves), pull camera forward
            if (hit.collider.gameObject != target.gameObject && !hit.collider.transform.IsChildOf(target.transform))
            {
                targetPosition = hit.point + hit.normal * 0.2f;
            }
        }

        // Smoothly move the camera's position using SmoothDamp
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref positionVelocity, positionSmoothTime);

        // 3. Determine looking direction
        // We look at the kart plus a small offset above it, and also slightly look ahead of the kart's forward motion
        Vector3 lookAtTarget = target.transform.position + Vector3.up * lookAtOffset;
        
        // Add a slight look-ahead based on speed to help the player navigate curves
        float speed = targetRb != null ? targetRb.linearVelocity.magnitude : 0f;
        lookAtTarget += target.transform.forward * (speed * 0.06f);

        Vector3 targetDirection = (lookAtTarget - transform.position).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);

        // Smoothly interpolate rotation
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime / rotationSmoothTime);

        // 4. Dynamic Field of View
        if (useDynamicFOV && cam != null)
        {
            float targetFOV = Mathf.Lerp(minFOV, maxFOV, speed / fovSpeedThreshold);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * 4f);
        }
    }
}

