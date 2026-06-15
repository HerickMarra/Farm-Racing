using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class KartController : MonoBehaviour
{
    [Header("Control Settings")]
    [Tooltip("If true, the player controls this kart. If false, the AI controls it.")]
    public bool isPlayer = false;

    [Header("Movement Stats")]
    public float maxSpeed = 22f;
    public float acceleration = 12f;
    public float deceleration = 8f;
    public float reverseSpeed = 8f;
    public float steeringSpeed = 260f;
    
    [Range(0f, 1f)]
    [Tooltip("How much the kart resists sliding sideways. 1 = on rails, 0 = pure ice.")]
    public float normalGrip = 0.85f;
    [Range(0f, 1f)]
    public float driftGrip = 0.15f;
    public float gravityForce = 35f;

    [Header("Drift Settings")]
    public float driftSteerMultiplier = 1.0f;
    [Tooltip("Cooldown between drift hops to prevent rapid jumping.")]
    public float hopCooldown = 1.0f;

    public enum AIDifficulty { Facil, Medio, Dificil, Adaptavel, Competitivo }

    [Header("AI Settings")]
    [Tooltip("Nivel de dificuldade da IA.")]
    public AIDifficulty aiDifficulty = AIDifficulty.Medio;
    public WaypointCircuit waypointCircuit;
    public float waypointThreshold = 10f;
    [Range(0f, 1f)]
    [Tooltip("How much the AI slows down in sharp turns.")]
    public float aiSpeedAdaptation = 0.6f;

    [Header("Juice & Responsiveness")]
    [Tooltip("How fast the steering responds to input. Lower values = smoother/heavier feel, higher = faster/snappier.")]
    public float steeringDamping = 20f;
    [Tooltip("How fast the throttle/acceleration responds to input. Adds weight to the kart.")]
    public float throttleDamping = 8f;
    [Tooltip("Centrifugal roll angle when making turns.")]
    public float bodyLeanAmount = 15f;
    [Tooltip("How high the kart jumps when initiating a drift.")]
    public float driftHopForce = 9.5f;

    [Header("Visuals (Optional)")]
    public Transform frontLeftWheel;
    public Transform frontRightWheel;
    public Transform rearLeftWheel;
    public Transform rearRightWheel;
    public float wheelSpinSpeed = 150f;
    public float maxWheelTurnAngle = 32f;

    [Header("Visual Suspension")]
    public bool useVisualSuspension = true;
    public float wheelRadius = 0.30f;
    public float suspensionRestDistance = 0.15f; // Drop offset in the air
    public float suspensionTravel = 0.12f; // Max compression/extension from rest
    public float suspensionDamping = 16f; // Spring return rate

    [Header("Respawn Settings")]
    public float aiMaxStuckTime = 4.5f;

    // Race Tracking
    [HideInInspector] public bool controlsEnabled = true;
    private int currentLap = 1;
    public int CurrentLap
    {
        get => currentLap;
        set => currentLap = value;
    }
    [HideInInspector] public int currentPosition = 1;

    // Internal physics variables
    private Rigidbody rb;
    private float throttleInput;
    private float steeringInput;
    private bool isDrifting;
    private bool wasDrifting;
    private int currentWaypointIndex = 0;
    private int lastClosestIdx = -1;
    private bool isGrounded;
    private float currentSpeed = 0f;
    private Vector3 groundNormal = Vector3.up;
    private float driftHopCooldownTimer = 0f;

    // Advanced AI decision variables
    private float aiStuckTimer = 0f;
    private bool aiIsReversing = false;
    private float aiReverseDuration = 0f;
    private float aiOvertakeSideOffset = 0f;
    private float aiOvertakeTimer = 0f;
    private float aiOvertakeDirection = 1f; // 1 = right, -1 = left
    private float aiWaypointTimeoutTimer = 0f;
    private int aiReverseCount = 0;
    private Vector3 lastStuckCheckPosition;
    private float stuckPositionTimer = 0f;
    private float accumulatedStuckTime = 0f;

    // Cache variables
    private KartController playerKartCached;
    private float playerCacheTimer = 0f;

    // Drift Boost variables
    private float driftDuration = 0f;
    private float activeBoostTimer = 0f;
    private float activeBoostMultiplier = 1f;

    // Visual Drift variables
    private float driftDirection = 0f; // -1 = Left, 1 = Right
    private float driftYawOffset = 0f;
    private float currentGripValue = 0.96f;
    private Vector3 smoothedGroundNormal = Vector3.up;

    // Visual Jump/Stunt variables
    private bool jumpJustPressed = false;
    private bool hasStuntPerformed = false;
    private float stuntSpinTime = 0f;

    // Slipstream (Vácuo) variables
    private float slipstreamTimer = 0f;
    private bool isDraftingActive = false;

    // Respawn variables
    private float respawnHoldTimer = 0f;

    // Smoothed input state
    private float smoothedSteeringInput = 0f;
    private float smoothedThrottleInput = 0f;
    private Transform bodyTransform;

    // Initial local rotations and positions to support suspension and orientations
    private Quaternion flInitialRot;
    private Quaternion frInitialRot;
    private Quaternion rlInitialRot;
    private Quaternion rrInitialRot;
    private Vector3 flInitialPos;
    private Vector3 frInitialPos;
    private Vector3 rlInitialPos;
    private Vector3 rrInitialPos;
    private float cumulativeRollAngle = 0f;

    // Input Actions (New Input System)
    private InputAction moveAction;
    private InputAction driftAction;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        // Ensure Rigidbody is configured for arcade racing
        rb.useGravity = true;
        rb.linearDamping = 0.1f;
        rb.angularDamping = 1.5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate; // CRITICAL for smooth camera tracking without visual stutters/teleports!
        
        // Freeze all physical rotations so the sphere collider acts as a stable sliding point,
        // letting our script handle all slope alignment and steering rotations without physics rolling the kart.
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        // Try to auto-find WaypointCircuit if not assigned
        if (waypointCircuit == null)
        {
            waypointCircuit = Object.FindAnyObjectByType<WaypointCircuit>();
        }

        // Find closest waypoint on start to prevent backtracking
        if (waypointCircuit != null && waypointCircuit.waypoints != null && waypointCircuit.waypoints.Length > 0)
        {
            float closestDist = float.MaxValue;
            int closestIdx = 0;
            for (int i = 0; i < waypointCircuit.waypoints.Length; i++)
            {
                if (waypointCircuit.waypoints[i] == null) continue;
                float d = Vector3.Distance(transform.position, waypointCircuit.waypoints[i].position);
                if (d < closestDist)
                {
                    closestDist = d;
                    closestIdx = i;
                }
            }
            // Target the waypoint immediately after the closest one to ensure we drive forward
            currentWaypointIndex = (closestIdx + 1) % waypointCircuit.waypoints.Length;
        }

        // Always enable the Player action map in project-wide actions
        if (InputSystem.actions != null)
        {
            var playerMap = InputSystem.actions.FindActionMap("Player");
            if (playerMap != null)
            {
                playerMap.Enable();
                resultLog("Enabled Player Input Action Map.");
            }
            
            moveAction = InputSystem.actions.FindAction("Player/Move");
            driftAction = InputSystem.actions.FindAction("Player/Jump"); // Using Jump action for Drift/Hop
            
            moveAction?.Enable();
            driftAction?.Enable();
        }

        // Create the Visuals container dynamically to isolate body lean from SkinnedMesh bone skinning
        GameObject visualsGo = new GameObject("Visuals");
        visualsGo.transform.SetParent(transform, false);
        visualsGo.transform.localPosition = Vector3.zero;
        visualsGo.transform.localRotation = Quaternion.identity;
        visualsGo.transform.localScale = Vector3.one;

        // Move all other children into the Visuals container
        System.Collections.Generic.List<Transform> childrenToMove = new System.Collections.Generic.List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != visualsGo.transform)
            {
                childrenToMove.Add(child);
            }
        }

        foreach (var child in childrenToMove)
        {
            child.SetParent(visualsGo.transform, true);
        }

        // We set the bodyTransform to the Visuals container so the entire visual assembly (mesh + bones) leans together!
        bodyTransform = visualsGo.transform;

        // Cache initial local rotations and positions of the wheels (after parenting is completed)
        if (frontLeftWheel != null) { flInitialRot = frontLeftWheel.localRotation; flInitialPos = frontLeftWheel.localPosition; }
        if (frontRightWheel != null) { frInitialRot = frontRightWheel.localRotation; frInitialPos = frontRightWheel.localPosition; }
        if (rearLeftWheel != null) { rlInitialRot = rearLeftWheel.localRotation; rlInitialPos = rearLeftWheel.localPosition; }
        if (rearRightWheel != null) { rrInitialRot = rearRightWheel.localRotation; rrInitialPos = rearRightWheel.localPosition; }

        smoothedGroundNormal = Vector3.up;
        lastStuckCheckPosition = transform.position;
    }

    private void resultLog(string message)
    {
        // helper to print logs safely
    }

    private void Update()
    {
        if (!controlsEnabled)
        {
            throttleInput = 0f;
            steeringInput = 0f;
            isDrifting = false;
            smoothedThrottleInput = 0f;
            smoothedSteeringInput = 0f;
            if (rb != null)
            {
                rb.linearVelocity = Vector3.MoveTowards(rb.linearVelocity, Vector3.zero, Time.deltaTime * 10f);
            }
            UpdateWheelVisuals();
            return;
        }

        bool previousDrift = isDrifting;

        if (isPlayer)
        {
            HandlePlayerInput();

            // Drift boost duration charging and release (Player only)
            if (isDrifting)
            {
                driftDuration += Time.deltaTime;
                if (driftDirection == 0f && steeringInput != 0f)
                {
                    driftDirection = steeringInput > 0f ? 1f : -1f;
                }
            }
            else
            {
                if (previousDrift && driftDuration > 0.6f)
                {
                    if (driftDuration >= 1.5f)
                    {
                        activeBoostTimer = 1.8f; // Super-Turbo (orange sparks feel)
                        activeBoostMultiplier = 1.60f;
                        Debug.Log("SUPER DRIFT BOOST ACTIVATED!");
                    }
                    else
                    {
                        activeBoostTimer = 1.0f; // Mini-Turbo (blue sparks feel)
                        activeBoostMultiplier = 1.35f;
                        Debug.Log("MINI DRIFT BOOST ACTIVATED!");
                    }
                }
                driftDuration = 0f;
                driftDirection = 0f;
            }

            // Slipstream (Vácuo) mechanics
            UpdateSlipstream();

            // Stunt / Jump Trick detection in the air
            if (!isGrounded && !hasStuntPerformed && jumpJustPressed)
            {
                hasStuntPerformed = true;
                stuntSpinTime = 0.3f; // Spin visually for 0.3 seconds
                Debug.Log("STUNT TRICK PERFORMED! Landing Boost charged.");
            }

            // Player hold-to-respawn check (R Key)
            if (Keyboard.current != null)
            {
                if (Keyboard.current.rKey.isPressed)
                {
                    respawnHoldTimer += Time.deltaTime;
                    if (respawnHoldTimer >= 1.0f)
                    {
                        RespawnAtClosestWaypoint();
                        respawnHoldTimer = 0f;
                    }
                }
                else
                {
                    respawnHoldTimer = 0f;
                }
            }
        }
        else
        {
            HandleAIInput();
        }

        UpdateWheelVisuals();
    }

    private void LateUpdate()
    {
        if (bodyTransform != null)
        {
            // Calculate a centrifugal outward lean/roll and acceleration pitch
            float targetRoll = -smoothedSteeringInput * bodyLeanAmount;
            float targetPitch = (smoothedThrottleInput > 0.05f ? 2f : (smoothedThrottleInput < -0.05f ? -4f : 0f));

            // Smoothly interpolate drift angle (Y-axis rotation of body)
            float targetYaw = 0f;
            if (isDrifting && driftDirection != 0f)
            {
                // Face the chassi into the turn while sliding outwards
                targetYaw = driftDirection * 28f;
                
                // Aggressive inwards body lean during drift (locked to drift direction, ignoring counter-steer)
                targetRoll = -driftDirection * bodyLeanAmount * 1.5f;
            }
            driftYawOffset = Mathf.MoveTowards(driftYawOffset, targetYaw, Time.deltaTime * 140f);

            Quaternion targetLocalRot = Quaternion.Euler(targetPitch, driftYawOffset, targetRoll);

            // Apply stunt flip visual rotation if stunt is active
            if (stuntSpinTime > 0f)
            {
                stuntSpinTime -= Time.deltaTime;
                float stuntAngle = (stuntSpinTime / 0.3f) * 360f;
                Quaternion stuntRot = Quaternion.Euler(stuntAngle, 0f, 0f);
                targetLocalRot = targetLocalRot * stuntRot;
            }

            bodyTransform.localRotation = Quaternion.Slerp(bodyTransform.localRotation, targetLocalRot, Time.deltaTime * 10f);
        }
    }

    private void FixedUpdate()
    {
        CheckGroundStatus();
        UpdateWaypointTracking();
        ApplyMovementPhysics();
    }

    private void HandlePlayerInput()
    {
        if (!controlsEnabled)
        {
            throttleInput = 0f;
            steeringInput = 0f;
            isDrifting = false;
            jumpJustPressed = false;
            return;
        }

        float inputThrottle = 0f;
        float inputSteer = 0f;

        // 1. Read from Input System Action if available
        if (moveAction != null && moveAction.enabled)
        {
            Vector2 moveValue = moveAction.ReadValue<Vector2>();
            inputThrottle = moveValue.y;
            inputSteer = moveValue.x;
        }

        // 2. Read from direct Keyboard fallback to guarantee control is ALWAYS working
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) 
                inputThrottle = Mathf.Max(inputThrottle, 1f);
            else if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) 
                inputThrottle = Mathf.Min(inputThrottle, -1f);

            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) 
                inputSteer = Mathf.Min(inputSteer, -1f);
            else if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) 
                inputSteer = Mathf.Max(inputSteer, 1f);
        }

        throttleInput = inputThrottle;
        steeringInput = inputSteer;

        // Drift / Jump
        bool inputDrift = false;
        jumpJustPressed = false;
        if (driftAction != null && driftAction.enabled)
        {
            inputDrift = driftAction.IsPressed();
            jumpJustPressed = driftAction.WasPressedThisFrame();
        }
        if (Keyboard.current != null)
        {
            inputDrift = inputDrift || Keyboard.current.spaceKey.isPressed;
            jumpJustPressed = jumpJustPressed || Keyboard.current.spaceKey.wasPressedThisFrame;
        }

        isDrifting = inputDrift && Mathf.Abs(steeringInput) > 0.15f && isGrounded;
    }

    private void HandleAIInput()
    {
        if (!controlsEnabled)
        {
            throttleInput = 0f;
            steeringInput = 0f;
            isDrifting = false;
            return;
        }

        if (waypointCircuit == null || waypointCircuit.waypoints == null || waypointCircuit.waypoints.Length == 0)
        {
            throttleInput = 0f;
            steeringInput = 0f;
            isDrifting = false;
            return;
        }

        float absSpeed = rb != null ? rb.linearVelocity.magnitude : Mathf.Abs(currentSpeed);
        if (absSpeed > 4.5f)
        {
            aiWaypointTimeoutTimer = 0f;
            aiReverseCount = 0;
        }

        // Check if wiggling/stuck in the same small 3.0m area (net displacement)
        stuckPositionTimer += Time.deltaTime;
        if (stuckPositionTimer >= 1.5f)
        {
            stuckPositionTimer = 0f;
            float distanceMoved = Vector3.Distance(transform.position, lastStuckCheckPosition);
            if (distanceMoved < 3.0f)
            {
                accumulatedStuckTime += 1.5f;
            }
            else
            {
                accumulatedStuckTime = Mathf.Max(0f, accumulatedStuckTime - 1.0f); // decay slowly
            }
            lastStuckCheckPosition = transform.position;
        }

        if (accumulatedStuckTime >= 4.0f)
        {
            Debug.Log(gameObject.name + " detected stuck in same area (wiggling/stuck cycle). Respawning.");
            RespawnAtClosestWaypoint();
            return;
        }

        Vector3 targetPos = waypointCircuit.waypoints[currentWaypointIndex].position;

        // 1. Stuck & Obstacle detection (handles forward, reverse, and wiggling)
        if (isGrounded && absSpeed < 1.2f && Mathf.Abs(throttleInput) > 0.15f)
        {
            aiStuckTimer += Time.deltaTime;
        }
        else
        {
            aiStuckTimer = Mathf.Max(0f, aiStuckTimer - Time.deltaTime * 0.6f); // Decay slowly so shifting/transitional states don't wipe progress
        }

        // Auto-respawn if stuck for too long, fell off, or drifted too far
        float distToTargetWp = Vector3.Distance(transform.position, targetPos);
        
        // Stuck checks: timeout or reverse loops
        aiWaypointTimeoutTimer += Time.deltaTime;
        bool isStuckTimeout = aiWaypointTimeoutTimer > 10.0f;
        bool isReverseLoopStuck = aiReverseCount >= 2;

        if (aiStuckTimer > aiMaxStuckTime || transform.position.y < -10f || distToTargetWp > 65f || isStuckTimeout || isReverseLoopStuck)
        {
            Debug.Log(gameObject.name + " detected stuck. Timeout: " + isStuckTimeout + ", Loop: " + isReverseLoopStuck + ", ReverseCount: " + aiReverseCount + ". Respawning.");
            RespawnAtClosestWaypoint();
            return;
        }

        if (aiStuckTimer > 2.0f && !aiIsReversing)
        {
            aiIsReversing = true;
            aiReverseDuration = Random.Range(1.2f, 1.8f);
            aiStuckTimer = 0.5f; // Keep some stuck history so it triggers respawn faster if reverse fails
            aiReverseCount++;
        }

        if (aiIsReversing)
        {
            aiReverseDuration -= Time.deltaTime;
            throttleInput = -0.9f;
            Vector3 localTarget = transform.InverseTransformPoint(targetPos);
            steeringInput = localTarget.x >= 0f ? -0.8f : 0.8f; 

            if (aiReverseDuration <= 0f)
            {
                aiIsReversing = false;
            }
            return;
        }

        // 2. Overtaking & Obstacle Avoidance (3-way Whiskers)
        aiOvertakeTimer -= Time.deltaTime;
        if (aiOvertakeTimer <= 0f)
        {
            aiOvertakeSideOffset = 0f;
            
            // Cast three whiskers forward (Center, Left, Right)
            Vector3 centerRayStart = transform.position + Vector3.up * 0.85f;
            Vector3 leftRayStart = centerRayStart - transform.right * 0.5f;
            Vector3 rightRayStart = centerRayStart + transform.right * 0.5f;
            
            float checkDistance = 9.0f;
            RaycastHit hit;
            bool hitObstacle = false;
            float obstacleOffsetDir = 0f;

            // Center sensor
            if (Physics.Raycast(centerRayStart, transform.forward, out hit, checkDistance))
            {
                if (IsValidObstacle(hit))
                {
                    hitObstacle = true;
                    Vector3 localHitPoint = transform.InverseTransformPoint(hit.point);
                    obstacleOffsetDir = localHitPoint.x >= 0f ? -1.3f : 1.3f;
                }
            }

            // Left sensor (angled slightly outwards)
            if (!hitObstacle && Physics.Raycast(leftRayStart, Quaternion.Euler(0f, -18f, 0f) * transform.forward, out hit, checkDistance * 0.8f))
            {
                if (IsValidObstacle(hit))
                {
                    hitObstacle = true;
                    obstacleOffsetDir = 1.3f; // Turn right to avoid left obstacle
                }
            }

            // Right sensor (angled slightly outwards)
            if (!hitObstacle && Physics.Raycast(rightRayStart, Quaternion.Euler(0f, 18f, 0f) * transform.forward, out hit, checkDistance * 0.8f))
            {
                if (IsValidObstacle(hit))
                {
                    hitObstacle = true;
                    obstacleOffsetDir = -1.3f; // Turn left to avoid right obstacle
                }
            }

            if (hitObstacle)
            {
                aiOvertakeDirection = obstacleOffsetDir;
                aiOvertakeSideOffset = aiOvertakeDirection * Random.Range(1.8f, 2.8f);
                aiOvertakeTimer = Random.Range(1.0f, 1.8f);
            }
        }

        // Apply the side offset perpendicular to the track heading
        if (Mathf.Abs(aiOvertakeSideOffset) > 0.05f)
        {
            Vector3 trackDirection = Vector3.forward;
            if (currentWaypointIndex > 0)
            {
                trackDirection = (targetPos - waypointCircuit.waypoints[currentWaypointIndex - 1].position).normalized;
            }
            Vector3 sideDirection = Vector3.Cross(Vector3.up, trackDirection).normalized;
            targetPos += sideDirection * aiOvertakeSideOffset;
        }

        // 3. Driving Speed Adjustments & Rubberbanding (Optimized caching!)
        playerCacheTimer -= Time.deltaTime;
        if (playerKartCached == null || playerCacheTimer <= 0f)
        {
            playerCacheTimer = 2.0f; // Update reference only every 2 seconds instead of every frame
            KartController[] allKarts = Object.FindObjectsByType<KartController>(FindObjectsSortMode.None);
            foreach (var k in allKarts)
            {
                if (k.isPlayer) { playerKartCached = k; break; }
            }
        }

        GameObject playerObj = playerKartCached != null ? playerKartCached.gameObject : null;
        float rubberbandMultiplier = 1.0f;
        if (playerObj != null)
        {
            float distToPlayer = Vector3.Distance(transform.position, playerObj.transform.position);
            Vector3 localPlayerPos = transform.InverseTransformPoint(playerObj.transform.position);
            
            if (localPlayerPos.z > 0f) // Player is ahead
            {
                float maxBoost = 1.35f;
                if (aiDifficulty == AIDifficulty.Facil) maxBoost = 1.10f;
                else if (aiDifficulty == AIDifficulty.Dificil) maxBoost = 1.90f; // Extremely aggressive catch up

                rubberbandMultiplier = Mathf.Lerp(1.0f, maxBoost, Mathf.Clamp01(distToPlayer / 35f));
            }
            else // Player is behind
            {
                float maxNerf = 0.82f;
                if (aiDifficulty == AIDifficulty.Facil) maxNerf = 0.70f;
                else if (aiDifficulty == AIDifficulty.Dificil) maxNerf = 1.0f; // No nerfing/waiting for the player on hard mode!

                rubberbandMultiplier = Mathf.Lerp(1.0f, maxNerf, Mathf.Clamp01(distToPlayer / 35f));
            }
        }
        Vector3 localTargetPos = transform.InverseTransformPoint(targetPos);
        
        // Calculate steer input
        float angleToTarget = Mathf.Atan2(localTargetPos.x, localTargetPos.z) * Mathf.Rad2Deg;
        steeringInput = Mathf.Clamp(angleToTarget / 32f, -1f, 1f);

        // Throttle input: full speed forward, but slow down dynamically in sharp turns
        float activeSpeedAdaptation = aiSpeedAdaptation;
        if (aiDifficulty == AIDifficulty.Facil) activeSpeedAdaptation = Mathf.Min(1.0f, aiSpeedAdaptation * 1.3f);
        else if (aiDifficulty == AIDifficulty.Medio) activeSpeedAdaptation = aiSpeedAdaptation * 0.65f; // Slower deceleration in curves (65%)
        else if (aiDifficulty == AIDifficulty.Dificil) activeSpeedAdaptation = aiSpeedAdaptation * 0.25f; // Barely slows down in curves
        else if (aiDifficulty == AIDifficulty.Adaptavel) activeSpeedAdaptation = aiSpeedAdaptation * 0.5f;
        else if (aiDifficulty == AIDifficulty.Competitivo) activeSpeedAdaptation = aiSpeedAdaptation * 0.3f;

        float speedFactor = Mathf.Max(1f - (Mathf.Abs(steeringInput) * activeSpeedAdaptation), 0.35f);
        throttleInput = speedFactor * rubberbandMultiplier;

        // If the turn is sharp enough, the AI will drift (stable drift hysteresis)
        float activeMaxSpeed = maxSpeed;
        if (aiDifficulty == AIDifficulty.Facil) activeMaxSpeed = maxSpeed * 0.70f;
        else if (aiDifficulty == AIDifficulty.Medio) activeMaxSpeed = maxSpeed * 1.28f; // 1.28x faster Medio speed
        else if (aiDifficulty == AIDifficulty.Dificil) activeMaxSpeed = maxSpeed * 1.45f; // Very fast top speed
        else if (aiDifficulty == AIDifficulty.Adaptavel)
        {
            if (playerKartCached != null)
            {
                float playerSpeed = playerKartCached.rb != null ? playerKartCached.rb.linearVelocity.magnitude : playerKartCached.currentSpeed;
                float dist = Vector3.Distance(transform.position, playerKartCached.transform.position);
                Vector3 localPlayerPos = transform.InverseTransformPoint(playerKartCached.transform.position);
                if (localPlayerPos.z > 0f)
                {
                    activeMaxSpeed = playerSpeed + Mathf.Clamp((dist - 6f) * 1.2f, -10f, 15f);
                }
                else
                {
                    activeMaxSpeed = playerSpeed + Mathf.Clamp((6f - dist) * 0.8f, -15f, 10f);
                }
                activeMaxSpeed = Mathf.Clamp(activeMaxSpeed, maxSpeed * 0.4f, maxSpeed * 1.45f);
            }
        }
        else if (aiDifficulty == AIDifficulty.Competitivo)
        {
            if (playerKartCached != null)
            {
                float playerSpeed = playerKartCached.rb != null ? playerKartCached.rb.linearVelocity.magnitude : playerKartCached.currentSpeed;
                float dist = Vector3.Distance(transform.position, playerKartCached.transform.position);
                Vector3 localPlayerPos = transform.InverseTransformPoint(playerKartCached.transform.position);
                if (localPlayerPos.z > 0f) // Player is ahead
                {
                    activeMaxSpeed = playerSpeed + 3f + Mathf.Clamp(dist * 1.4f, 0f, 16f);
                }
                else // Player is behind
                {
                    if (dist < 8f)
                    {
                        activeMaxSpeed = playerSpeed + 2f + (8f - dist) * 0.5f;
                    }
                    else
                    {
                        activeMaxSpeed = Mathf.Max(playerSpeed + 2f, maxSpeed * 1.25f);
                    }
                }
                activeMaxSpeed = Mathf.Clamp(activeMaxSpeed, maxSpeed * 0.8f, maxSpeed * 1.50f);
            }
        }

        bool speedIsEnough = rb != null && rb.linearVelocity.magnitude > (activeMaxSpeed * 0.35f);
        if (isDrifting)
        {
            isDrifting = Mathf.Abs(steeringInput) > 0.5f && speedIsEnough && isGrounded;
            if (isDrifting && driftDirection == 0f && steeringInput != 0f)
            {
                driftDirection = steeringInput > 0f ? 1f : -1f;
            }
        }
        else
        {
            isDrifting = Mathf.Abs(steeringInput) > 0.85f && speedIsEnough && isGrounded && !aiIsReversing;
            driftDirection = 0f;
        }

    }

    private bool IsValidObstacle(RaycastHit hit)
    {
        // Ignore ourselves or children
        if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform))
            return false;

        // Ignore road/ground. A valid obstacle has a steep or non-upwards normal (normal.y < 0.6f)
        if (hit.normal.y > 0.6f)
            return false;

        bool isOtherKart = hit.collider.GetComponentInParent<KartController>() != null;
        if (isOtherKart) return true;

        string name = hit.collider.gameObject.name.ToLower();
        // Ignore road/terrain
        if (name.Contains("road") || name.Contains("pista") || name.Contains("ground") || name.Contains("chao") || name.Contains("terrain"))
            return false;

        bool isScenery = name.Contains("wood") || name.Contains("fence") || name.Contains("feno") || name.Contains("mapa") || name.Contains("wall") || name.Contains("colision") || name.Contains("collider");
        return isScenery;
    }

    private void UpdateWaypointTracking()
    {
        if (waypointCircuit == null || waypointCircuit.waypoints == null || waypointCircuit.waypoints.Length == 0)
            return;

        int W = waypointCircuit.waypoints.Length;

        // Find the closest waypoint to our current position
        float closestDist = float.MaxValue;
        int closestIdx = 0;
        for (int i = 0; i < W; i++)
        {
            if (waypointCircuit.waypoints[i] == null) continue;
            float d = Vector3.Distance(transform.position, waypointCircuit.waypoints[i].position);
            if (d < closestDist)
            {
                closestDist = d;
                closestIdx = i;
            }
        }

        // Initialize lastClosestIdx on first run
        if (lastClosestIdx == -1)
        {
            lastClosestIdx = closestIdx;
        }

        // Detect lap crossing: transitioning from the end of the track to the beginning
        if (closestIdx != lastClosestIdx)
        {
            float thresholdHigh = W * 0.7f;
            float thresholdLow = W * 0.3f;

            // Forward crossing: from last 30% of waypoints to first 30% of waypoints
            if (lastClosestIdx >= thresholdHigh && closestIdx <= thresholdLow)
            {
                currentLap++;
                Debug.Log(gameObject.name + " completed lap! New Lap: " + currentLap);
            }

            lastClosestIdx = closestIdx;
        }

        // Target waypoint is always the one ahead of the closest one
        currentWaypointIndex = (closestIdx + 1) % W;
    }

    private void CheckGroundStatus()
    {
        // Raycast down to find ground and normal
        // Start 0.5m above the pivot and look down 1.8m.
        // We use RaycastAll to filter out our own colliders so we never "ground" on ourselves!
        RaycastHit[] hits = Physics.RaycastAll(transform.position + Vector3.up * 0.5f, Vector3.down, 1.8f);
        isGrounded = false;
        RaycastHit closestHit = default;
        float closestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            // 1. Ignore ourselves and any of our children
            if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform))
                continue;

            // 2. Ignore other karts completely (using parent check) to prevent flipping when they overlap or touch
            if (hit.collider.GetComponentInParent<KartController>() != null)
                continue;

            // 3. Ignore vertical walls, fences, and steep surfaces. A valid ground normal must have a Y component of at least 0.6.
            if (hit.normal.y < 0.6f)
                continue;

            if (hit.distance < closestDist)
            {
                closestDist = hit.distance;
                closestHit = hit;
            }
        }

        if (closestDist <= 0.85f)
        {
            isGrounded = true;
            groundNormal = closestHit.normal;
        }
        else
        {
            isGrounded = false;
            groundNormal = Vector3.up;
        }
    }

    private void ApplyMovementPhysics()
    {
        // Smooth out inputs to give the kart some momentum/weight and remove abrupt snaps
        smoothedSteeringInput = Mathf.MoveTowards(smoothedSteeringInput, steeringInput, steeringDamping * Time.fixedDeltaTime);
        smoothedThrottleInput = Mathf.MoveTowards(smoothedThrottleInput, throttleInput, throttleDamping * Time.fixedDeltaTime);

        if (driftHopCooldownTimer > 0f)
        {
            driftHopCooldownTimer -= Time.fixedDeltaTime;
        }

        // Smoothly interpolate the ground normal to avoid micro-jitter on uneven terrain/polygons
        if (isGrounded)
        {
            smoothedGroundNormal = Vector3.Slerp(smoothedGroundNormal, groundNormal, Time.fixedDeltaTime * 12f);
        }
        else
        {
            smoothedGroundNormal = Vector3.Slerp(smoothedGroundNormal, Vector3.up, Time.fixedDeltaTime * 5f);
        }

        // Check for landing after a stunt
        if (isGrounded && hasStuntPerformed)
        {
            hasStuntPerformed = false;
            stuntSpinTime = 0f;
            activeBoostTimer = 1.2f; // Boost for 1.2 seconds
            activeBoostMultiplier = 1.45f;
            Debug.Log("LANDING STUNT BOOST ACTIVATED!");
        }

        // Check for Drift Hop or Simple Hop (Jump!) - Player only to prevent AI jitter/instability
        if (isPlayer && jumpJustPressed && isGrounded && driftHopCooldownTimer <= 0f)
        {
            rb.AddForce(transform.up * driftHopForce, ForceMode.VelocityChange);
            driftHopCooldownTimer = hopCooldown;
            jumpJustPressed = false; // Consume jump event
        }
        wasDrifting = isDrifting;

        // Synchronize our internal tracking speed with the actual Rigidbody speed!
        if (rb != null)
        {
            if (isGrounded)
            {
                currentSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
            }
            else
            {
                // In the air, track the horizontal velocity magnitude to prevent speed loss when leveling out
                Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                currentSpeed = horizontalVel.magnitude;
            }

            // Wall Collision Preventative Anti-Jitter:
            // Check if there is an obstacle directly in front of or behind the kart to prevent clipping/vibrating against walls
            if (isGrounded)
            {
                if (currentSpeed > 0.1f)
                {
                    RaycastHit wallHit;
                    if (Physics.Raycast(transform.position + Vector3.up * 0.45f, transform.forward, out wallHit, 1.2f))
                    {
                        if (!wallHit.collider.isTrigger && wallHit.collider.gameObject != gameObject && !wallHit.collider.transform.IsChildOf(transform))
                        {
                            if (Vector3.Dot(transform.forward, wallHit.normal) < -0.5f)
                            {
                                currentSpeed = 0f;
                            }
                        }
                    }
                }
                else if (currentSpeed < -0.1f)
                {
                    RaycastHit wallHit;
                    if (Physics.Raycast(transform.position + Vector3.up * 0.45f, -transform.forward, out wallHit, 1.2f))
                    {
                        if (!wallHit.collider.isTrigger && wallHit.collider.gameObject != gameObject && !wallHit.collider.transform.IsChildOf(transform))
                        {
                            if (Vector3.Dot(-transform.forward, wallHit.normal) < -0.5f)
                            {
                                currentSpeed = 0f;
                            }
                        }
                    }
                }
            }
        }

        // Apply Mini-Turbo Drift Boost timer update
        if (activeBoostTimer > 0f)
        {
            activeBoostTimer -= Time.fixedDeltaTime;
        }
        else
        {
            activeBoostMultiplier = 1.0f;
        }

        // 1. Calculate Target Forward Speed and Acceleration Rate using smoothed throttle input
        float targetForwardSpeed = 0f;
        float currentAccel = acceleration;

        float activeMaxSpeed = maxSpeed;
        float activeAcceleration = acceleration;

        // Apply Boost modifier to max speed and acceleration
        if (activeBoostTimer > 0f)
        {
            activeMaxSpeed *= activeBoostMultiplier;
            activeAcceleration *= 1.8f; // Stronger acceleration during boost
        }

        if (!isPlayer)
        {
            if (aiDifficulty == AIDifficulty.Facil)
            {
                activeMaxSpeed = maxSpeed * 0.70f;
                activeAcceleration = acceleration * 0.75f;
            }
            else if (aiDifficulty == AIDifficulty.Medio)
            {
                activeMaxSpeed = maxSpeed * 1.28f; // 1.28x speed multiplier for Medio mode
                activeAcceleration = acceleration * 1.26f; // 1.26x acceleration multiplier for Medio mode
            }
            else if (aiDifficulty == AIDifficulty.Dificil)
            {
                activeMaxSpeed = maxSpeed * 1.45f; // Match 1.45x top speed limit
                activeAcceleration = acceleration * 1.50f; // 1.5x punchier acceleration
            }
            else if (aiDifficulty == AIDifficulty.Adaptavel)
            {
                if (playerKartCached != null)
                {
                    float playerSpeed = playerKartCached.rb != null ? playerKartCached.rb.linearVelocity.magnitude : playerKartCached.currentSpeed;
                    float dist = Vector3.Distance(transform.position, playerKartCached.transform.position);
                    Vector3 localPlayerPos = transform.InverseTransformPoint(playerKartCached.transform.position);
                    
                    if (localPlayerPos.z > 0f) // Player is ahead
                    {
                        activeMaxSpeed = playerSpeed + Mathf.Clamp((dist - 6f) * 1.2f, -10f, 15f);
                    }
                    else // Player is behind
                    {
                        activeMaxSpeed = playerSpeed + Mathf.Clamp((6f - dist) * 0.8f, -15f, 10f);
                    }
                    
                    activeMaxSpeed = Mathf.Clamp(activeMaxSpeed, maxSpeed * 0.4f, maxSpeed * 1.45f);
                    activeAcceleration = acceleration * 1.30f;
                }
                else
                {
                    activeMaxSpeed = maxSpeed;
                    activeAcceleration = acceleration;
                }
            }
            else if (aiDifficulty == AIDifficulty.Competitivo)
            {
                if (playerKartCached != null)
                {
                    float playerSpeed = playerKartCached.rb != null ? playerKartCached.rb.linearVelocity.magnitude : playerKartCached.currentSpeed;
                    float dist = Vector3.Distance(transform.position, playerKartCached.transform.position);
                    Vector3 localPlayerPos = transform.InverseTransformPoint(playerKartCached.transform.position);
                    
                    if (localPlayerPos.z > 0f) // Player is ahead
                    {
                        activeMaxSpeed = playerSpeed + 3f + Mathf.Clamp(dist * 1.4f, 0f, 16f);
                    }
                    else // Player is behind
                    {
                        if (dist < 8f)
                        {
                            activeMaxSpeed = playerSpeed + 2f + (8f - dist) * 0.5f;
                        }
                        else
                        {
                            activeMaxSpeed = Mathf.Max(playerSpeed + 2f, maxSpeed * 1.25f);
                        }
                    }
                    
                    activeMaxSpeed = Mathf.Clamp(activeMaxSpeed, maxSpeed * 0.8f, maxSpeed * 1.50f);
                    activeAcceleration = acceleration * 1.40f;
                }
                else
                {
                    activeMaxSpeed = maxSpeed;
                    activeAcceleration = acceleration;
                }
            }
        }

        if (isGrounded)
        {
            bool isBrakeDrifting = isPlayer && isDrifting && throttleInput <= 0.1f;
            
            if (isBrakeDrifting)
            {
                // Brake-Drift speed capping: limit speed and apply deceleration
                targetForwardSpeed = activeMaxSpeed * 0.40f; // Cap speed to 40%
                currentAccel = deceleration * 2.0f; // Decelerate very fast
            }
            else if (smoothedThrottleInput > 0.05f)
            {
                targetForwardSpeed = smoothedThrottleInput * activeMaxSpeed;
                currentAccel = activeAcceleration;
            }
            else if (smoothedThrottleInput < -0.05f)
            {
                targetForwardSpeed = smoothedThrottleInput * reverseSpeed;
                // SNAPPY REVERSE: Give a 2.5x boost to acceleration when trying to reverse, 
                // allowing karts to instantly back out of crashes!
                currentAccel = acceleration * 2.5f;
            }
            else
            {
                targetForwardSpeed = 0f;
                currentAccel = deceleration;
            }

            // Non-linear torque curve: higher acceleration at low speeds, tapering off near top speed
            float speedRatio = Mathf.Clamp01(Mathf.Abs(currentSpeed) / Mathf.Max(activeMaxSpeed, 1f));
            float torqueFactor = Mathf.Lerp(1.5f, 0.5f, speedRatio);

            currentSpeed = Mathf.MoveTowards(currentSpeed, targetForwardSpeed, currentAccel * torqueFactor * Time.fixedDeltaTime);
        }
        else
        {
            // Air friction (deceleration)
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, deceleration * 0.2f * Time.fixedDeltaTime);
        }

        // 2. Extra Gravity to stick to slopes and track
        if (isGrounded)
        {
            rb.AddForce(-smoothedGroundNormal * gravityForce, ForceMode.Acceleration);
        }
        else
        {
            rb.AddForce(Vector3.down * gravityForce * 1.2f, ForceMode.Acceleration);
        }

        // 3. Steering & Rotation Alignment (UNIFIED PHYSICAL METHOD)
        float turnAngle = 0f;
        if (isGrounded && Mathf.Abs(currentSpeed) > 0.5f)
        {
            // Reverse steering if driving backward
            float steerDirection = currentSpeed >= 0 ? 1f : -1f;

            if (isDrifting && driftDirection != 0f)
            {
                // Mapped drift steering: always turn in drift direction, scale rate based on input
                // counter-steer (input opposite to driftDirection) opens up the curve (0.10x speed)
                // steer-in (input same as driftDirection) tightens the curve (0.60x speed)
                float steerFactor = 0.35f + (steeringInput * driftDirection) * 0.25f; // ranges from 0.10f to 0.60f
                
                // Brake-Drift: if player is not accelerating (throttleInput <= 0.1f) during drift, boost steer rate
                bool isBrakeDrifting = isPlayer && throttleInput <= 0.1f;
                if (isBrakeDrifting)
                {
                    steerFactor *= 1.8f;
                }

                float actualSteerSpeed = steeringSpeed * driftSteerMultiplier * steerFactor;

                turnAngle = driftDirection * actualSteerSpeed * steerDirection * Time.fixedDeltaTime;
            }
            else
            {
                turnAngle = smoothedSteeringInput * steeringSpeed * steerDirection * Time.fixedDeltaTime;
            }
        }

        // Step A: Apply Yaw Steering directly to our current rotation
        Quaternion steerRot = Quaternion.AngleAxis(turnAngle, transform.up);
        Quaternion yawedRot = steerRot * rb.rotation;

        // Step B: Calculate slope alignment and force zero roll (Z-axis tilt)
        Quaternion targetRot = yawedRot;
        if (isGrounded)
        {
            Vector3 forwardOnSlope = Vector3.ProjectOnPlane(yawedRot * Vector3.forward, smoothedGroundNormal).normalized;
            if (forwardOnSlope.sqrMagnitude > 0.001f)
            {
                targetRot = Quaternion.LookRotation(forwardOnSlope, smoothedGroundNormal);
            }
        }
        else
        {
            // In the air: smoothly level the kart out so it lands flat on its wheels!
            Vector3 forwardHorizontal = yawedRot * Vector3.forward;
            forwardHorizontal.y = 0f;
            forwardHorizontal.Normalize();
            if (forwardHorizontal.sqrMagnitude > 0.001f)
            {
                targetRot = Quaternion.LookRotation(forwardHorizontal, Vector3.up);
            }
        }

        // Use rb.MoveRotation to update physical orientation smoothly, allowing Unity's 
        // Rigidbody Interpolation to render motion without any camera micro-jitter/teleports!
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, Time.fixedDeltaTime * (isGrounded ? 30f : 10f)));

        // 4. Sideways Friction / Drift Blend (Local-Space Velocity Decomposition)
        if (isGrounded)
        {
            // Smoothly blend grip value instead of snapping instantly
            float targetGrip = isDrifting ? driftGrip : normalGrip;

            // Fluid slip: Reduce target grip slightly when making high-speed turns normally
            if (!isDrifting && Mathf.Abs(smoothedSteeringInput) > 0.1f)
            {
                targetGrip = Mathf.Lerp(normalGrip, normalGrip * 0.65f, Mathf.Abs(smoothedSteeringInput));
            }

            // Lerp the grip value. Exiting drift recovers grip slowly (3.5f) for a smooth transition.
            float gripSpeed = isDrifting ? 15f : 3.5f;
            currentGripValue = Mathf.Lerp(currentGripValue, targetGrip, Time.fixedDeltaTime * gripSpeed);

            // We want our forward velocity along transform.forward to match currentSpeed
            float forwardVel = currentSpeed;

            // We want our sideways velocity along transform.right (sliding) to be damped towards 0 based on grip
            float sidewaysVel = Vector3.Dot(rb.linearVelocity, transform.right);
            float targetSidewaysVel = 0f;

            // Apply centrifugal slide push during drift
            if (isDrifting && driftDirection != 0f)
            {
                // Drift push force: slide outwards from the locked drift direction
                float driftPush = -driftDirection * currentSpeed * 0.45f;
                targetSidewaysVel = driftPush;
            }

            float newSidewaysVel = Mathf.Lerp(sidewaysVel, targetSidewaysVel, currentGripValue * Time.fixedDeltaTime * 50f);

            // We want to preserve our vertical velocity along transform.up so gravity, jumps, and ramps act naturally
            float verticalVel = Vector3.Dot(rb.linearVelocity, transform.up);

            // Reassemble the velocity vector in local space and assign to Rigidbody
            rb.linearVelocity = transform.forward * forwardVel + transform.right * newSidewaysVel + transform.up * verticalVel;
        }
        else
        {
            // In the air, let Unity physics handle the trajectory naturally (preserving launch angles and velocity).
            // We only apply a very minor drag to currentSpeed, which we already did above.
        }
    }

    private void UpdateWheelVisuals()
    {
        // Update suspension offsets first
        if (useVisualSuspension)
        {
            UpdateWheelSuspension(frontLeftWheel, flInitialPos);
            UpdateWheelSuspension(frontRightWheel, frInitialPos);
            UpdateWheelSuspension(rearLeftWheel, rlInitialPos);
            UpdateWheelSuspension(rearRightWheel, rrInitialPos);
        }

        float speed = rb != null ? rb.linearVelocity.magnitude : 0f;
        float spinDir = Vector3.Dot(rb != null ? rb.linearVelocity : Vector3.zero, transform.forward) >= 0 ? 1f : -1f;
        
        // Accumulate roll angle over time
        cumulativeRollAngle += spinDir * speed * wheelSpinSpeed * Time.deltaTime;
        cumulativeRollAngle %= 360f; // Prevent overflow

        // Use smoothedSteeringInput for ultra-smooth wheel turning
        float steerAngle = smoothedSteeringInput * maxWheelTurnAngle;

        // Rotate wheels using parent-space (kart-space) axes, then apply initial local offsets.
        // This is 100% bulletproof for ANY imported model wheel orientation!
        if (frontLeftWheel != null)
        {
            frontLeftWheel.localRotation = Quaternion.Euler(0f, steerAngle, 0f) * Quaternion.Euler(cumulativeRollAngle, 0f, 0f) * flInitialRot;
        }
        if (frontRightWheel != null)
        {
            frontRightWheel.localRotation = Quaternion.Euler(0f, steerAngle, 0f) * Quaternion.Euler(cumulativeRollAngle, 0f, 0f) * frInitialRot;
        }
        if (rearLeftWheel != null)
        {
            rearLeftWheel.localRotation = Quaternion.Euler(cumulativeRollAngle, 0f, 0f) * rlInitialRot;
        }
        if (rearRightWheel != null)
        {
            rearRightWheel.localRotation = Quaternion.Euler(cumulativeRollAngle, 0f, 0f) * rrInitialRot;
        }
    }

    private void UpdateWheelSuspension(Transform wheel, Vector3 initialLocalPos)
    {
        if (wheel == null || !useVisualSuspension) return;

        // Cast ray from mount point (0.3m above the wheel's rest position) downwards along -transform.up
        Vector3 mountPointWorld = transform.TransformPoint(initialLocalPos + Vector3.up * 0.3f);
        float rayLength = 0.3f + suspensionRestDistance;
        
        float targetYOffset = -suspensionRestDistance; // Default fully extended in the air
        
        RaycastHit[] hits = Physics.RaycastAll(mountPointWorld, -transform.up, rayLength);
        float closestDist = float.MaxValue;
        bool grounded = false;

        foreach (var hit in hits)
        {
            // Ignore ourselves and other karts
            if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform))
                continue;
            if (hit.collider.GetComponentInParent<KartController>() != null)
                continue;

            // Ignore triggers
            if (hit.collider.isTrigger)
                continue;

            if (hit.distance < closestDist)
            {
                closestDist = hit.distance;
                grounded = true;
            }
        }

        if (grounded)
        {
            // Center should be at: closestDist - wheelRadius from mount point
            // Rest position is at offset 0.3f from mount point
            targetYOffset = 0.3f - closestDist + wheelRadius;
            targetYOffset = Mathf.Clamp(targetYOffset, -suspensionTravel, suspensionTravel);
        }

        Vector3 targetLocalPos = initialLocalPos + Vector3.up * targetYOffset;
        wheel.localPosition = Vector3.Lerp(wheel.localPosition, targetLocalPos, Time.deltaTime * suspensionDamping);
    }

    private void UpdateSlipstream()
    {
        if (!isPlayer || !isGrounded)
        {
            slipstreamTimer = 0f;
            isDraftingActive = false;
            return;
        }

        // Find all karts in the scene
        KartController[] allKarts = Object.FindObjectsByType<KartController>(FindObjectsSortMode.None);
        bool drafting = false;

        foreach (var other in allKarts)
        {
            if (other == this || other.isPlayer) continue;

            // Calculate distance and relative direction
            Vector3 diff = other.transform.position - transform.position;
            float dist = diff.magnitude;

            if (dist < 14f) // max distance for slipstream
            {
                // Check if other kart is forward relative to us
                Vector3 localDir = transform.InverseTransformDirection(diff.normalized);
                if (localDir.z > 0.75f) // ahead of us
                {
                    // Check alignment of our forward vectors
                    float angle = Vector3.Angle(transform.forward, other.transform.forward);
                    if (angle < 15f) // degrees limit
                    {
                        drafting = true;
                        break;
                    }
                }
            }
        }

        if (drafting)
        {
            slipstreamTimer += Time.deltaTime;
            isDraftingActive = true;
            
            if (slipstreamTimer >= 1.8f) // charge time
            {
                activeBoostTimer = 1.5f; // boost duration
                activeBoostMultiplier = 1.50f; // 50% boost speed
                slipstreamTimer = 0f; // Reset
                Debug.Log("SLIPSTREAM BOOST ACTIVATED!");
            }
        }
        else
        {
            // Decay charge slowly when not behind anyone
            slipstreamTimer = Mathf.Max(0f, slipstreamTimer - Time.deltaTime * 1.5f);
            isDraftingActive = false;
        }
    }

    public void RespawnAtClosestWaypoint()
    {
        if (waypointCircuit == null || waypointCircuit.waypoints == null || waypointCircuit.waypoints.Length == 0)
            return;

        // Find the closest waypoint
        float closestDist = float.MaxValue;
        int closestIdx = 0;
        for (int i = 0; i < waypointCircuit.waypoints.Length; i++)
        {
            if (waypointCircuit.waypoints[i] == null) continue;
            float d = Vector3.Distance(transform.position, waypointCircuit.waypoints[i].position);
            if (d < closestDist)
            {
                closestDist = d;
                closestIdx = i;
            }
        }

        Transform targetWaypoint = waypointCircuit.waypoints[closestIdx];
        Vector3 spawnPos = targetWaypoint.position + Vector3.up * 0.8f;
        
        // Calculate rotation facing next waypoint
        Quaternion spawnRot = targetWaypoint.rotation;
        int nextIdx = (closestIdx + 1) % waypointCircuit.waypoints.Length;
        if (waypointCircuit.waypoints[nextIdx] != null)
        {
            Vector3 lookDir = (waypointCircuit.waypoints[nextIdx].position - targetWaypoint.position).normalized;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                spawnRot = Quaternion.LookRotation(lookDir, Vector3.up);
            }
        }
        
        // Reposition both transform AND Rigidbody directly to prevent Unity snapping/interpolation bugs
        transform.position = spawnPos;
        transform.rotation = spawnRot;
        
        if (rb != null)
        {
            rb.position = spawnPos;
            rb.rotation = spawnRot;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        currentSpeed = 0f;
        smoothedSteeringInput = 0f;
        smoothedThrottleInput = 0f;
        isDrifting = false;
        driftDirection = 0f;
        driftYawOffset = 0f;
        hasStuntPerformed = false;
        stuntSpinTime = 0f;

        currentWaypointIndex = nextIdx;
        aiStuckTimer = 0f;
        aiIsReversing = false;
        aiWaypointTimeoutTimer = 0f;
        aiReverseCount = 0;
        lastStuckCheckPosition = transform.position;
        stuckPositionTimer = 0f;
        accumulatedStuckTime = 0f;

        Debug.Log(gameObject.name + " respawned at waypoint: " + closestIdx);
    }

    // Public method to reset waypoint tracking (for restarting race, etc.)
    public void ResetRaceProgress()
    {
        currentWaypointIndex = 0;
        currentSpeed = 0f;
        currentLap = 1;
        currentPosition = 1;
    }

    public int CurrentWaypointIndex => currentWaypointIndex;

    public float GetRaceProgress()
    {
        if (waypointCircuit == null || waypointCircuit.waypoints == null || waypointCircuit.waypoints.Length == 0)
            return 0f;

        int W = waypointCircuit.waypoints.Length;
        int idx = currentWaypointIndex;
        int prevIdx = (idx - 1 + W) % W;

        if (waypointCircuit.waypoints[prevIdx] == null || waypointCircuit.waypoints[idx] == null)
            return 0f;

        Vector3 P = waypointCircuit.waypoints[prevIdx].position;
        Vector3 N = waypointCircuit.waypoints[idx].position;

        float D = Vector3.Distance(P, N);
        if (D < 0.01f) D = 0.01f;
        float d = Vector3.Distance(transform.position, N);
        float fraction = Mathf.Clamp01(1f - (d / D));

        float pathPosition = prevIdx + fraction;

        return (currentLap - 1) * W + pathPosition;
    }
}
