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
    
    [Header("Drift Tuning (Player Only)")]
    [Tooltip("Base visual yaw rotation of the kart chassi during drift.")]
    public float driftVisualYawBase = 35f;
    [Tooltip("Steering influence on the visual yaw rotation of the kart during drift.")]
    public float driftVisualYawSteerInfluence = 8f;
    [Tooltip("How fast the kart visuals orient/snap to the drift angle.")]
    public float driftVisualYawSpeed = 220f;
    [Tooltip("Physical steering/turning multiplier during drift for the player.")]
    public float driftPhysicalSteerLimit = 0.58f;
    [Tooltip("Base sideways sliding slip factor during drift.")]
    public float driftSlipFactor = 0.75f;
    [Tooltip("Steering influence on the sideways sliding slip factor during drift.")]
    public float driftSlipSteerInfluence = 0.15f;

    [Header("Effects Settings")]
    [Tooltip("Particle systems to play during drift.")]
    public ParticleSystem[] driftParticles;
    [Tooltip("Particle systems to play during boost/nitro.")]
    public ParticleSystem[] boostParticles;
    [Tooltip("Particle system of metal sparks to instantiate at collision contact points.")]
    public ParticleSystem collisionSparksPrefab;
    [Tooltip("Minimum collision speed/force to trigger sparks and screen shake.")]
    public float minCollisionForce = 4f;
    [Tooltip("Maximum collision force used to clamp/scale the screen shake intensity.")]
    public float maxCollisionForce = 25f;
    [Tooltip("Maximum screen shake intensity at max collision force.")]
    public float maxShakeIntensity = 0.5f;
    [Tooltip("Screen shake duration in seconds.")]
    public float shakeDuration = 0.25f;

    [Header("Boost Meter Settings")]
    [Tooltip("Maximum boost score capacity.")]
    public float maxBoostScore = 1000f;
    [Tooltip("Current accumulated boost score.")]
    public float currentBoostScore = 0f;
    [Tooltip("How much boost score is charged per second of drifting.")]
    public float boostChargeRate = 250f;
    [Tooltip("How much boost score is consumed to trigger a single 2-second Nitro Boost.")]
    public float boostActivateCost = 300f;
    [Tooltip("UI GameObjects representing the boost charges/icons.")]
    public GameObject[] boostIcons;

    [Header("Audio Settings")]
    [Tooltip("Audio source to play looping tire screech sound during drift.")]
    public AudioSource driftAudioSource;
    [Tooltip("Audio source to play looping/one-shot sound when boost is active.")]
    public AudioSource boostAudioSource;
    [Tooltip("Target volume for the drift screech sound.")]
    public float maxDriftVolume = 0.8f;
    [Tooltip("How fast the drift sound volume fades in and out.")]
    public float driftFadeSpeed = 4f;
    [Tooltip("Target volume for the boost sound.")]
    public float maxBoostVolume = 0.8f;
    [Tooltip("How fast the boost sound volume fades in and out.")]
    public float boostFadeSpeed = 4f;

    public enum AIDifficulty { Facil, Medio, Dificil, Adaptavel, Competitivo, CompetitivoB, CompetitivoA, CompetitivoF }

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
    private float nitroBoostTimer = 0f;

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
    private float mindsetSpeedBoost = 1.0f;
    private float mindsetAccelBoost = 1.0f;
    private Vector3 stuckRadiusAnchor;
    private float stuckRadiusTimer = 0f;

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

    private float driftBasePitch = 1.0f;
    private float boostBasePitch = 1.0f;
    private float aiBoostCooldownTimer = 0f;

    // Input Actions (New Input System)
    private InputAction moveAction;
    private InputAction driftAction;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (driftAudioSource != null) driftBasePitch = driftAudioSource.pitch;
        if (boostAudioSource != null) boostBasePitch = boostAudioSource.pitch;
        
        // Ensure Rigidbody is configured for arcade racing
        rb.useGravity = true;
        rb.linearDamping = 0.1f;
        rb.angularDamping = 1.5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate; // CRITICAL for smooth camera tracking without visual stutters/teleports!
        
        // Freeze all physical rotations so the sphere collider acts as a stable sliding point,
        // letting our script handle all slope alignment and steering rotations without physics rolling the kart.
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        // Disable colliders on visual wheels and their children to prevent physics engine conflicts when wheels are moved in Update/LateUpdate
        if (frontLeftWheel != null) { foreach (var col in frontLeftWheel.GetComponentsInChildren<Collider>()) col.enabled = false; }
        if (frontRightWheel != null) { foreach (var col in frontRightWheel.GetComponentsInChildren<Collider>()) col.enabled = false; }
        if (rearLeftWheel != null) { foreach (var col in rearLeftWheel.GetComponentsInChildren<Collider>()) col.enabled = false; }
        if (rearRightWheel != null) { foreach (var col in rearRightWheel.GetComponentsInChildren<Collider>()) col.enabled = false; }

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
        stuckRadiusAnchor = transform.position;
        stuckRadiusTimer = 0f;

        // Stop all particles at start to ensure they begin in an inactive state
        if (driftParticles != null)
        {
            foreach (var ps in driftParticles)
            {
                if (ps != null) ps.Stop();
            }
        }
        if (boostParticles != null)
        {
            foreach (var ps in boostParticles)
            {
                if (ps != null) ps.Stop();
            }
        }
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
            UpdateBoostHUD();

            // Drift boost duration charging and release (Player only)
            if (isDrifting)
            {
                driftDuration += Time.deltaTime;
                if (steeringInput != 0f)
                {
                    driftDirection = steeringInput > 0f ? 1f : -1f;
                }
            }
            else
            {
                /* Temporarily disabled drift boost as requested
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
                */
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

        // Charge the boost score meter during active drift for both Player and AI
        if (isDrifting)
        {
            currentBoostScore = Mathf.Min(currentBoostScore + boostChargeRate * Time.deltaTime, maxBoostScore);
        }

        UpdateWheelVisuals();
        UpdateParticles();
        UpdateAudio();
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
                // Dynamic drift yaw angle
                float steerInfluence = steeringInput * driftDirection; // ranges from -1 (counter-steer) to 1 (steer-in)
                float baseAngle = isPlayer ? driftVisualYawBase : 32f;
                
                if (isPlayer)
                {
                    bool isHoldingSpace = false;
                    if (driftAction != null && driftAction.enabled) isHoldingSpace = driftAction.IsPressed();
                    if (Keyboard.current != null) isHoldingSpace = isHoldingSpace || Keyboard.current.spaceKey.isPressed;
                    
                    if (!isHoldingSpace)
                    {
                        baseAngle *= 0.65f; // Reduce visual yaw angle when Space is released
                    }
                }
                
                targetYaw = driftDirection * (baseAngle + steerInfluence * driftVisualYawSteerInfluence);
                
                // Aggressive inwards body lean during drift (locked to drift direction, ignoring counter-steer)
                targetRoll = -driftDirection * bodyLeanAmount * 1.5f;
            }
            driftYawOffset = Mathf.MoveTowards(driftYawOffset, targetYaw, Time.deltaTime * driftVisualYawSpeed);

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

            if (Keyboard.current.leftShiftKey.wasPressedThisFrame || Keyboard.current.rightShiftKey.wasPressedThisFrame)
            {
                if (currentBoostScore >= boostActivateCost)
                {
                    currentBoostScore -= boostActivateCost;
                    nitroBoostTimer = 2.0f;
                    Debug.Log("NITRO BOOST ACTIVATED! Remaining Meter: " + currentBoostScore);
                }
                else
                {
                    Debug.Log("Not enough boost score! Cost: " + boostActivateCost + ", Current: " + currentBoostScore);
                }
            }
        }

        bool isSteeringActively = Mathf.Abs(steeringInput) > 0.15f;
        if (isGrounded)
        {
            // Enter or stay in drift ONLY while the drift button (Space) is held down AND steering actively
            isDrifting = inputDrift && isSteeringActively;
        }
        else
        {
            isDrifting = false;
        }
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

        // Countdown AI boost cooldown
        if (aiBoostCooldownTimer > 0f)
        {
            aiBoostCooldownTimer -= Time.deltaTime;
        }

        // Passive boost charging for AI to ensure they get to use it on straights
        float passiveCharge = 0f;
        if (aiDifficulty == AIDifficulty.Facil) passiveCharge = 15f;
        else if (aiDifficulty == AIDifficulty.Medio) passiveCharge = 15f;
        else if (aiDifficulty == AIDifficulty.Dificil) passiveCharge = 30f;
        else if (aiDifficulty == AIDifficulty.Adaptavel) passiveCharge = 25f;
        else if (aiDifficulty >= AIDifficulty.Competitivo) passiveCharge = 40f; // All competitive modes

        currentBoostScore = Mathf.Min(currentBoostScore + passiveCharge * Time.deltaTime, maxBoostScore);

        if (waypointCircuit == null || waypointCircuit.waypoints == null || waypointCircuit.waypoints.Length == 0)
        {
            throttleInput = 0f;
            steeringInput = 0f;
            isDrifting = false;
            return;
        }

        float absSpeed = rb != null ? rb.linearVelocity.magnitude : Mathf.Abs(currentSpeed);
        
        // Determine AI mindset dynamically based on race positions
        int totalKarts = Object.FindObjectsByType<KartController>(FindObjectsSortMode.None).Length;
        mindsetSpeedBoost = 1.0f;
        mindsetAccelBoost = 1.0f;

        if (currentPosition == 1) // First place - Defending Lead
        {
            // "Estou em primeiro preciso ficar ligado no segundo lugar pra ele não me ultrapassar"
            KartController secondPlaceKart = null;
            KartController[] allKarts = Object.FindObjectsByType<KartController>(FindObjectsSortMode.None);
            foreach (var k in allKarts)
            {
                if (k.currentPosition == 2)
                {
                    secondPlaceKart = k;
                    break;
                }
            }

            if (secondPlaceKart != null)
            {
                float distToSecond = Vector3.Distance(transform.position, secondPlaceKart.transform.position);
                if (distToSecond < 8.0f)
                {
                    float secondSpeed = secondPlaceKart.rb != null ? secondPlaceKart.rb.linearVelocity.magnitude : secondPlaceKart.currentSpeed;
                    mindsetSpeedBoost = Mathf.Max(1.0f, (secondSpeed + 2.0f) / maxSpeed);
                    mindsetAccelBoost = 1.25f;

                    // Block: shift target position to cover the line of the chasing kart
                    Vector3 localSecondPos = transform.InverseTransformPoint(secondPlaceKart.transform.position);
                    if (localSecondPos.z < 0f)
                    {
                        aiOvertakeSideOffset = Mathf.Clamp(localSecondPos.x, -2.0f, 2.0f);
                        aiOvertakeTimer = 0.4f;
                    }
                }
            }
        }
        else if (currentPosition == 2) // Second place - Chasing Leader
        {
            // "Estou em segundo mas posso ficar em primeiro"
            mindsetSpeedBoost = 1.10f; // 10% faster top speed
            mindsetAccelBoost = 1.20f; // 20% punchier acceleration
        }
        else if (currentPosition == totalKarts && totalKarts > 1) // Last place - Catch Up
        {
            // "Preciso acelerar estou em ultimo"
            mindsetSpeedBoost = 1.16f; // 16% faster top speed
            mindsetAccelBoost = 1.25f; // 25% faster acceleration
        }
        else // Middle positions - Maintaining
        {
            // "Estou em uma posição boa preciso manter"
            mindsetSpeedBoost = 1.02f;
            mindsetAccelBoost = 1.05f;
        }
        if (absSpeed > 4.5f)
        {
            aiWaypointTimeoutTimer = 0f;
            aiReverseCount = 0;
        }

        // Radius-based stuck check: if the AI stays inside a 12-meter radius for more than 4.5 seconds, it is stuck
        float distanceFromAnchor = Vector3.Distance(transform.position, stuckRadiusAnchor);
        if (distanceFromAnchor > 12.0f)
        {
            // Moved outside the stuck zone, update anchor and reset timer
            stuckRadiusAnchor = transform.position;
            stuckRadiusTimer = 0f;
        }
        else
        {
            stuckRadiusTimer += Time.deltaTime;
            if (stuckRadiusTimer >= 4.5f)
            {
                Debug.Log(gameObject.name + " stuck in a 12m radius for 4.5 seconds. Respawning.");
                RespawnAtClosestWaypoint();
                return;
            }
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

        // 2. Overtaking & Obstacle Avoidance (Coordinate check for karts + Whiskers for scenery)
        aiOvertakeTimer -= Time.deltaTime;
        if (aiOvertakeTimer <= 0f)
        {
            aiOvertakeSideOffset = 0f;
            
            // A. Check for other karts blocking us (NPCs or Player)
            KartController[] allKarts = Object.FindObjectsByType<KartController>(FindObjectsSortMode.None);
            bool foundKartToOvertake = false;
            foreach (var other in allKarts)
            {
                if (other == this) continue;

                Vector3 toOther = other.transform.position - transform.position;
                float distanceToOther = toOther.magnitude;

                // Check karts directly ahead in a 12-meter window
                if (distanceToOther > 1.2f && distanceToOther < 12.0f)
                {
                    Vector3 localPosOfOther = transform.InverseTransformPoint(other.transform.position);
                    
                    // If the kart is ahead (z > 0.5) and laterally in our lane (x within 2.5 meters)
                    if (localPosOfOther.z > 0.5f && Mathf.Abs(localPosOfOther.x) < 2.5f)
                    {
                        // Overtake on the opposite side of where they are positioned relative to us
                        float overtakeSide = localPosOfOther.x >= 0f ? -1.0f : 1.0f;
                        aiOvertakeDirection = overtakeSide;
                        aiOvertakeSideOffset = overtakeSide * Random.Range(2.5f, 3.5f); // Move aside by 2.5 to 3.5 meters
                        aiOvertakeTimer = Random.Range(0.6f, 1.2f);
                        foundKartToOvertake = true;
                        break;
                    }
                }
            }

            // B. If no karts are directly blocking, use standard whiskers for static environment obstacles
            if (!foundKartToOvertake)
            {
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
                else if (aiDifficulty == AIDifficulty.Dificil || 
                         aiDifficulty == AIDifficulty.Competitivo || 
                         aiDifficulty == AIDifficulty.CompetitivoB || 
                         aiDifficulty == AIDifficulty.CompetitivoA || 
                         aiDifficulty == AIDifficulty.CompetitivoF) 
                    maxNerf = 1.0f; // No nerfing/waiting for the player on competitive and hard modes!

                rubberbandMultiplier = Mathf.Lerp(1.0f, maxNerf, Mathf.Clamp01(distToPlayer / 35f));
            }
        }
        Vector3 localTargetPos = transform.InverseTransformPoint(targetPos);
        
        // Calculate steer input
        float angleToTarget = Mathf.Atan2(localTargetPos.x, localTargetPos.z) * Mathf.Rad2Deg;
        steeringInput = Mathf.Clamp(angleToTarget / 32f, -1f, 1f);

        // Throttle input: full speed forward, but slow down dynamically in sharp turns
        float activeSpeedAdaptation = aiSpeedAdaptation;
        float activeBrakeProximity = 15.0f;
        float activeBrakeStrength = 1.35f;

        if (aiDifficulty == AIDifficulty.Facil) 
        { 
            activeSpeedAdaptation = Mathf.Min(1.0f, aiSpeedAdaptation * 1.3f); 
            activeBrakeProximity = 18.0f; 
            activeBrakeStrength = 1.60f; // Braking early and safely
        }
        else if (aiDifficulty == AIDifficulty.Medio) 
        { 
            activeSpeedAdaptation = aiSpeedAdaptation * 0.65f; 
            activeBrakeProximity = 16.0f; 
            activeBrakeStrength = 1.35f; 
        }
        else if (aiDifficulty == AIDifficulty.Dificil) 
        { 
            activeSpeedAdaptation = aiSpeedAdaptation * 0.25f; 
            activeBrakeProximity = 13.0f; 
            activeBrakeStrength = 0.90f; 
        }
        else if (aiDifficulty == AIDifficulty.Adaptavel) 
        { 
            activeSpeedAdaptation = aiSpeedAdaptation * 0.5f; 
            activeBrakeProximity = 14.0f; 
            activeBrakeStrength = 1.10f; 
        }
        else if (aiDifficulty == AIDifficulty.Competitivo) 
        { 
            activeSpeedAdaptation = aiSpeedAdaptation * 0.3f; 
            activeBrakeProximity = 12.0f; 
            activeBrakeStrength = 0.80f; 
        }
        else if (aiDifficulty == AIDifficulty.CompetitivoB) 
        { 
            activeSpeedAdaptation = aiSpeedAdaptation * 0.4f; 
            activeBrakeProximity = 14.0f; 
            activeBrakeStrength = 1.00f; 
        }
        else if (aiDifficulty == AIDifficulty.CompetitivoA) 
        { 
            activeSpeedAdaptation = aiSpeedAdaptation * 0.2f; 
            activeBrakeProximity = 10.0f; 
            activeBrakeStrength = 0.50f; // Braking very late
        }
        else if (aiDifficulty == AIDifficulty.CompetitivoF) 
        { 
            activeSpeedAdaptation = aiSpeedAdaptation * 0.05f; 
            activeBrakeProximity = 5.0f; 
            activeBrakeStrength = 0.10f; // Barely brakes
        }

        // Look-Ahead Curve Detection (Pre-braking before entering the curve)
        float curveLookAheadBraking = 1.0f;
        int nextWpIdx = currentWaypointIndex;
        if (waypointCircuit != null && waypointCircuit.waypoints != null && waypointCircuit.waypoints.Length > 0)
        {
            int W = waypointCircuit.waypoints.Length;
            int lookAheadWpIdx = (currentWaypointIndex + 1) % W;
            if (nextWpIdx >= 0 && nextWpIdx < W && waypointCircuit.waypoints[nextWpIdx] != null && waypointCircuit.waypoints[lookAheadWpIdx] != null)
            {
                Vector3 dirToCurrent = (waypointCircuit.waypoints[nextWpIdx].position - transform.position).normalized;
                Vector3 dirNextSegment = (waypointCircuit.waypoints[lookAheadWpIdx].position - waypointCircuit.waypoints[nextWpIdx].position).normalized;
                
                float angleDifference = Vector3.Angle(dirToCurrent, dirNextSegment);
                if (angleDifference > 18.0f) // If the upcoming segment turns by more than 18 degrees
                {
                    float distToCurveEntry = Vector3.Distance(transform.position, waypointCircuit.waypoints[nextWpIdx].position);
                    if (distToCurveEntry < activeBrakeProximity)
                    {
                        float curveSeverity = Mathf.Clamp01((angleDifference - 18.0f) / 65.0f); // 0 to 1
                        float proximity = 1f - Mathf.Clamp01(distToCurveEntry / activeBrakeProximity); // 0 to 1
                        
                        // Pre-brake factor: can go negative (acting as active physical braking) for sharp curves
                        curveLookAheadBraking = 1f - (curveSeverity * proximity * activeBrakeStrength);
                    }
                }
            }
        }

        float speedFactor = Mathf.Max(1f - (Mathf.Abs(steeringInput) * activeSpeedAdaptation), 0.35f);
        throttleInput = speedFactor * rubberbandMultiplier;
        
        // Apply look-ahead pre-braking constraint
        if (curveLookAheadBraking < 1.0f)
        {
            throttleInput = Mathf.Min(throttleInput, curveLookAheadBraking);
        }

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
        else if (aiDifficulty == AIDifficulty.CompetitivoB)
        {
            if (playerKartCached != null)
            {
                float playerSpeed = playerKartCached.rb != null ? playerKartCached.rb.linearVelocity.magnitude : playerKartCached.currentSpeed;
                float dist = Vector3.Distance(transform.position, playerKartCached.transform.position);
                Vector3 localPlayerPos = transform.InverseTransformPoint(playerKartCached.transform.position);
                if (localPlayerPos.z > 0f) // Player is ahead
                {
                    activeMaxSpeed = playerSpeed + 1.5f + Mathf.Clamp(dist * 1.0f, 0f, 10f);
                }
                else // Player is behind
                {
                    activeMaxSpeed = Mathf.Max(playerSpeed + 1.5f, maxSpeed * 1.30f);
                }
                activeMaxSpeed = Mathf.Clamp(activeMaxSpeed, maxSpeed * 0.8f, maxSpeed * 1.35f);
            }
        }
        else if (aiDifficulty == AIDifficulty.CompetitivoA)
        {
            if (playerKartCached != null)
            {
                float playerSpeed = playerKartCached.rb != null ? playerKartCached.rb.linearVelocity.magnitude : playerKartCached.currentSpeed;
                float dist = Vector3.Distance(transform.position, playerKartCached.transform.position);
                Vector3 localPlayerPos = transform.InverseTransformPoint(playerKartCached.transform.position);
                if (localPlayerPos.z > 0f) // Player is ahead
                {
                    activeMaxSpeed = playerSpeed + 4.0f + Mathf.Clamp(dist * 1.5f, 0f, 18f);
                }
                else // Player is behind
                {
                    activeMaxSpeed = Mathf.Max(playerSpeed + 3.0f, maxSpeed * 1.42f);
                }
                activeMaxSpeed = Mathf.Clamp(activeMaxSpeed, maxSpeed * 0.9f, maxSpeed * 1.50f);
            }
        }
        else if (aiDifficulty == AIDifficulty.CompetitivoF)
        {
            if (playerKartCached != null)
            {
                float playerSpeed = playerKartCached.rb != null ? playerKartCached.rb.linearVelocity.magnitude : playerKartCached.currentSpeed;
                float dist = Vector3.Distance(transform.position, playerKartCached.transform.position);
                Vector3 localPlayerPos = transform.InverseTransformPoint(playerKartCached.transform.position);
                if (localPlayerPos.z > 0f) // Player is ahead
                {
                    activeMaxSpeed = playerSpeed + 8.0f + (dist * 2.0f);
                }
                else // Player is behind
                {
                    if (dist < 4.0f)
                    {
                        activeMaxSpeed = playerSpeed + 4.5f;
                    }
                    else
                    {
                        activeMaxSpeed = Mathf.Max(playerSpeed + 3.5f, maxSpeed * 1.60f);
                    }
                }
                activeMaxSpeed = Mathf.Clamp(activeMaxSpeed, maxSpeed * 1.0f, maxSpeed * 2.50f);
            }
        }

        bool speedIsEnough = rb != null && rb.linearVelocity.magnitude > (activeMaxSpeed * 0.35f);
        if (isDrifting)
        {
            isDrifting = Mathf.Abs(steeringInput) > 0.5f && speedIsEnough && isGrounded;
            if (isDrifting && steeringInput != 0f)
            {
                driftDirection = steeringInput > 0f ? 1f : -1f;
            }
        }
        else
        {
            // Lower drift steering threshold if pre-braking to enter drift easily in curves
            float steerThreshold = (curveLookAheadBraking < 0.9f) ? 0.45f : 0.85f;
            isDrifting = Mathf.Abs(steeringInput) > steerThreshold && speedIsEnough && isGrounded && !aiIsReversing;
            driftDirection = 0f;
        }

        // AI Boost Activation Logic
        if (currentBoostScore >= boostActivateCost && aiBoostCooldownTimer <= 0f && isGrounded && throttleInput > 0.8f && nitroBoostTimer <= 0f)
        {
            // Evaluate if we are on a straight line to use the boost safely
            bool isStraightLine = false;
            if (waypointCircuit != null && waypointCircuit.waypoints != null && waypointCircuit.waypoints.Length > 0)
            {
                int W = waypointCircuit.waypoints.Length;
                int currentWp = currentWaypointIndex;
                int nextWp = (currentWp + 1) % W;
                int afterNextWp = (nextWp + 1) % W;

                if (waypointCircuit.waypoints[currentWp] != null && waypointCircuit.waypoints[nextWp] != null && waypointCircuit.waypoints[afterNextWp] != null)
                {
                    Vector3 toCurrentWp = (waypointCircuit.waypoints[currentWp].position - transform.position).normalized;
                    Vector3 toNextWp = (waypointCircuit.waypoints[nextWp].position - waypointCircuit.waypoints[currentWp].position).normalized;
                    Vector3 toAfterNextWp = (waypointCircuit.waypoints[afterNextWp].position - waypointCircuit.waypoints[nextWp].position).normalized;

                    float angle1 = Vector3.Angle(transform.forward, toCurrentWp);
                    float angle2 = Vector3.Angle(toCurrentWp, toNextWp);
                    float angle3 = Vector3.Angle(toNextWp, toAfterNextWp);

                    // If all angle differences are small, it's a straight line section!
                    // Let's also verify that we aren't steering heavily right now (Mathf.Abs(steeringInput) < 0.25f)
                    if (angle1 < 22f && angle2 < 20f && angle3 < 20f && Mathf.Abs(steeringInput) < 0.25f)
                    {
                        isStraightLine = true;
                    }
                }
            }

            // Decide to boost based on difficulty and strategic conditions
            if (isStraightLine)
            {
                bool shouldBoost = false;
                
                // Diff-based usage probability and conditions
                if (aiDifficulty == AIDifficulty.Facil)
                {
                    // Easy AI: 10% chance to boost on straight line, long cooldown
                    if (Random.value < 0.10f)
                    {
                        shouldBoost = true;
                        aiBoostCooldownTimer = Random.Range(12f, 18f);
                    }
                }
                else if (aiDifficulty == AIDifficulty.Medio)
                {
                    // Medium AI: 40% chance, medium cooldown
                    if (Random.value < 0.40f)
                    {
                        shouldBoost = true;
                        aiBoostCooldownTimer = Random.Range(6f, 10f);
                    }
                }
                else
                {
                    // Hard / Competitive / Adaptive AI: 85% chance to boost immediately when on a straight line
                    shouldBoost = true;
                    aiBoostCooldownTimer = Random.Range(3.5f, 6.0f);
                }

                if (shouldBoost)
                {
                    currentBoostScore -= boostActivateCost;
                    nitroBoostTimer = 2.0f;
                    Debug.Log(gameObject.name + " (" + aiDifficulty + ") ACTIVATED AI NITRO BOOST on straight line! Remaining: " + currentBoostScore);
                }
            }
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

        // Check for Drift Hop or Simple Hop (Jump!) - Ground hop force removed to make Space exclusive to starting a drift instantly from the ground
        if (isPlayer && jumpJustPressed && isGrounded)
        {
            jumpJustPressed = false; // Consume jump event
        }
        wasDrifting = isDrifting;

        // Synchronize our internal tracking speed with the actual Rigidbody speed!
        if (rb != null)
        {
            if (isGrounded)
            {
                float physicalSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
                if (isDrifting && throttleInput > 0.1f)
                {
                    // Prevent ground friction from slowing down the kart to 0 during drift
                    float driftDrag = 2.0f * Time.fixedDeltaTime; // small speed decay
                    currentSpeed = Mathf.Max(physicalSpeed, currentSpeed - driftDrag);
                }
                else
                {
                    currentSpeed = physicalSpeed;
                }
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

        // Apply Nitro Boost timer update
        if (nitroBoostTimer > 0f)
        {
            nitroBoostTimer -= Time.fixedDeltaTime;
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
            else if (aiDifficulty == AIDifficulty.CompetitivoB)
            {
                if (playerKartCached != null)
                {
                    float playerSpeed = playerKartCached.rb != null ? playerKartCached.rb.linearVelocity.magnitude : playerKartCached.currentSpeed;
                    float dist = Vector3.Distance(transform.position, playerKartCached.transform.position);
                    Vector3 localPlayerPos = transform.InverseTransformPoint(playerKartCached.transform.position);
                    
                    if (localPlayerPos.z > 0f) // Player is ahead
                    {
                        activeMaxSpeed = playerSpeed + 1.5f + Mathf.Clamp(dist * 1.0f, 0f, 10f);
                    }
                    else // Player is behind
                    {
                        activeMaxSpeed = Mathf.Max(playerSpeed + 1.5f, maxSpeed * 1.30f);
                    }
                    
                    activeMaxSpeed = Mathf.Clamp(activeMaxSpeed, maxSpeed * 0.8f, maxSpeed * 1.35f);
                    activeAcceleration = acceleration * 1.35f;
                }
                else
                {
                    activeMaxSpeed = maxSpeed * 1.30f;
                    activeAcceleration = acceleration * 1.35f;
                }
            }
            else if (aiDifficulty == AIDifficulty.CompetitivoA)
            {
                if (playerKartCached != null)
                {
                    float playerSpeed = playerKartCached.rb != null ? playerKartCached.rb.linearVelocity.magnitude : playerKartCached.currentSpeed;
                    float dist = Vector3.Distance(transform.position, playerKartCached.transform.position);
                    Vector3 localPlayerPos = transform.InverseTransformPoint(playerKartCached.transform.position);
                    
                    if (localPlayerPos.z > 0f) // Player is ahead
                    {
                        activeMaxSpeed = playerSpeed + 4.0f + Mathf.Clamp(dist * 1.5f, 0f, 18f);
                    }
                    else // Player is behind
                    {
                        activeMaxSpeed = Mathf.Max(playerSpeed + 3.0f, maxSpeed * 1.42f);
                    }
                    
                    activeMaxSpeed = Mathf.Clamp(activeMaxSpeed, maxSpeed * 0.9f, maxSpeed * 1.50f);
                    activeAcceleration = acceleration * 1.48f;
                }
                else
                {
                    activeMaxSpeed = maxSpeed * 1.42f;
                    activeAcceleration = acceleration * 1.48f;
                }
            }
            else if (aiDifficulty == AIDifficulty.CompetitivoF)
            {
                if (playerKartCached != null)
                {
                    float playerSpeed = playerKartCached.rb != null ? playerKartCached.rb.linearVelocity.magnitude : playerKartCached.currentSpeed;
                    float dist = Vector3.Distance(transform.position, playerKartCached.transform.position);
                    Vector3 localPlayerPos = transform.InverseTransformPoint(playerKartCached.transform.position);
                    
                    if (localPlayerPos.z > 0f) // Player is ahead
                    {
                        activeMaxSpeed = playerSpeed + 8.0f + (dist * 2.0f);
                    }
                    else // Player is behind
                    {
                        if (dist < 4.0f)
                        {
                            activeMaxSpeed = playerSpeed + 4.5f;
                        }
                        else
                        {
                            activeMaxSpeed = Mathf.Max(playerSpeed + 3.5f, maxSpeed * 1.60f);
                        }
                    }
                    
                    activeMaxSpeed = Mathf.Clamp(activeMaxSpeed, maxSpeed * 1.0f, maxSpeed * 2.50f);
                    activeAcceleration = acceleration * 2.50f;
                }
                else
                {
                    activeMaxSpeed = maxSpeed * 1.60f;
                    activeAcceleration = acceleration * 2.50f;
                }
            }
        }

        // Apply Mindset modifiers (NPCs only) after all difficulty calculations
        if (!isPlayer)
        {
            activeMaxSpeed *= mindsetSpeedBoost;
            activeAcceleration *= mindsetAccelBoost;
        }

        // Apply Nitro Boost modifier to both Player and AI (after difficulty/mindset speed calculations)
        if (nitroBoostTimer > 0f)
        {
            activeMaxSpeed *= 1.55f; // Nitro speed multiplier
            activeAcceleration *= 2.2f; // Much stronger acceleration during nitro
        }

        if (isGrounded)
        {
            bool isBrakeDrifting = isDrifting && throttleInput <= 0.1f;
            
            if (isBrakeDrifting)
            {
                // Brake-Drift speed capping: limit speed and apply deceleration
                float speedCapFactor = isPlayer ? 0.40f : 0.52f; // Keep a bit more speed for AI to remain competitive
                targetForwardSpeed = activeMaxSpeed * speedCapFactor; 
                currentAccel = deceleration * (isPlayer ? 2.0f : 2.5f); // AI decelerates aggressively to hold the inner curve line
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
                // Physical drift steering: turn the actual Rigidbody slower so the kart slides in a wide arc
                // rather than spinning/rotating rapidly on its own axis (which causes donut/zerinho loops)
                float steerFactor = isPlayer 
                    ? (0.35f + (steeringInput * driftDirection) * 0.35f) // ranges from 0.00f (flat slide) to 0.70f (sharp turn) for player
                    : (0.35f + (steeringInput * driftDirection) * 0.25f); // AI keeps default behavior for pathing
                
                // Brake-Drift: if not accelerating (throttleInput <= 0.1f) during drift, slightly scale steer rate
                bool isBrakeDrifting = isDrifting && throttleInput <= 0.1f;
                if (isBrakeDrifting)
                {
                    steerFactor *= isPlayer ? 1.35f : 2.2f; 
                }

                float playerSteerLimit = driftPhysicalSteerLimit;
                if (isPlayer)
                {
                    bool isHoldingSpace = false;
                    if (driftAction != null && driftAction.enabled) isHoldingSpace = driftAction.IsPressed();
                    if (Keyboard.current != null) isHoldingSpace = isHoldingSpace || Keyboard.current.spaceKey.isPressed;

                    if (!isHoldingSpace)
                    {
                        // Reduce turn speed by 60% when Space is released, opening up the drift curve dynamically!
                        playerSteerLimit *= 0.40f; 
                    }
                }
                // AI gets 2x tighter turning limit (2.0f instead of 1.0f) to remain competitive and hold paths precisely
                float actualSteerSpeed = steeringSpeed * (isPlayer ? playerSteerLimit : 2.0f) * driftSteerMultiplier * steerFactor;

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
        Quaternion newRotation = Quaternion.Slerp(rb.rotation, targetRot, Time.fixedDeltaTime * (isGrounded ? 15f : 10f));
        rb.MoveRotation(newRotation);

        // Derive updated orientation vectors directly from the new rotation to avoid a 1-frame alignment delay
        Vector3 newForward = newRotation * Vector3.forward;
        Vector3 newRight = newRotation * Vector3.right;
        Vector3 newUp = newRotation * Vector3.up;

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

            // Lerp the grip value. Exiting drift recovers grip quickly (10.0f) for a crisp, instant snap back to normal traction.
            float gripSpeed = isDrifting ? 15f : 10.0f;
            currentGripValue = Mathf.Lerp(currentGripValue, targetGrip, Time.fixedDeltaTime * gripSpeed);

            // We want our forward velocity along newForward to match currentSpeed
            float forwardVel = currentSpeed;

            // We want our sideways velocity along newRight (sliding) to be damped towards 0 based on grip
            float sidewaysVel = Vector3.Dot(rb.linearVelocity, newRight);
            float targetSidewaysVel = 0f;

            // Apply centrifugal slide push during drift
            if (isDrifting && driftDirection != 0f)
            {
                // Drift push force: slide outwards from the locked drift direction
                // Scale slide magnitude dynamically: counter-steer reduces sliding, steer-in slides wider!
                float steerSlideFactor = driftSlipFactor + (steeringInput * driftDirection) * driftSlipSteerInfluence;
                float driftPush = -driftDirection * currentSpeed * steerSlideFactor;
                targetSidewaysVel = driftPush;
            }

            float newSidewaysVel = Mathf.Lerp(sidewaysVel, targetSidewaysVel, currentGripValue * Time.fixedDeltaTime * 50f);

            // We want to preserve our vertical velocity along newUp so gravity, jumps, and ramps act naturally
            float verticalVel = Vector3.Dot(rb.linearVelocity, newUp);

            // Reassemble the velocity vector in local space and assign to Rigidbody
            rb.linearVelocity = newForward * forwardVel + newRight * newSidewaysVel + newUp * verticalVel;
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
        stuckRadiusAnchor = transform.position;
        stuckRadiusTimer = 0f;

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

    // Public method to activate a nitro speed boost
    public void ActivateNitroBoost(float duration = 2.0f)
    {
        nitroBoostTimer = duration;
        Debug.Log(gameObject.name + " activated boost for " + duration + "s.");
    }

    // Public method to add boost charges (1, 2, or 3/full)
    public void AddBoostCharges(int chargesCount)
    {
        if (chargesCount <= 0) return;

        float amountToAdd = 0f;
        if (chargesCount == 1)
        {
            amountToAdd = boostActivateCost;
            currentBoostScore = Mathf.Min(currentBoostScore + amountToAdd, maxBoostScore);
            Debug.Log($"{gameObject.name} obtained 1 Boost Charge (+{amountToAdd} score). Current: {currentBoostScore}");
        }
        else if (chargesCount == 2)
        {
            amountToAdd = boostActivateCost * 2f;
            currentBoostScore = Mathf.Min(currentBoostScore + amountToAdd, maxBoostScore);
            Debug.Log($"{gameObject.name} obtained 2 Boost Charges (+{amountToAdd} score). Current: {currentBoostScore}");
        }
        else if (chargesCount >= 3)
        {
            currentBoostScore = maxBoostScore;
            Debug.Log($"{gameObject.name} fully charged the Boost Bar! Current: {currentBoostScore}");
        }
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


    private void UpdateParticles()
    {
        // 1. Drift Particles: active when drifting and grounded
        bool shouldPlayDrift = isDrifting && isGrounded;
        if (driftParticles != null)
        {
            for (int i = 0; i < driftParticles.Length; i++)
            {
                var ps = driftParticles[i];
                if (ps != null)
                {
                    if (shouldPlayDrift)
                    {
                        if (!ps.isPlaying) ps.Play();
                    }
                    else
                    {
                        if (ps.isPlaying) ps.Stop();
                    }
                }
            }
        }

        // 2. Boost Particles: active when the kart has a speed boost (drift boost or nitro trigger boost)
        bool shouldPlayBoost = (nitroBoostTimer > 0f) || (activeBoostTimer > 0f);
        if (boostParticles != null)
        {
            for (int i = 0; i < boostParticles.Length; i++)
            {
                var ps = boostParticles[i];
                if (ps != null)
                {
                    if (shouldPlayBoost)
                    {
                        if (!ps.isPlaying) ps.Play();
                    }
                    else
                    {
                        if (ps.isPlaying) ps.Stop();
                    }
                }
            }
        }
    }

    private void UpdateAudio()
    {
        // 1. Drift Audio: play/fade when drifting and grounded
        bool shouldPlayDriftAudio = isDrifting && isGrounded && Mathf.Abs(currentSpeed) > 1.0f;
        if (driftAudioSource != null)
        {
            if (shouldPlayDriftAudio)
            {
                if (!driftAudioSource.isPlaying)
                {
                    driftAudioSource.volume = 0f;
                    if (driftAudioSource.clip != null && driftAudioSource.clip.length > 0f)
                    {
                        driftAudioSource.time = Random.Range(0f, driftAudioSource.clip.length);
                    }
                    driftAudioSource.pitch = driftBasePitch * Random.Range(0.92f, 1.08f);
                    driftAudioSource.Play();
                }
                driftAudioSource.volume = Mathf.MoveTowards(driftAudioSource.volume, maxDriftVolume, driftFadeSpeed * Time.deltaTime);
            }
            else
            {
                if (driftAudioSource.isPlaying)
                {
                    driftAudioSource.volume = Mathf.MoveTowards(driftAudioSource.volume, 0f, driftFadeSpeed * Time.deltaTime);
                    if (driftAudioSource.volume <= 0.01f)
                    {
                        driftAudioSource.Stop();
                        driftAudioSource.volume = 0f;
                    }
                }
            }
        }

        // 2. Boost Audio: play/fade when boost is active
        bool shouldPlayBoostAudio = (nitroBoostTimer > 0f) || (activeBoostTimer > 0f);
        if (boostAudioSource != null)
        {
            if (shouldPlayBoostAudio)
            {
                if (!boostAudioSource.isPlaying)
                {
                    boostAudioSource.volume = 0f;
                    if (boostAudioSource.clip != null && boostAudioSource.clip.length > 0f)
                    {
                        boostAudioSource.time = Random.Range(0f, boostAudioSource.clip.length);
                    }
                    boostAudioSource.pitch = boostBasePitch * Random.Range(0.95f, 1.05f);
                    boostAudioSource.Play();
                }
                boostAudioSource.volume = Mathf.MoveTowards(boostAudioSource.volume, maxBoostVolume, boostFadeSpeed * Time.deltaTime);
            }
            else
            {
                if (boostAudioSource.isPlaying)
                {
                    boostAudioSource.volume = Mathf.MoveTowards(boostAudioSource.volume, 0f, boostFadeSpeed * Time.deltaTime);
                    if (boostAudioSource.volume <= 0.01f)
                    {
                        boostAudioSource.Stop();
                        boostAudioSource.volume = 0f;
                    }
                }
            }
        }
    }

    private void UpdateBoostHUD()
    {
        if (boostIcons == null || boostIcons.Length == 0) return;

        int activeCharges = Mathf.FloorToInt(currentBoostScore / boostActivateCost);

        for (int i = 0; i < boostIcons.Length; i++)
        {
            if (boostIcons[i] != null)
            {
                boostIcons[i].SetActive(i < activeCharges);
            }
        }
    }

    public float SteeringInput
    {
        get { return steeringInput; }
    }

    public bool IsBoosting
    {
        get { return (nitroBoostTimer > 0f) || (activeBoostTimer > 0f); }
    }

    private void OnCollisionEnter(Collision collision)
    {
        float force = collision.relativeVelocity.magnitude;
        if (force < minCollisionForce) return;

        // Instantiate sparks at contact point
        if (collisionSparksPrefab != null && collision.contactCount > 0)
        {
            ContactPoint contact = collision.GetContact(0);
            Vector3 point = contact.point;
            Vector3 normal = contact.normal;

            Quaternion rotation = Quaternion.LookRotation(normal);
            ParticleSystem sparks = Instantiate(collisionSparksPrefab, point, rotation);

            Destroy(sparks.gameObject, sparks.main.duration + sparks.main.startLifetime.constantMax);
        }

        // Only shake the camera if this is the player's kart
        if (isPlayer)
        {
            CameraController cameraCtrl = Object.FindAnyObjectByType<CameraController>();
            if (cameraCtrl != null && cameraCtrl.target == this)
            {
                float t = Mathf.InverseLerp(minCollisionForce, maxCollisionForce, force);
                float intensity = Mathf.Lerp(0.1f, maxShakeIntensity, t);
                cameraCtrl.TriggerShake(intensity, shakeDuration);
            }
        }
    }
}
