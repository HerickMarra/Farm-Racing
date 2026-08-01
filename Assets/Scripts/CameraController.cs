using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(100)]
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
    public float rotationSmoothTime = 0.08f;
    [Tooltip("How smoothly the camera responds to the kart's direction changes.")]
    public float directionSmoothTime = 0.18f;

    [Header("Dynamic FOV")]
    public bool useDynamicFOV = true;
    public float minFOV = 60f;
    public float maxFOV = 74f;
    public float fovSpeedThreshold = 22f;

    [Header("Camera Tilt")]
    public bool useCameraTilt = true;
    public float maxTiltAngle = 4.0f;
    public float tiltSpeed = 5.0f;
    private float currentTilt = 0f;

    // Cinema mode state variables
    private List<KartController> cinemaKarts = new List<KartController>();
    private int cinemaTargetIndex = 0;
    private float cinemaSwitchTimer = 0f;
    private float cinemaAngleOffset = 0f;
    private bool isCinemaMode = false;

    // Movement smoothing vectors
    private Vector3 positionVelocity;
    private Vector3 smoothForward;
    private Camera cam;
    private Rigidbody targetRb;

    // Cached target transform references (0 GC allocations)
    private Transform cachedTargetTransform;
    private Transform cachedTargetRoot;
    private RaceManager cachedRaceManager;

    // Smooth wall anti-clipping
    private float currentClipOffset = 0f;
    private float clipVelocity = 0f;

    /// <summary>
    /// The actual gameplay Camera that renders the race and follows the player.
    /// Other systems (e.g. world-tracking HUD) should use this instead of Camera.main.
    /// </summary>
    public Camera Cam
    {
        get
        {
            if (cam == null) cam = GetComponent<Camera>();
            return cam;
        }
    }
    private float smoothedSpeed = 0f;
    private Vector3 smoothedVelocityDir = Vector3.forward;
    private bool wasLookingBehind = false;

    // Intro mode variables
    private bool isIntroMode = true;
    private int introWaypointIndex = 0;
    private float introProgress = 0f;
    private WaypointCircuit waypointCircuit;
    private Vector3 introSmoothForward = Vector3.zero;
    private float introRoll = 0f;
    private int introWaypointsVisited = 0;
    private float transitionTimer = 0f;
    private Vector3 transitionStartPos;
    private Quaternion transitionStartRot;

    private void Start()
    {
        cam = GetComponent<Camera>();
        FindActivePlayerTarget();
        if (target != null)
        {
            smoothForward = target.transform.forward;
            targetRb = target.GetComponent<Rigidbody>();
            cachedTargetTransform = target.transform;
            cachedTargetRoot = cachedTargetTransform.root;
        }
        if (waypointCircuit == null)
        {
            waypointCircuit = Object.FindAnyObjectByType<WaypointCircuit>();
        }
        cachedRaceManager = Object.FindAnyObjectByType<RaceManager>();
    }

    private void LateUpdate()
    {
        if (isIntroMode && waypointCircuit != null && waypointCircuit.waypoints != null && waypointCircuit.waypoints.Length > 0)
        {
            UpdateIntroFlyby();
        }
        else if (isCinemaMode)
        {
            UpdateCinemaMode();
        }
        else
        {
            if (target == null || !target.isPlayer)
            {
                FindActivePlayerTarget();
            }

            if (target != null)
            {
                FollowTarget();
            }
        }
    }

    private void UpdateIntroFlyby()
    {
        int W = waypointCircuit.waypoints.Length;
        if (W == 0) return;

        introProgress += Time.deltaTime * 4.2f;
        if (introProgress >= 1f)
        {
            int advanced = Mathf.FloorToInt(introProgress);
            introWaypointIndex += advanced;
            introWaypointsVisited += advanced;
            introProgress = introProgress % 1f;

            if (introWaypointsVisited >= W)
            {
                if (cachedRaceManager == null) cachedRaceManager = Object.FindAnyObjectByType<RaceManager>();
                if (cachedRaceManager != null)
                {
                    cachedRaceManager.StartRaceCountdown();
                }
            }

            introWaypointIndex = introWaypointIndex % W;
        }

        Transform currentWp = waypointCircuit.waypoints[introWaypointIndex];
        Transform nextWp = waypointCircuit.waypoints[(introWaypointIndex + 1) % W];
        if (currentWp == null || nextWp == null) return;

        Vector3 posOnPath = Vector3.Lerp(currentWp.position, nextWp.position, introProgress);
        Vector3 forwardDir = (nextWp.position - currentWp.position).normalized;

        if (introSmoothForward == Vector3.zero)
        {
            introSmoothForward = forwardDir;
        }
        float tIntroForward = 1f - Mathf.Exp(-Time.deltaTime * 3.5f);
        introSmoothForward = Vector3.Slerp(introSmoothForward, forwardDir, tIntroForward);

        Vector3 cameraTargetPos = posOnPath + Vector3.up * 4.5f - introSmoothForward * 9.0f;

        float tIntroPos = 1f - Mathf.Exp(-Time.deltaTime * 3.8f);
        transform.position = Vector3.Lerp(transform.position, cameraTargetPos, tIntroPos);
        
        float turnAngle = Vector3.SignedAngle(introSmoothForward, forwardDir, Vector3.up);
        float targetRoll = Mathf.Clamp(-turnAngle * 2.2f, -28f, 28f);
        float tIntroRoll = 1f - Mathf.Exp(-Time.deltaTime * 5.0f);
        introRoll = Mathf.Lerp(introRoll, targetRoll, tIntroRoll);

        Vector3 lookAtTarget = posOnPath + introSmoothForward * 6.0f + Vector3.up * 1.2f;
        Vector3 targetDirection = (lookAtTarget - transform.position).normalized;
        if (targetDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            targetRotation = targetRotation * Quaternion.Euler(0f, 0f, introRoll);
            float tIntroRot = 1f - Mathf.Exp(-Time.deltaTime * 2.8f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, tIntroRot);
        }
    }

    public void EndIntroMode()
    {
        isIntroMode = false;
        FindActivePlayerTarget();
        if (target != null)
        {
            smoothForward = transform.forward;
            smoothForward.y = 0f;
            smoothForward.Normalize();
            
            positionVelocity = Vector3.zero;
            transitionTimer = 1.6f;
            transitionStartPos = transform.position;
            transitionStartRot = transform.rotation;
        }
    }

    public void StartCinemaMode(List<KartController> allKarts)
    {
        cinemaKarts = new List<KartController>(allKarts);
        isCinemaMode = true;
        cinemaTargetIndex = 0;
        cinemaSwitchTimer = 0f;
        cinemaAngleOffset = 0f;
        if (cinemaKarts.Count > 0)
        {
            target = cinemaKarts[cinemaTargetIndex];
            if (target != null)
            {
                targetRb = target.GetComponent<Rigidbody>();
                cachedTargetTransform = target.transform;
                cachedTargetRoot = cachedTargetTransform.root;
                smoothForward = target.transform.forward;
            }
        }
    }

    private void UpdateCinemaMode()
    {
        if (cinemaKarts == null || cinemaKarts.Count == 0) return;

        cinemaSwitchTimer += Time.deltaTime;
        if (cinemaSwitchTimer >= 4.0f)
        {
            cinemaSwitchTimer = 0f;
            cinemaTargetIndex = (cinemaTargetIndex + 1) % cinemaKarts.Count;
            target = cinemaKarts[cinemaTargetIndex];
            if (target != null)
            {
                targetRb = target.GetComponent<Rigidbody>();
                cachedTargetTransform = target.transform;
                cachedTargetRoot = cachedTargetTransform.root;
                smoothForward = target.transform.forward;
            }
        }

        if (target == null) return;

        cinemaAngleOffset += Time.deltaTime * 20f;

        int style = cinemaTargetIndex % 3;
        Vector3 targetPos = cachedTargetTransform.position;

        if (style == 0)
        {
            Quaternion rotation = Quaternion.Euler(14f, cinemaAngleOffset, 0f);
            Vector3 targetPosition = targetPos + rotation * new Vector3(0f, 0f, -9.2f) + Vector3.up * 1.5f;
            float tCinemaPos1 = 1f - Mathf.Exp(-Time.deltaTime * 3.5f);
            transform.position = Vector3.Lerp(transform.position, targetPosition, tCinemaPos1);
            transform.LookAt(targetPos + Vector3.up * 0.6f);
        }
        else if (style == 1)
        {
            Vector3 targetPosition = targetPos + cachedTargetTransform.forward * 8.8f + Vector3.up * 1.6f;
            float tCinemaPos2 = 1f - Mathf.Exp(-Time.deltaTime * 4f);
            transform.position = Vector3.Lerp(transform.position, targetPosition, tCinemaPos2);
            transform.LookAt(targetPos + Vector3.up * 0.6f);
        }
        else
        {
            FollowTarget();
        }

        float minDistance = 6.8f;
        Vector3 camToTarget = transform.position - targetPos;
        float currentDist = camToTarget.magnitude;
        if (currentDist < minDistance)
        {
            transform.position = targetPos + camToTarget.normalized * minDistance;
        }
    }

    private void FindActivePlayerTarget()
    {
        var karts = KartController.ActiveKarts;
        for (int i = 0; i < karts.Count; i++)
        {
            KartController kart = karts[i];
            if (kart != null && kart.isPlayer)
            {
                target = kart;
                targetRb = target.GetComponent<Rigidbody>();
                cachedTargetTransform = target.transform;
                cachedTargetRoot = cachedTargetTransform.root;
                smoothForward = cachedTargetTransform.forward;
                break;
            }
        }
    }

    private void FollowTarget()
    {
        if (target == null || cachedTargetTransform == null) return;

        Vector3 targetForward = cachedTargetTransform.forward;
        Vector3 targetPos = cachedTargetTransform.position;
        
        bool isMovingForward = targetRb != null && Vector3.Dot(targetRb.linearVelocity, targetForward) >= 0f;
        
        if (targetRb != null && isMovingForward && targetRb.linearVelocity.sqrMagnitude > 1.0f)
        {
            Vector3 rawVelDir = targetRb.linearVelocity;
            rawVelDir.y = 0f;
            
            if (rawVelDir.sqrMagnitude > 0.001f)
            {
                rawVelDir.Normalize();
                float tVelSmooth = 1f - Mathf.Exp(-Time.deltaTime * 12f);
                smoothedVelocityDir = Vector3.Slerp(smoothedVelocityDir, rawVelDir, tVelSmooth).normalized;

                float blendFactor = target.IsDrifting ? 0.85f : 0.60f;
                float speedPercent = Mathf.Clamp01(smoothedSpeed / 8f);
                targetForward = Vector3.Slerp(targetForward, smoothedVelocityDir, speedPercent * blendFactor).normalized;
            }
        }
        else
        {
            targetForward.y = 0f;
            targetForward.Normalize();
            smoothedVelocityDir = targetForward;
        }

        if (targetForward.sqrMagnitude > 0.001f)
        {
            float activeDirectionSmooth = directionSmoothTime;
            if (target.IsDrifting)
            {
                activeDirectionSmooth *= 0.5f;
            }
            
            float tDir = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(activeDirectionSmooth, 0.01f));
            smoothForward = Vector3.Slerp(smoothForward, targetForward, tDir);
        }

        float dirMultiplier = -1f;
        float lookAheadDirection = 1f;
        bool lookBehind = false;
        
        if (Keyboard.current != null)
        {
            lookBehind = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
        }

        if (lookBehind)
        {
            dirMultiplier = 1f;
            lookAheadDirection = -1f;
        }

        // Raycast anti-clipping against scenery walls (0 GC allocations)
        Vector3 desiredCameraPos = targetPos + (smoothForward * distance * dirMultiplier) + (Vector3.up * height);
        Vector3 rayOrigin = targetPos + Vector3.up * 0.5f;
        Vector3 rayDir = (desiredCameraPos - rayOrigin).normalized;
        float rayDist = distance;

        float targetClipOffset = 0f;
        RaycastHit hit;
        if (Physics.Raycast(rayOrigin, rayDir, out hit, rayDist, ~0, QueryTriggerInteraction.Ignore))
        {
            Transform hitT = hit.collider.transform;
            if (hitT != cachedTargetTransform && hitT.root != cachedTargetRoot)
            {
                // Smoothly pull camera forward when hitting walls instead of snapping instantly
                targetClipOffset = distance - hit.distance + 0.25f;
            }
        }

        // Smoothly damp the wall offset to eliminate camera wall-clipping jitters
        currentClipOffset = Mathf.SmoothDamp(currentClipOffset, targetClipOffset, ref clipVelocity, 0.06f);
        float effectiveDistance = Mathf.Max(0.5f, distance - currentClipOffset);

        Vector3 targetPosition = targetPos + (smoothForward * effectiveDistance * dirMultiplier) + (Vector3.up * height);

        // Smooth look-at target position
        Vector3 lookAtTarget = targetPos + Vector3.up * lookAtOffset;
        
        float rawSpeed = targetRb != null ? targetRb.linearVelocity.magnitude : 0f;
        float tSpeed = 1f - Mathf.Exp(-Time.deltaTime * 6.0f);
        smoothedSpeed = Mathf.Lerp(smoothedSpeed, rawSpeed, tSpeed);

        lookAtTarget += smoothForward * (smoothedSpeed * 0.06f * lookAheadDirection);

        if (transitionTimer > 0f)
        {
            transitionTimer -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(transitionTimer / 1.6f);
            float tCurve = 1f - Mathf.Pow(1f - t, 3f);
            
            Vector3 targetDirection = (lookAtTarget - transform.position).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            
            if (useCameraTilt)
            {
                float steerValue = target.SteeringInput;
                float targetTilt = -steerValue * maxTiltAngle;
                currentTilt = Mathf.Lerp(currentTilt, targetTilt, 1f - Mathf.Exp(-Time.deltaTime * tiltSpeed));
                targetRotation = targetRotation * Quaternion.Euler(0f, 0f, currentTilt);
            }

            transform.position = Vector3.Lerp(transitionStartPos, targetPosition, tCurve);
            transform.rotation = Quaternion.Slerp(transitionStartRot, targetRotation, tCurve);
            
            positionVelocity = Vector3.zero;
        }
        else
        {
            if (lookBehind != wasLookingBehind)
            {
                transform.position = targetPosition;
                Vector3 instantDirection = (lookAtTarget - targetPosition).normalized;
                transform.rotation = Quaternion.LookRotation(instantDirection);
                positionVelocity = Vector3.zero;
                wasLookingBehind = lookBehind;
            }
            else
            {
                // Smooth position
                transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref positionVelocity, positionSmoothTime);
                
                // Smooth rotation
                Vector3 targetDirection = (lookAtTarget - transform.position).normalized;
                if (targetDirection.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
                    
                    if (useCameraTilt)
                    {
                        float steerValue = target.SteeringInput;
                        float targetTilt = -steerValue * maxTiltAngle;
                        currentTilt = Mathf.Lerp(currentTilt, targetTilt, 1f - Mathf.Exp(-Time.deltaTime * tiltSpeed));
                        targetRotation = targetRotation * Quaternion.Euler(0f, 0f, currentTilt);
                    }

                    float activeRotationSmooth = rotationSmoothTime;
                    if (target.IsDrifting)
                    {
                        activeRotationSmooth *= 0.6f;
                    }
                    float tRotFollow = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(activeRotationSmooth, 0.01f));
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, tRotFollow);
                }
            }
        }
        

        // Dynamic Field of View
        if (useDynamicFOV && cam != null)
        {
            float currentMin = minFOV;
            float currentMax = maxFOV;
            if (target.IsBoosting)
            {
                currentMin += 5f;
                currentMax += 12f;
            }
            else if (target.IsDrifting)
            {
                currentMin += 3f;
                currentMax += 7f;
            }
            float targetFOV = Mathf.Lerp(currentMin, currentMax, smoothedSpeed / fovSpeedThreshold);
            float tFOV = 1f - Mathf.Exp(-Time.deltaTime * 4f);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, tFOV);
        }
    }
}
