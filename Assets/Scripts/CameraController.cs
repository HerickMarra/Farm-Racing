using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

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
    public float positionSmoothTime = 0.08f;
    [Tooltip("How smoothly the camera rotates to face the kart.")]
    public float rotationSmoothTime = 0.15f;
    [Tooltip("How smoothly the camera responds to the kart's direction changes.")]
    public float directionSmoothTime = 0.4f;

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

    [Header("Screen Shake")]
    private float shakeDuration = 0f;
    private float shakeIntensity = 0f;
    private float shakeDecay = 5f;

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
        }
        if (waypointCircuit == null)
        {
            waypointCircuit = Object.FindAnyObjectByType<WaypointCircuit>();
        }
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
            // If we don't have a target, or the current target is no longer marked as the player,
            // dynamically search for the new player-controlled kart!
            if (target == null || !target.isPlayer)
            {
                FindActivePlayerTarget();
            }

            if (target != null)
            {
                FollowTarget();
            }
        }

        ApplyScreenShake();
    }

    public void TriggerShake(float intensity, float duration, float decay = 5f)
    {
        shakeIntensity = intensity;
        shakeDuration = duration;
        shakeDecay = decay;
    }

    private void ApplyScreenShake()
    {
        if (shakeDuration > 0f)
        {
            transform.position += Random.insideUnitSphere * shakeIntensity;
            shakeDuration -= Time.deltaTime;

            if (shakeDuration <= 0f)
            {
                shakeDuration = 0f;
                shakeIntensity = 0f;
            }
        }
    }

    private void UpdateIntroFlyby()
    {
        int W = waypointCircuit.waypoints.Length;
        if (W == 0) return;

        Transform currentWp = waypointCircuit.waypoints[introWaypointIndex];
        Transform nextWp = waypointCircuit.waypoints[(introWaypointIndex + 1) % W];
        if (currentWp == null || nextWp == null) return;

        // Progress camera along the track waypoints (significantly faster, drone style speed)
        introProgress += Time.deltaTime * 4.2f;
        if (introProgress >= 1f)
        {
            int advanced = Mathf.FloorToInt(introProgress);
            introWaypointIndex += advanced;
            introWaypointsVisited += advanced;
            introProgress = introProgress % 1f; // Keep fractional overshoot to avoid position jumps

            // Automatically start the countdown if the intro camera completes a full lap of the circuit
            if (introWaypointsVisited >= W)
            {
                RaceManager raceManager = Object.FindAnyObjectByType<RaceManager>();
                if (raceManager != null)
                {
                    raceManager.StartRaceCountdown();
                }
            }

            introWaypointIndex = introWaypointIndex % W;
        }

        Vector3 posOnPath = Vector3.Lerp(currentWp.position, nextWp.position, introProgress);
        Vector3 forwardDir = (nextWp.position - currentWp.position).normalized;

        // Smoothly interpolate forward direction (gentle Slerp to eliminate any camera jerk/tranco)
        if (introSmoothForward == Vector3.zero)
        {
            introSmoothForward = forwardDir;
        }
        float tIntroForward = 1f - Mathf.Exp(-Time.deltaTime * 3.5f);
        introSmoothForward = Vector3.Slerp(introSmoothForward, forwardDir, tIntroForward);

        // Position camera behind lookahead point and elevated using the smoothed direction vector
        Vector3 cameraTargetPos = posOnPath + Vector3.up * 4.5f - introSmoothForward * 9.0f;

        // Smoothly position the camera (adjusted factor for responsive but smooth drone-like follow style)
        float tIntroPos = 1f - Mathf.Exp(-Time.deltaTime * 3.8f);
        transform.position = Vector3.Lerp(transform.position, cameraTargetPos, tIntroPos);
        
        // Calculate banking/roll angle based on steering curvature (like a racing drone tilting into curves)
        float turnAngle = Vector3.SignedAngle(introSmoothForward, forwardDir, Vector3.up);
        float targetRoll = Mathf.Clamp(-turnAngle * 2.2f, -28f, 28f); // Bank up to 28 degrees
        float tIntroRoll = 1f - Mathf.Exp(-Time.deltaTime * 5.0f);
        introRoll = Mathf.Lerp(introRoll, targetRoll, tIntroRoll);

        // Cinematographically rotate/look at target using Slerp and apply banking rotation
        Vector3 lookAtTarget = posOnPath + introSmoothForward * 6.0f + Vector3.up * 1.2f;
        Vector3 targetDirection = (lookAtTarget - transform.position).normalized;
        if (targetDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            // Apply roll rotation (Z-axis tilting) relative to local target alignment
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
            // Do NOT snap position and rotation instantly anymore.
            // Initialize the smoothForward vector matching the current camera looking direction
            smoothForward = transform.forward;
            smoothForward.y = 0f;
            smoothForward.Normalize();
            
            positionVelocity = Vector3.zero;
            transitionTimer = 1.6f; // Start a faster 1.6s smooth transition glide
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
            // Target the next valid kart
            cinemaTargetIndex = (cinemaTargetIndex + 1) % cinemaKarts.Count;
            target = cinemaKarts[cinemaTargetIndex];
            if (target != null)
            {
                targetRb = target.GetComponent<Rigidbody>();
                smoothForward = target.transform.forward;
            }
        }

        if (target == null) return;

        cinemaAngleOffset += Time.deltaTime * 20f; // Rotate camera slowly

        // Alternate camera style: 0 = slow orbital rotation, 1 = head-on track facing, 2 = standard follow
        int style = cinemaTargetIndex % 3;

        if (style == 0)
        {
            // Orbital rotation shot - Increased distance to 9.2f
            Quaternion rotation = Quaternion.Euler(14f, cinemaAngleOffset, 0f);
            Vector3 targetPosition = target.transform.position + rotation * new Vector3(0f, 0f, -9.2f) + Vector3.up * 1.5f;
            float tCinemaPos1 = 1f - Mathf.Exp(-Time.deltaTime * 3.5f);
            transform.position = Vector3.Lerp(transform.position, targetPosition, tCinemaPos1);
            transform.LookAt(target.transform.position + Vector3.up * 0.6f);
        }
        else if (style == 1)
        {
            // Front track shot (looks back at the kart's front grilles as it drives) - Increased distance to 8.8f
            Vector3 targetPosition = target.transform.position + target.transform.forward * 8.8f + Vector3.up * 1.6f;
            float tCinemaPos2 = 1f - Mathf.Exp(-Time.deltaTime * 4f);
            transform.position = Vector3.Lerp(transform.position, targetPosition, tCinemaPos2);
            transform.LookAt(target.transform.position + Vector3.up * 0.6f);
        }
        else
        {
            // Cinematic follow
            FollowTarget();
        }

        // Hard guarantee: Camera never gets closer than 6.8 meters to the target kart in cinema mode
        float minDistance = 6.8f;
        Vector3 camToTarget = transform.position - target.transform.position;
        float currentDist = camToTarget.magnitude;
        if (currentDist < minDistance)
        {
            transform.position = target.transform.position + camToTarget.normalized * minDistance;
        }
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
        Vector3 targetForward = target.transform.forward;
        targetForward.y = 0f;
        targetForward.Normalize();

        if (targetForward.sqrMagnitude > 0.001f)
        {
            float tDir = 1f - Mathf.Exp(-Time.deltaTime / directionSmoothTime);
            smoothForward = Vector3.Slerp(smoothForward, targetForward, tDir);
        }

        // Check look-behind input via Ctrl keys
        float dirMultiplier = -1f;
        float lookAheadDirection = 1f;
        bool lookBehind = false;
        
        if (Keyboard.current != null)
        {
            lookBehind = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
        }

        if (lookBehind)
        {
            dirMultiplier = 1f;       // Inverts follow position (places camera in front)
            lookAheadDirection = -1f; // Inverts curve look-ahead offset direction
        }

        // 2. Determine target position based on direction multiplier
        Vector3 targetPosition = target.transform.position + (smoothForward * distance * dirMultiplier) + (Vector3.up * height);

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

        // 3. Determine looking direction
        Vector3 lookAtTarget = target.transform.position + Vector3.up * lookAtOffset;
        
        // Add a slight look-ahead based on speed to help the player navigate curves
        float speed = targetRb != null ? targetRb.linearVelocity.magnitude : 0f;
        lookAtTarget += smoothForward * (speed * 0.06f * lookAheadDirection);

        // 4. Position and orient camera with either smooth transition ease-out or standard follow
        if (transitionTimer > 0f)
        {
            transitionTimer -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(transitionTimer / 1.6f);
            
            // Cubic Ease-Out curve: starts fast to cover distance, then decelerates to 0 speed smoothly at the end
            float tCurve = 1f - Mathf.Pow(1f - t, 3f);
            
            Vector3 targetDirection = (lookAtTarget - transform.position).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            
            if (useCameraTilt && target != null)
            {
                float steerValue = target.SteeringInput;
                float targetTilt = -steerValue * maxTiltAngle;
                currentTilt = Mathf.Lerp(currentTilt, targetTilt, 1f - Mathf.Exp(-Time.deltaTime * tiltSpeed));
                targetRotation = targetRotation * Quaternion.Euler(0f, 0f, currentTilt);
            }

            transform.position = Vector3.Lerp(transitionStartPos, targetPosition, tCurve);
            transform.rotation = Quaternion.Slerp(transitionStartRot, targetRotation, tCurve);
            
            positionVelocity = Vector3.zero; // Clear standard follow velocity during transition
        }
        else
        {
            // Smoothly move position and rotation, or snap instantly on state change
            if (lookBehind != wasLookingBehind)
            {
                // Snap position instantly
                transform.position = targetPosition;
                
                // Calculate instant look direction relative to the snapped position
                Vector3 instantDirection = (lookAtTarget - targetPosition).normalized;
                transform.rotation = Quaternion.LookRotation(instantDirection);
                
                positionVelocity = Vector3.zero; // Reset velocity
                wasLookingBehind = lookBehind;
            }
            else
            {
                // Smoothly move the camera's position using SmoothDamp
                transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref positionVelocity, positionSmoothTime);
                
                // Smoothly interpolate rotation
                Vector3 targetDirection = (lookAtTarget - transform.position).normalized;
                Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
                
                if (useCameraTilt && target != null)
                {
                    float steerValue = target.SteeringInput;
                    float targetTilt = -steerValue * maxTiltAngle;
                    currentTilt = Mathf.Lerp(currentTilt, targetTilt, 1f - Mathf.Exp(-Time.deltaTime * tiltSpeed));
                    targetRotation = targetRotation * Quaternion.Euler(0f, 0f, currentTilt);
                }

                float tRotFollow = 1f - Mathf.Exp(-Time.deltaTime / rotationSmoothTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, tRotFollow);
            }
        }

        // 4. Dynamic Field of View
        if (useDynamicFOV && cam != null)
        {
            float currentMin = minFOV;
            float currentMax = maxFOV;
            if (target != null && target.IsBoosting)
            {
                currentMin += 5f;
                currentMax += 12f; // Expand dynamic range during boost
            }
            float targetFOV = Mathf.Lerp(currentMin, currentMax, speed / fovSpeedThreshold);
            float tFOV = 1f - Mathf.Exp(-Time.deltaTime * 4f);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, tFOV);
        }
    }
}

