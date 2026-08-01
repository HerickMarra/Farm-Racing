using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class KartController : MonoBehaviour
{
    public static List<KartController> ActiveKarts = new List<KartController>();

    private static void CleanupActiveKarts()
    {
        for (int i = ActiveKarts.Count - 1; i >= 0; i--)
        {
            if (ActiveKarts[i] == null)
            {
                ActiveKarts.RemoveAt(i);
            }
        }
    }

    [Header("Special System")]
    public bool hasSpecial = false;
    public SpecialAbility currentSpecial;
    public UnityEngine.UI.Image specialIconImage;
    private float stunTimer = 0f;
    public bool isStunned => stunTimer > 0f;

    // Decoupled targeting/lock-on component (auto-resolved in Start).
    private KartTargetingSystem targetingSystem;
    public KartTargetingSystem TargetingSystem => targetingSystem;

    private void OnEnable()
    {
        CleanupActiveKarts();
        if (!ActiveKarts.Contains(this))
        {
            ActiveKarts.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveKarts.Remove(this);
        CleanupActiveKarts();
    }

    private void OnDestroy()
    {
        ActiveKarts.Remove(this);
        CleanupActiveKarts();
    }

    [Header("Control Settings")]
    [Tooltip("If true, the player controls this kart. If false, the AI controls it.")]
    public bool isPlayer = false;
    [Tooltip("ID do carro para corresponder ao ID selecionado nos Cards da UI.")]
    public int carID = 0;
    [Tooltip("Define se este é o carrinho padrão do player (que inicia em último lugar/grid correto do player).")]
    public bool isDefaultPlayerKart = false;

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
    [Tooltip("Minimum collision speed/force to trigger sparks.")]
    public float minCollisionForce = 4f;

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

    // Cached References
    private RaceManager cachedRaceManager;
    private Transform cachedTransform;
    private Transform cachedRootTransform;

    /// <summary>
    /// Returns the progress of the kart relative to the race finish line (0 = start, 1 = finish).
    /// </summary>
    public float GetRaceFinishProgress()
    {
        if (cachedRaceManager == null)
        {
            cachedRaceManager = Object.FindAnyObjectByType<RaceManager>();
        }
        int maxLaps = cachedRaceManager != null ? cachedRaceManager.totalLaps : 3;

        // Calculate lap progress
        float lapProgress = (float)(currentLap - 1) / maxLaps;

        // Calculate waypoint progress within the current lap
        float waypointProgress = 0f;
        if (waypointCircuit != null && waypointCircuit.waypoints != null && waypointCircuit.waypoints.Length > 0)
        {
            waypointProgress = (float)currentWaypointIndex / (waypointCircuit.waypoints.Length * maxLaps);
        }

        return Mathf.Clamp01(lapProgress + waypointProgress);
    }

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
    private readonly RaycastHit[] groundHits = new RaycastHit[8];
    private readonly RaycastHit[] suspensionHits = new RaycastHit[8];

    // Particle state tracking (avoids redundant Play/Stop calls)
    private bool currentDriftParticleState = false;
    private bool currentBoostParticleState = false;
    private int lastActiveBoostCharges = -1;

    // Spark particle pool (0 GC alloc on collisions)
    private static readonly List<ParticleSystem> sparksPool = new List<ParticleSystem>();

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

    // Static keywords for zero-alloc scenery obstacle checks
    private static readonly string[] SceneryKeywords = new string[]
    {
        "wood", "fence", "feno", "mapa", "wall", "colision", "collider"
    };

    private static readonly string[] IgnoredKeywords = new string[]
    {
        "road", "pista", "ground", "chao", "terrain"
    };

    private void Awake()
    {
        cachedTransform = transform;
        cachedRootTransform = cachedTransform.root;

        // Synchronize physics to 60 Hz to eliminate temporal beat-aliasing stutters.
        // At 30 FPS, this guarantees a perfect 2:1 ratio (exactly 2 physics updates per render frame).
        Time.fixedDeltaTime = 0.0166667f;

        // Obtém o ID salvo no PlayerPrefs (se não existir, usa 1 como valor padrão)
        int savedID = PlayerPrefs.GetInt("SelectedCarID", 1);
        isPlayer = (savedID == carID);

        // Automatically set the tag to "Player" if isPlayer is active
        if (isPlayer)
        {
            gameObject.tag = "Player";
        }
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        cachedRaceManager = Object.FindAnyObjectByType<RaceManager>();

        if (driftAudioSource != null) driftBasePitch = driftAudioSource.pitch;
        if (boostAudioSource != null) boostBasePitch = boostAudioSource.pitch;

        // Resolve the decoupled targeting/lock-on system (add one if missing so the Special always works).
        targetingSystem = GetComponent<KartTargetingSystem>();
        if (targetingSystem == null)
        {
            targetingSystem = gameObject.AddComponent<KartTargetingSystem>();
        }
        
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

        // Create and apply a frictionless PhysicMaterial to the main kart collider.
        // This prevents Unity's default physics friction from fighting with our custom linearVelocity script updates,
        // eliminating micro-stutters and sliding jitters on uneven road polygons.
        PhysicsMaterial frictionlessMaterial = new PhysicsMaterial("FrictionlessKartMaterial")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounciness = 0f,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };
        Collider mainCollider = GetComponent<Collider>();
        if (mainCollider != null)
        {
            mainCollider.material = frictionlessMaterial;
        }

        // Try to auto-find WaypointCircuit if not assigned
        if (waypointCircuit == null)
        {
            waypointCircuit = Object.FindAnyObjectByType<WaypointCircuit>();
        }

        // Find closest waypoint on start to prevent backtracking
        RecalculateClosestWaypoint();

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
        visualsGo.transform.SetParent(cachedTransform, false);
        visualsGo.transform.localPosition = Vector3.zero;
        visualsGo.transform.localRotation = Quaternion.identity;
        visualsGo.transform.localScale = Vector3.one;

        // Move all other children into the Visuals container
        List<Transform> childrenToMove = new List<Transform>();
        for (int i = 0; i < cachedTransform.childCount; i++)
        {
            Transform child = cachedTransform.GetChild(i);
            if (child != visualsGo.transform)
            {
                childrenToMove.Add(child);
            }
        }

        for (int i = 0; i < childrenToMove.Count; i++)
        {
            childrenToMove[i].SetParent(visualsGo.transform, true);
        }

        // We set the bodyTransform to the Visuals container so the entire visual assembly (mesh + bones) leans together!
        bodyTransform = visualsGo.transform;

        // Cache initial local rotations and positions of the wheels (after parenting is completed)
        if (frontLeftWheel != null) { flInitialRot = frontLeftWheel.localRotation; flInitialPos = frontLeftWheel.localPosition; }
        if (frontRightWheel != null) { frInitialRot = frontRightWheel.localRotation; frInitialPos = frontRightWheel.localPosition; }
        if (rearLeftWheel != null) { rlInitialRot = rearLeftWheel.localRotation; rlInitialPos = rearLeftWheel.localPosition; }
        if (rearRightWheel != null) { rrInitialRot = rearRightWheel.localRotation; rrInitialPos = rearRightWheel.localPosition; }

        smoothedGroundNormal = Vector3.up;
        Vector3 currentPos = cachedTransform.position;
        lastStuckCheckPosition = currentPos;
        stuckRadiusAnchor = currentPos;
        stuckRadiusTimer = 0f;

        // Stop all particles at start to ensure they begin in an inactive state
        if (driftParticles != null)
        {
            for (int i = 0; i < driftParticles.Length; i++)
            {
                if (driftParticles[i] != null) driftParticles[i].Stop();
            }
        }
        if (boostParticles != null)
        {
            for (int i = 0; i < boostParticles.Length; i++)
            {
                if (boostParticles[i] != null) boostParticles[i].Stop();
            }
        }
    }

    public void RecalculateClosestWaypoint()
    {
        if (waypointCircuit != null && waypointCircuit.waypoints != null && waypointCircuit.waypoints.Length > 0)
        {
            float closestSqrDist = float.MaxValue;
            int closestIdx = 0;
            Vector3 pos = cachedTransform.position;
            var waypoints = waypointCircuit.waypoints;

            for (int i = 0; i < waypoints.Length; i++)
            {
                Transform wp = waypoints[i];
                if (wp == null) continue;
                float sqrD = (pos - wp.position).sqrMagnitude;
                if (sqrD < closestSqrDist)
                {
                    closestSqrDist = sqrD;
                    closestIdx = i;
                }
            }
            // Target the waypoint immediately after the closest one to ensure we drive forward
            currentWaypointIndex = (closestIdx + 1) % waypoints.Length;
        }
    }

    private void resultLog(string message)
    {
        // helper to print logs safely
    }

    private void Update()
    {
        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
            throttleInput = 0f;
            steeringInput = 0f;
            isDrifting = false;
            smoothedThrottleInput = 0f;
            smoothedSteeringInput = 0f;
            UpdateWheelVisuals();
            UpdateParticles();
            UpdateAudio();
            return;
        }

        if (!controlsEnabled)
        {
            throttleInput = 0f;
            steeringInput = 0f;
            isDrifting = false;
            smoothedThrottleInput = 0f;
            smoothedSteeringInput = 0f;
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
        if (stunTimer > 0f)
        {
            if (bodyTransform != null)
            {
                float spinAngle = (stunTimer * 720f) % 360f;
                bodyTransform.localRotation = Quaternion.Euler(0f, spinAngle, 0f);
            }
            return;
        }

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
        if (stunTimer > 0f)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector3.MoveTowards(rb.linearVelocity, Vector3.zero, Time.fixedDeltaTime * 15f);
            }
            return;
        }
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

            // Special System Input: Q fires the Special at the locked target.
            if (hasSpecial && Keyboard.current.qKey.wasPressedThisFrame)
            {
                UseSpecial();
            }
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

        // Handle AI special item usage
        UpdateAISpecial();

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
        else if (aiDifficulty >= AIDifficulty.Competitivo) passiveCharge = 40f;

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
        int totalKarts = ActiveKarts.Count;
        mindsetSpeedBoost = 1.0f;
        mindsetAccelBoost = 1.0f;

        Vector3 myPos = cachedTransform.position;

        if (currentPosition == 1) // First place - Defending Lead
        {
            KartController secondPlaceKart = null;
            for (int i = 0; i < totalKarts; i++)
            {
                KartController k = ActiveKarts[i];
                if (k != null && k.currentPosition == 2)
                {
                    secondPlaceKart = k;
                    break;
                }
            }

            if (secondPlaceKart != null)
            {
                float sqrDistToSecond = (myPos - secondPlaceKart.transform.position).sqrMagnitude;
                if (sqrDistToSecond < 64.0f) // 8m * 8m
                {
                    float secondSpeed = secondPlaceKart.rb != null ? secondPlaceKart.rb.linearVelocity.magnitude : secondPlaceKart.currentSpeed;
                    mindsetSpeedBoost = Mathf.Max(1.0f, (secondSpeed + 2.0f) / maxSpeed);
                    mindsetAccelBoost = 1.25f;

                    Vector3 localSecondPos = cachedTransform.InverseTransformPoint(secondPlaceKart.transform.position);
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
            mindsetSpeedBoost = 1.10f;
            mindsetAccelBoost = 1.20f;
        }
        else if (currentPosition == totalKarts && totalKarts > 1) // Last place - Catch Up
        {
            mindsetSpeedBoost = 1.16f;
            mindsetAccelBoost = 1.25f;
        }
        else // Middle positions - Maintaining
        {
            mindsetSpeedBoost = 1.02f;
            mindsetAccelBoost = 1.05f;
        }

        if (absSpeed > 4.5f)
        {
            aiWaypointTimeoutTimer = 0f;
            aiReverseCount = 0;
        }

        // Radius-based stuck check: if the AI stays inside a 12-meter radius (144 sqrDist) for more than 4.5 seconds, it is stuck
        float sqrDistFromAnchor = (myPos - stuckRadiusAnchor).sqrMagnitude;
        if (sqrDistFromAnchor > 144.0f)
        {
            stuckRadiusAnchor = myPos;
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

        // 1. Stuck & Obstacle detection
        if (isGrounded && absSpeed < 1.2f && Mathf.Abs(throttleInput) > 0.15f)
        {
            aiStuckTimer += Time.deltaTime;
        }
        else
        {
            aiStuckTimer = Mathf.Max(0f, aiStuckTimer - Time.deltaTime * 0.6f);
        }

        // Auto-respawn if stuck for too long, fell off, or drifted too far
        float sqrDistToTargetWp = (myPos - targetPos).sqrMagnitude;
        
        aiWaypointTimeoutTimer += Time.deltaTime;
        bool isStuckTimeout = aiWaypointTimeoutTimer > 10.0f;
        bool isReverseLoopStuck = aiReverseCount >= 2;

        if (aiStuckTimer > aiMaxStuckTime || myPos.y < -10f || sqrDistToTargetWp > 4225f || isStuckTimeout || isReverseLoopStuck) // 65m * 65m = 4225
        {
            Debug.Log(gameObject.name + " detected stuck. Timeout: " + isStuckTimeout + ", Loop: " + isReverseLoopStuck + ", ReverseCount: " + aiReverseCount + ". Respawning.");
            RespawnAtClosestWaypoint();
            return;
        }

        if (aiStuckTimer > 2.0f && !aiIsReversing)
        {
            aiIsReversing = true;
            aiReverseDuration = Random.Range(1.2f, 1.8f);
            aiStuckTimer = 0.5f;
            aiReverseCount++;
        }

        if (aiIsReversing)
        {
            aiReverseDuration -= Time.deltaTime;
            throttleInput = -0.9f;
            Vector3 localTarget = cachedTransform.InverseTransformPoint(targetPos);
            steeringInput = localTarget.x >= 0f ? -0.8f : 0.8f; 

            if (aiReverseDuration <= 0f)
            {
                aiIsReversing = false;
            }
            return;
        }

        // 2. Overtaking & Obstacle Avoidance
        aiOvertakeTimer -= Time.deltaTime;
        if (aiOvertakeTimer <= 0f)
        {
            aiOvertakeSideOffset = 0f;
            bool foundKartToOvertake = false;

            for (int i = 0; i < totalKarts; i++)
            {
                KartController other = ActiveKarts[i];
                if (other == null || other == this) continue;

                Vector3 toOther = other.transform.position - myPos;
                float sqrDistToOther = toOther.sqrMagnitude;

                // Check karts directly ahead in a 1.2m to 12m window (1.44f to 144.0f sqrDist)
                if (sqrDistToOther > 1.44f && sqrDistToOther < 144.0f)
                {
                    Vector3 localPosOfOther = cachedTransform.InverseTransformPoint(other.transform.position);
                    
                    if (localPosOfOther.z > 0.5f && Mathf.Abs(localPosOfOther.x) < 2.5f)
                    {
                        float overtakeSide = localPosOfOther.x >= 0f ? -1.0f : 1.0f;
                        aiOvertakeDirection = overtakeSide;
                        aiOvertakeSideOffset = overtakeSide * Random.Range(2.5f, 3.5f);
                        aiOvertakeTimer = Random.Range(0.6f, 1.2f);
                        foundKartToOvertake = true;
                        break;
                    }
                }
            }

            if (!foundKartToOvertake)
            {
                Vector3 centerRayStart = myPos + Vector3.up * 0.85f;
                Vector3 myForward = cachedTransform.forward;
                Vector3 myRight = cachedTransform.right;

                Vector3 leftRayStart = centerRayStart - myRight * 0.5f;
                Vector3 rightRayStart = centerRayStart + myRight * 0.5f;
                
                float checkDistance = 9.0f;
                RaycastHit hit;
                bool hitObstacle = false;
                float obstacleOffsetDir = 0f;

                if (Physics.Raycast(centerRayStart, myForward, out hit, checkDistance, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (IsValidObstacle(hit))
                    {
                        hitObstacle = true;
                        Vector3 localHitPoint = cachedTransform.InverseTransformPoint(hit.point);
                        obstacleOffsetDir = localHitPoint.x >= 0f ? -1.3f : 1.3f;
                    }
                }

                if (!hitObstacle && Physics.Raycast(leftRayStart, Quaternion.Euler(0f, -18f, 0f) * myForward, out hit, checkDistance * 0.8f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (IsValidObstacle(hit))
                    {
                        hitObstacle = true;
                        obstacleOffsetDir = 1.3f;
                    }
                }

                if (!hitObstacle && Physics.Raycast(rightRayStart, Quaternion.Euler(0f, 18f, 0f) * myForward, out hit, checkDistance * 0.8f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (IsValidObstacle(hit))
                    {
                        hitObstacle = true;
                        obstacleOffsetDir = -1.3f;
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

        // 3. Driving Speed Adjustments & Rubberbanding (Cached player reference)
        playerCacheTimer -= Time.deltaTime;
        if (playerKartCached == null || playerCacheTimer <= 0f)
        {
            playerCacheTimer = 2.0f;
            for (int i = 0; i < totalKarts; i++)
            {
                KartController k = ActiveKarts[i];
                if (k != null && k.isPlayer) { playerKartCached = k; break; }
            }
        }

        float activeMaxSpeed = maxSpeed;

        if (aiDifficulty == AIDifficulty.Facil)
        {
            activeMaxSpeed = maxSpeed * 0.70f;
        }
        else if (aiDifficulty == AIDifficulty.Medio)
        {
            activeMaxSpeed = maxSpeed * 1.28f;
        }
        else if (aiDifficulty == AIDifficulty.Dificil)
        {
            activeMaxSpeed = maxSpeed * 1.45f;
        }
        else if (aiDifficulty == AIDifficulty.Adaptavel)
        {
            if (playerKartCached != null)
            {
                float playerSpeed = playerKartCached.rb != null ? playerKartCached.rb.linearVelocity.magnitude : playerKartCached.currentSpeed;
                float dist = Vector3.Distance(myPos, playerKartCached.transform.position);
                Vector3 localPlayerPos = cachedTransform.InverseTransformPoint(playerKartCached.transform.position);
                
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
                float dist = Vector3.Distance(myPos, playerKartCached.transform.position);
                Vector3 localPlayerPos = cachedTransform.InverseTransformPoint(playerKartCached.transform.position);
                
                if (localPlayerPos.z > 0f)
                {
                    activeMaxSpeed = playerSpeed + 3f + Mathf.Clamp(dist * 1.4f, 0f, 16f);
                }
                else
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
                float dist = Vector3.Distance(myPos, playerKartCached.transform.position);
                Vector3 localPlayerPos = cachedTransform.InverseTransformPoint(playerKartCached.transform.position);
                
                if (localPlayerPos.z > 0f)
                {
                    activeMaxSpeed = playerSpeed + 5.0f + (dist * 1.6f);
                }
                else
                {
                    if (dist < 6.0f)
                    {
                        activeMaxSpeed = playerSpeed + 3.0f;
                    }
                    else
                    {
                        activeMaxSpeed = Mathf.Max(playerSpeed + 2.5f, maxSpeed * 1.35f);
                    }
                }
                activeMaxSpeed = Mathf.Clamp(activeMaxSpeed, maxSpeed * 0.85f, maxSpeed * 1.70f);
            }
        }
        else if (aiDifficulty == AIDifficulty.CompetitivoA)
        {
            if (playerKartCached != null)
            {
                float playerSpeed = playerKartCached.rb != null ? playerKartCached.rb.linearVelocity.magnitude : playerKartCached.currentSpeed;
                float dist = Vector3.Distance(myPos, playerKartCached.transform.position);
                Vector3 localPlayerPos = cachedTransform.InverseTransformPoint(playerKartCached.transform.position);
                
                if (localPlayerPos.z > 0f)
                {
                    activeMaxSpeed = playerSpeed + 6.5f + (dist * 1.8f);
                }
                else
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
                float dist = Vector3.Distance(myPos, playerKartCached.transform.position);
                Vector3 localPlayerPos = cachedTransform.InverseTransformPoint(playerKartCached.transform.position);
                if (localPlayerPos.z > 0f)
                {
                    activeMaxSpeed = playerSpeed + 8.0f + (dist * 2.0f);
                }
                else
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

        // Apply mindset modifiers
        activeMaxSpeed *= mindsetSpeedBoost;

        // Steering towards target waypoint
        Vector3 targetDirection = (targetPos - myPos).normalized;
        Vector3 localTargetDir = cachedTransform.InverseTransformDirection(targetDirection);
        float targetSteer = Mathf.Clamp(localTargetDir.x * 2.5f, -1f, 1f);

        // Slow down in curves (Speed Adaptation)
        float curveAngle = Vector3.Angle(cachedTransform.forward, targetDirection);
        float curveLookAheadBraking = 1.0f;
        if (curveAngle > 18f)
        {
            float curveSeverity = Mathf.Clamp01((curveAngle - 18f) / 60f);
            curveLookAheadBraking = Mathf.Lerp(1.0f, 1.0f - (aiSpeedAdaptation * 0.45f), curveSeverity);
        }

        throttleInput = curveLookAheadBraking;
        steeringInput = targetSteer;

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
            float steerThreshold = (curveLookAheadBraking < 0.9f) ? 0.45f : 0.85f;
            isDrifting = Mathf.Abs(steeringInput) > steerThreshold && speedIsEnough && isGrounded && !aiIsReversing;
            driftDirection = 0f;
        }

        // AI Boost Activation Logic (Uses Dot product instead of Angle for zero-alloc speed)
        if (currentBoostScore >= boostActivateCost && aiBoostCooldownTimer <= 0f && isGrounded && throttleInput > 0.8f && nitroBoostTimer <= 0f)
        {
            bool isStraightLine = false;
            var waypoints = waypointCircuit.waypoints;
            if (waypoints != null && waypoints.Length > 0)
            {
                int W = waypoints.Length;
                int currentWp = currentWaypointIndex;
                int nextWp = (currentWp + 1) % W;
                int afterNextWp = (nextWp + 1) % W;

                if (waypoints[currentWp] != null && waypoints[nextWp] != null && waypoints[afterNextWp] != null)
                {
                    Vector3 toCurrentWp = (waypoints[currentWp].position - myPos).normalized;
                    Vector3 toNextWp = (waypoints[nextWp].position - waypoints[currentWp].position).normalized;
                    Vector3 toAfterNextWp = (waypoints[afterNextWp].position - waypoints[nextWp].position).normalized;

                    // Dot product thresholds corresponding to angles: 22° -> 0.9272f, 20° -> 0.9397f
                    float dot1 = Vector3.Dot(cachedTransform.forward, toCurrentWp);
                    float dot2 = Vector3.Dot(toCurrentWp, toNextWp);
                    float dot3 = Vector3.Dot(toNextWp, toAfterNextWp);

                    if (dot1 > 0.9272f && dot2 > 0.9397f && dot3 > 0.9397f && Mathf.Abs(steeringInput) < 0.25f)
                    {
                        isStraightLine = true;
                    }
                }
            }

            if (isStraightLine)
            {
                bool shouldBoost = false;
                
                if (aiDifficulty == AIDifficulty.Facil)
                {
                    if (Random.value < 0.10f)
                    {
                        shouldBoost = true;
                        aiBoostCooldownTimer = Random.Range(12f, 18f);
                    }
                }
                else if (aiDifficulty == AIDifficulty.Medio)
                {
                    if (Random.value < 0.40f)
                    {
                        shouldBoost = true;
                        aiBoostCooldownTimer = Random.Range(6f, 10f);
                    }
                }
                else
                {
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
        Transform hitT = hit.collider.transform;

        // Ignore ourselves or children
        if (hitT == cachedTransform || hitT.root == cachedRootTransform)
            return false;

        // Ignore road/ground. A valid obstacle has a steep or non-upwards normal (normal.y < 0.6f)
        if (hit.normal.y > 0.6f)
            return false;

        bool isOtherKart = hit.collider.attachedRigidbody != null && hit.collider.attachedRigidbody.TryGetComponent<KartController>(out _);
        if (isOtherKart) return true;

        string objName = hit.collider.gameObject.name;
        
        // Zero GC string search using OrdinalIgnoreCase (no lowercasing allocations)
        for (int i = 0; i < IgnoredKeywords.Length; i++)
        {
            if (objName.IndexOf(IgnoredKeywords[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
        }

        for (int i = 0; i < SceneryKeywords.Length; i++)
        {
            if (objName.IndexOf(SceneryKeywords[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private void UpdateWaypointTracking()
    {
        if (waypointCircuit == null || waypointCircuit.waypoints == null || waypointCircuit.waypoints.Length == 0)
            return;

        var waypoints = waypointCircuit.waypoints;
        int W = waypoints.Length;
        Vector3 pos = cachedTransform.position;

        // Find closest waypoint using sqrMagnitude (eliminates Mathf.Sqrt)
        float closestSqrDist = float.MaxValue;
        int closestIdx = 0;
        for (int i = 0; i < W; i++)
        {
            Transform wp = waypoints[i];
            if (wp == null) continue;
            float sqrD = (pos - wp.position).sqrMagnitude;
            if (sqrD < closestSqrDist)
            {
                closestSqrDist = sqrD;
                closestIdx = i;
            }
        }

        if (lastClosestIdx == -1)
        {
            lastClosestIdx = closestIdx;
        }

        if (closestIdx != lastClosestIdx)
        {
            float thresholdHigh = W * 0.7f;
            float thresholdLow = W * 0.3f;

            if (lastClosestIdx >= thresholdHigh && closestIdx <= thresholdLow)
            {
                currentLap++;
                Debug.Log(gameObject.name + " completed lap! New Lap: " + currentLap);
            }

            lastClosestIdx = closestIdx;
        }

        currentWaypointIndex = (closestIdx + 1) % W;
    }

    private void CheckGroundStatus()
    {
        int hitCount = Physics.RaycastNonAlloc(cachedTransform.position + Vector3.up * 0.5f, Vector3.down, groundHits, 1.8f, ~0, QueryTriggerInteraction.Ignore);
        isGrounded = false;
        RaycastHit closestHit = default;
        float closestDist = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = groundHits[i];
            Transform hitT = hit.collider.transform;

            if (hitT == cachedTransform || hitT.root == cachedRootTransform)
                continue;

            if (hit.collider.attachedRigidbody != null && hit.collider.attachedRigidbody.TryGetComponent<KartController>(out _))
                continue;

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
        smoothedSteeringInput = Mathf.MoveTowards(smoothedSteeringInput, steeringInput, steeringDamping * Time.fixedDeltaTime);
        smoothedThrottleInput = Mathf.MoveTowards(smoothedThrottleInput, throttleInput, throttleDamping * Time.fixedDeltaTime);

        if (driftHopCooldownTimer > 0f)
        {
            driftHopCooldownTimer -= Time.fixedDeltaTime;
        }

        if (isGrounded)
        {
            smoothedGroundNormal = Vector3.Slerp(smoothedGroundNormal, groundNormal, Time.fixedDeltaTime * 12f);
        }
        else
        {
            smoothedGroundNormal = Vector3.Slerp(smoothedGroundNormal, Vector3.up, Time.fixedDeltaTime * 5f);
        }

        if (isGrounded && hasStuntPerformed)
        {
            hasStuntPerformed = false;
            stuntSpinTime = 0f;
            activeBoostTimer = 1.2f;
            activeBoostMultiplier = 1.45f;
            Debug.Log("LANDING STUNT BOOST ACTIVATED!");
        }

        if (isPlayer && jumpJustPressed && isGrounded)
        {
            jumpJustPressed = false;
        }
        wasDrifting = isDrifting;

        Vector3 myForward = cachedTransform.forward;

        if (rb != null)
        {
            if (isGrounded)
            {
                float physicalSpeed = Vector3.Dot(rb.linearVelocity, myForward);
                if (isDrifting && throttleInput > 0.1f)
                {
                    float driftDrag = 2.0f * Time.fixedDeltaTime;
                    currentSpeed = Mathf.Max(physicalSpeed, currentSpeed - driftDrag);
                }
                else
                {
                    currentSpeed = physicalSpeed;
                }
            }
            else
            {
                Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                currentSpeed = horizontalVel.magnitude;
            }

            if (isGrounded)
            {
                Vector3 currentPos = cachedTransform.position;
                if (currentSpeed > 0.1f)
                {
                    RaycastHit wallHit;
                    if (Physics.Raycast(currentPos + Vector3.up * 0.45f, myForward, out wallHit, 1.2f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        Transform wallT = wallHit.collider.transform;
                        if (!wallHit.collider.isTrigger && wallT != cachedTransform && wallT.root != cachedRootTransform)
                        {
                            if (Vector3.Dot(myForward, wallHit.normal) < -0.5f)
                            {
                                currentSpeed = 0f;
                            }
                        }
                    }
                }
                else if (currentSpeed < -0.1f)
                {
                    RaycastHit wallHit;
                    if (Physics.Raycast(currentPos + Vector3.up * 0.45f, -myForward, out wallHit, 1.2f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        Transform wallT = wallHit.collider.transform;
                        if (!wallHit.collider.isTrigger && wallT != cachedTransform && wallT.root != cachedRootTransform)
                        {
                            if (Vector3.Dot(-myForward, wallHit.normal) < -0.5f)
                            {
                                currentSpeed = 0f;
                            }
                        }
                    }
                }
            }
        }

        if (activeBoostTimer > 0f)
        {
            activeBoostTimer -= Time.fixedDeltaTime;
        }
        else
        {
            activeBoostMultiplier = 1.0f;
        }

        if (nitroBoostTimer > 0f)
        {
            nitroBoostTimer -= Time.fixedDeltaTime;
        }

        float targetForwardSpeed = 0f;
        float currentAccel = acceleration;

        float activeMaxSpeed = maxSpeed;
        float activeAcceleration = acceleration;

        if (activeBoostTimer > 0f)
        {
            activeMaxSpeed *= activeBoostMultiplier;
            activeAcceleration *= 1.8f;
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
                activeMaxSpeed = maxSpeed * 1.28f;
                activeAcceleration = acceleration * 1.26f;
            }
            else if (aiDifficulty == AIDifficulty.Dificil)
            {
                activeMaxSpeed = maxSpeed * 1.45f;
                activeAcceleration = acceleration * 1.50f;
            }
            else if (aiDifficulty == AIDifficulty.Adaptavel)
            {
                if (playerKartCached != null)
                {
                    float playerSpeed = playerKartCached.rb != null ? playerKartCached.rb.linearVelocity.magnitude : playerKartCached.currentSpeed;
                    float dist = Vector3.Distance(cachedTransform.position, playerKartCached.transform.position);
                    Vector3 localPlayerPos = cachedTransform.InverseTransformPoint(playerKartCached.transform.position);
                    
                    if (localPlayerPos.z > 0f)
                    {
                        activeMaxSpeed = playerSpeed + Mathf.Clamp((dist - 6f) * 1.2f, -10f, 15f);
                    }
                    else
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
                    float dist = Vector3.Distance(cachedTransform.position, playerKartCached.transform.position);
                    Vector3 localPlayerPos = cachedTransform.InverseTransformPoint(playerKartCached.transform.position);
                    
                    if (localPlayerPos.z > 0f)
                    {
                        activeMaxSpeed = playerSpeed + 3f + Mathf.Clamp(dist * 1.4f, 0f, 16f);
                    }
                    else
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
                    float dist = Vector3.Distance(cachedTransform.position, playerKartCached.transform.position);
                    Vector3 localPlayerPos = cachedTransform.InverseTransformPoint(playerKartCached.transform.position);
                    
                    if (localPlayerPos.z > 0f)
                    {
                        activeMaxSpeed = playerSpeed + 5.0f + (dist * 1.6f);
                    }
                    else
                    {
                        if (dist < 6.0f)
                        {
                            activeMaxSpeed = playerSpeed + 3.0f;
                        }
                        else
                        {
                            activeMaxSpeed = Mathf.Max(playerSpeed + 2.5f, maxSpeed * 1.35f);
                        }
                    }
                    
                    activeMaxSpeed = Mathf.Clamp(activeMaxSpeed, maxSpeed * 0.85f, maxSpeed * 1.70f);
                    activeAcceleration = acceleration * 1.50f;
                }
                else
                {
                    activeMaxSpeed = maxSpeed * 1.35f;
                    activeAcceleration = acceleration * 1.50f;
                }
            }
            else if (aiDifficulty == AIDifficulty.CompetitivoA)
            {
                if (playerKartCached != null)
                {
                    float playerSpeed = playerKartCached.rb != null ? playerKartCached.rb.linearVelocity.magnitude : playerKartCached.currentSpeed;
                    float dist = Vector3.Distance(cachedTransform.position, playerKartCached.transform.position);
                    Vector3 localPlayerPos = cachedTransform.InverseTransformPoint(playerKartCached.transform.position);
                    
                    if (localPlayerPos.z > 0f)
                    {
                        activeMaxSpeed = playerSpeed + 6.5f + (dist * 1.8f);
                    }
                    else
                    {
                        activeMaxSpeed = Mathf.Max(playerSpeed + 3.0f, maxSpeed * 1.42f);
                    }
                    
                    activeMaxSpeed = Mathf.Clamp(activeMaxSpeed, maxSpeed * 0.9f, maxSpeed * 1.50f);
                    activeAcceleration = acceleration * 1.80f;
                }
                else
                {
                    activeMaxSpeed = maxSpeed * 1.42f;
                    activeAcceleration = acceleration * 1.80f;
                }
            }
            else if (aiDifficulty == AIDifficulty.CompetitivoF)
            {
                if (playerKartCached != null)
                {
                    float playerSpeed = playerKartCached.rb != null ? playerKartCached.rb.linearVelocity.magnitude : playerKartCached.currentSpeed;
                    float dist = Vector3.Distance(cachedTransform.position, playerKartCached.transform.position);
                    Vector3 localPlayerPos = cachedTransform.InverseTransformPoint(playerKartCached.transform.position);
                    
                    if (localPlayerPos.z > 0f)
                    {
                        activeMaxSpeed = playerSpeed + 8.0f + (dist * 2.0f);
                    }
                    else
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

        if (!isPlayer)
        {
            activeMaxSpeed *= mindsetSpeedBoost;
            activeAcceleration *= mindsetAccelBoost;
        }

        if (nitroBoostTimer > 0f)
        {
            activeMaxSpeed *= 1.55f;
            activeAcceleration *= 2.2f;
        }

        if (isGrounded)
        {
            bool isBrakeDrifting = isDrifting && throttleInput <= 0.1f;
            
            if (isBrakeDrifting)
            {
                float speedCapFactor = isPlayer ? 0.40f : 0.52f;
                targetForwardSpeed = activeMaxSpeed * speedCapFactor; 
                currentAccel = deceleration * (isPlayer ? 2.0f : 2.5f);
            }
            else if (smoothedThrottleInput > 0.05f)
            {
                targetForwardSpeed = smoothedThrottleInput * activeMaxSpeed;
                currentAccel = activeAcceleration;
            }
            else if (smoothedThrottleInput < -0.05f)
            {
                targetForwardSpeed = smoothedThrottleInput * reverseSpeed;
                currentAccel = acceleration * 2.5f;
            }
            else
            {
                targetForwardSpeed = 0f;
                currentAccel = deceleration;
            }

            float speedRatio = Mathf.Clamp01(Mathf.Abs(currentSpeed) / Mathf.Max(activeMaxSpeed, 1f));
            float torqueFactor = Mathf.Lerp(1.5f, 0.5f, speedRatio);

            currentSpeed = Mathf.MoveTowards(currentSpeed, targetForwardSpeed, currentAccel * torqueFactor * Time.fixedDeltaTime);
        }
        else
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, deceleration * 0.2f * Time.fixedDeltaTime);
        }

        if (isGrounded)
        {
            rb.AddForce(-smoothedGroundNormal * gravityForce, ForceMode.Acceleration);
        }
        else
        {
            rb.AddForce(Vector3.down * gravityForce * 1.2f, ForceMode.Acceleration);
        }

        float turnAngle = 0f;
        if (isGrounded && Mathf.Abs(currentSpeed) > 0.5f)
        {
            float steerDirection = currentSpeed >= 0 ? 1f : -1f;

            if (isDrifting && driftDirection != 0f)
            {
                float baseSteerFactor = (0.35f + (steeringInput * driftDirection) * 0.35f);
                if (IsBoosting)
                {
                    baseSteerFactor = (0.45f + (steeringInput * driftDirection) * 0.45f);
                }
                
                float steerFactor = isPlayer 
                    ? baseSteerFactor
                    : (0.35f + (steeringInput * driftDirection) * 0.25f);
                
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
                        playerSteerLimit *= 0.40f; 
                    }

                    if (IsBoosting)
                    {
                        playerSteerLimit *= 1.35f;
                    }
                }
                float actualSteerSpeed = steeringSpeed * (isPlayer ? playerSteerLimit : 2.0f) * driftSteerMultiplier * steerFactor;

                turnAngle = driftDirection * actualSteerSpeed * steerDirection * Time.fixedDeltaTime;
            }
            else
            {
                turnAngle = smoothedSteeringInput * steeringSpeed * steerDirection * Time.fixedDeltaTime;
            }
        }

        Quaternion steerRot = Quaternion.AngleAxis(turnAngle, cachedTransform.up);
        Quaternion yawedRot = steerRot * rb.rotation;

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
            Vector3 forwardHorizontal = yawedRot * Vector3.forward;
            forwardHorizontal.y = 0f;
            forwardHorizontal.Normalize();
            if (forwardHorizontal.sqrMagnitude > 0.001f)
            {
                targetRot = Quaternion.LookRotation(forwardHorizontal, Vector3.up);
            }
        }

        Quaternion newRotation = Quaternion.Slerp(rb.rotation, targetRot, Time.fixedDeltaTime * (isGrounded ? 15f : 10f));
        rb.MoveRotation(newRotation);

        Vector3 newForward = newRotation * Vector3.forward;
        Vector3 newRight = newRotation * Vector3.right;
        Vector3 newUp = newRotation * Vector3.up;

        if (isGrounded)
        {
            float targetGrip = isDrifting ? driftGrip : normalGrip;

            if (!isDrifting && Mathf.Abs(smoothedSteeringInput) > 0.1f)
            {
                targetGrip = Mathf.Lerp(normalGrip, normalGrip * 0.65f, Mathf.Abs(smoothedSteeringInput));
            }

            float gripSpeed = isDrifting ? 15f : 10.0f;
            currentGripValue = Mathf.Lerp(currentGripValue, targetGrip, Time.fixedDeltaTime * gripSpeed);

            float forwardVel = currentSpeed;

            float sidewaysVel = Vector3.Dot(rb.linearVelocity, newRight);
            float targetSidewaysVel = 0f;

            if (isDrifting && driftDirection != 0f)
            {
                float steerSlideFactor = driftSlipFactor + (steeringInput * driftDirection) * driftSlipSteerInfluence;
                
                if (IsBoosting)
                {
                    steerSlideFactor *= 0.55f;
                }
                
                float driftPush = -driftDirection * currentSpeed * steerSlideFactor;
                targetSidewaysVel = driftPush;
            }

            float newSidewaysVel = Mathf.Lerp(sidewaysVel, targetSidewaysVel, currentGripValue * Time.fixedDeltaTime * 50f);
            float verticalVel = Vector3.Dot(rb.linearVelocity, newUp);

            Vector3 targetVelocity = newForward * forwardVel + newRight * newSidewaysVel + newUp * verticalVel;
            Vector3 velocityChange = targetVelocity - rb.linearVelocity;
            rb.AddForce(velocityChange, ForceMode.VelocityChange);
        }
    }

    private void UpdateWheelVisuals()
    {
        if (useVisualSuspension)
        {
            UpdateWheelSuspension(frontLeftWheel, flInitialPos);
            UpdateWheelSuspension(frontRightWheel, frInitialPos);
            UpdateWheelSuspension(rearLeftWheel, rlInitialPos);
            UpdateWheelSuspension(rearRightWheel, rrInitialPos);
        }

        float speed = rb != null ? rb.linearVelocity.magnitude : 0f;
        float spinDir = Vector3.Dot(rb != null ? rb.linearVelocity : Vector3.zero, cachedTransform.forward) >= 0 ? 1f : -1f;
        
        cumulativeRollAngle += spinDir * speed * wheelSpinSpeed * Time.deltaTime;
        cumulativeRollAngle %= 360f;

        float steerAngle = smoothedSteeringInput * maxWheelTurnAngle;

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

        Vector3 mountPointWorld = cachedTransform.TransformPoint(initialLocalPos + Vector3.up * 0.3f);
        float rayLength = 0.3f + suspensionRestDistance;
        
        float targetYOffset = -suspensionRestDistance;
        
        int hitCount = Physics.RaycastNonAlloc(mountPointWorld, -cachedTransform.up, suspensionHits, rayLength, ~0, QueryTriggerInteraction.Ignore);
        float closestDist = float.MaxValue;
        bool grounded = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = suspensionHits[i];
            Transform hitT = hit.collider.transform;

            if (hitT == cachedTransform || hitT.root == cachedRootTransform)
                continue;
            if (hit.collider.attachedRigidbody != null && hit.collider.attachedRigidbody.TryGetComponent<KartController>(out _))
                continue;

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

        int totalKarts = ActiveKarts.Count;
        bool drafting = false;
        Vector3 playerPos = cachedTransform.position;
        Vector3 playerForward = cachedTransform.forward;

        for (int i = 0; i < totalKarts; i++)
        {
            KartController other = ActiveKarts[i];
            if (other == null || other == this || other.isPlayer) continue;

            Vector3 diff = other.transform.position - playerPos;
            float sqrDist = diff.sqrMagnitude;

            if (sqrDist < 196f) // 14m * 14m = 196
            {
                Vector3 localDir = cachedTransform.InverseTransformDirection(diff.normalized);
                if (localDir.z > 0.75f)
                {
                    // Vector3.Dot(a, b) > cos(15°) (0.9659f)
                    if (Vector3.Dot(playerForward, other.transform.forward) > 0.9659f)
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
            
            if (slipstreamTimer >= 1.8f)
            {
                activeBoostTimer = 1.5f;
                activeBoostMultiplier = 1.50f;
                slipstreamTimer = 0f;
                Debug.Log("SLIPSTREAM BOOST ACTIVATED!");
            }
        }
        else
        {
            slipstreamTimer = Mathf.Max(0f, slipstreamTimer - Time.deltaTime * 1.5f);
            isDraftingActive = false;
        }
    }

    public void RespawnAtClosestWaypoint()
    {
        if (waypointCircuit == null || waypointCircuit.waypoints == null || waypointCircuit.waypoints.Length == 0)
            return;

        var waypoints = waypointCircuit.waypoints;
        Vector3 currentPos = cachedTransform.position;
        float closestSqrDist = float.MaxValue;
        int closestIdx = 0;

        for (int i = 0; i < waypoints.Length; i++)
        {
            Transform wp = waypoints[i];
            if (wp == null) continue;
            float sqrD = (currentPos - wp.position).sqrMagnitude;
            if (sqrD < closestSqrDist)
            {
                closestSqrDist = sqrD;
                closestIdx = i;
            }
        }

        Transform targetWaypoint = waypoints[closestIdx];
        Vector3 spawnPos = targetWaypoint.position + Vector3.up * 0.8f;
        
        Quaternion spawnRot = targetWaypoint.rotation;
        int nextIdx = (closestIdx + 1) % waypoints.Length;
        if (waypoints[nextIdx] != null)
        {
            Vector3 lookDir = (waypoints[nextIdx].position - targetWaypoint.position).normalized;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                spawnRot = Quaternion.LookRotation(lookDir, Vector3.up);
            }
        }
        
        cachedTransform.position = spawnPos;
        cachedTransform.rotation = spawnRot;
        
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
        lastStuckCheckPosition = spawnPos;
        stuckPositionTimer = 0f;
        accumulatedStuckTime = 0f;
        stuckRadiusAnchor = spawnPos;
        stuckRadiusTimer = 0f;

        Debug.Log(gameObject.name + " respawned at waypoint: " + closestIdx);
    }

    public void ResetRaceProgress()
    {
        currentWaypointIndex = 0;
        currentSpeed = 0f;
        currentLap = 1;
        currentPosition = 1;
    }

    public void ActivateNitroBoost(float duration = 2.0f)
    {
        nitroBoostTimer = duration;
        Debug.Log(gameObject.name + " activated boost for " + duration + "s.");
    }

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

        var waypoints = waypointCircuit.waypoints;
        int W = waypoints.Length;
        int idx = currentWaypointIndex;
        int prevIdx = (idx - 1 + W) % W;

        if (waypoints[prevIdx] == null || waypoints[idx] == null)
            return 0f;

        Vector3 P = waypoints[prevIdx].position;
        Vector3 N = waypoints[idx].position;

        float D = Vector3.Distance(P, N);
        if (D < 0.01f) D = 0.01f;
        float d = Vector3.Distance(cachedTransform.position, N);
        float fraction = Mathf.Clamp01(1f - (d / D));

        float pathPosition = prevIdx + fraction;

        return (currentLap - 1) * W + pathPosition;
    }

    private void UpdateParticles()
    {
        bool shouldPlayDrift = isDrifting && isGrounded;
        if (shouldPlayDrift != currentDriftParticleState)
        {
            currentDriftParticleState = shouldPlayDrift;
            if (driftParticles != null)
            {
                for (int i = 0; i < driftParticles.Length; i++)
                {
                    var ps = driftParticles[i];
                    if (ps != null)
                    {
                        if (shouldPlayDrift) ps.Play();
                        else ps.Stop();
                    }
                }
            }
        }

        bool shouldPlayBoost = (nitroBoostTimer > 0f) || (activeBoostTimer > 0f);
        if (shouldPlayBoost != currentBoostParticleState)
        {
            currentBoostParticleState = shouldPlayBoost;
            if (boostParticles != null)
            {
                for (int i = 0; i < boostParticles.Length; i++)
                {
                    var ps = boostParticles[i];
                    if (ps != null)
                    {
                        if (shouldPlayBoost) ps.Play();
                        else ps.Stop();
                    }
                }
            }
        }
    }

    private void UpdateAudio()
    {
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
        if (activeCharges == lastActiveBoostCharges) return;

        lastActiveBoostCharges = activeCharges;

        for (int i = 0; i < boostIcons.Length; i++)
        {
            if (boostIcons[i] != null)
            {
                boostIcons[i].SetActive(i < activeCharges);
            }
        }
    }

    public float SteeringInput => steeringInput;

    public bool IsBoosting => (nitroBoostTimer > 0f) || (activeBoostTimer > 0f);

    public bool IsDrifting => isDrifting;

    public float CurrentSpeed
    {
        get
        {
            if (rb == null) return Mathf.Abs(currentSpeed);
            Vector3 horizontal = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            return horizontal.magnitude;
        }
    }

    public float MaxSpeed => maxSpeed;

    public float BoostedTopSpeed => maxSpeed * 1.55f;

    private void OnCollisionEnter(Collision collision)
    {
        float force = collision.relativeVelocity.magnitude;
        if (force < minCollisionForce) return;

        if (collisionSparksPrefab != null && collision.contactCount > 0)
        {
            ContactPoint contact = collision.GetContact(0);
            PlayCollisionSparks(contact.point, contact.normal);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("BoostPad"))
        {
            ActivateNitroBoost(2.5f);
            Debug.Log($"{gameObject.name} ativou Boost Automático ao passar por: {other.gameObject.name} (Tag: BoostPad)");
        }
    }

    private void PlayCollisionSparks(Vector3 point, Vector3 normal)
    {
        if (collisionSparksPrefab == null) return;

        ParticleSystem sparks = null;
        for (int i = 0; i < sparksPool.Count; i++)
        {
            ParticleSystem p = sparksPool[i];
            if (p != null && !p.isPlaying && !p.gameObject.activeSelf)
            {
                sparks = p;
                break;
            }
        }

        if (sparks == null)
        {
            sparks = Instantiate(collisionSparksPrefab);
            sparksPool.Add(sparks);
        }

        sparks.transform.position = point;
        sparks.transform.rotation = Quaternion.LookRotation(normal);
        sparks.gameObject.SetActive(true);
        sparks.Play();
    }

    public void UseSpecial()
    {
        if (!hasSpecial) return;

        KartController lockedTarget = targetingSystem != null ? targetingSystem.CurrentTarget : null;

        bool requiresTarget = currentSpecial != null ? currentSpecial.RequiresTarget : true;
        if (requiresTarget && lockedTarget == null)
        {
            Debug.Log($"{gameObject.name}: Sem alvo travado — Especial não disparado.");
            return;
        }

        if (currentSpecial != null)
        {
            currentSpecial.Activate(this, lockedTarget);
        }
        else
        {
            DefaultUseSpecial(lockedTarget);
        }

        hasSpecial = false;
        Debug.Log($"{gameObject.name} used Special on target: {(lockedTarget != null ? lockedTarget.name : "none")}.");
    }

    public void DefaultUseSpecial(KartController target)
    {
        Vector3 dir = target != null
            ? (target.transform.position - cachedTransform.position).normalized
            : cachedTransform.forward;

        Vector3 spawnPosition = cachedTransform.position + dir * 2.2f + Vector3.up * 0.8f;

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.position = spawnPosition;
        cube.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        cube.transform.localScale = new Vector3(0.5f, 0.5f, 1.2f);

        Collider col = cube.GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        HomingProjectile projectile = cube.AddComponent<HomingProjectile>();
        projectile.Initialize(this, target);
    }

    private void UpdateAISpecial()
    {
        if (!hasSpecial || currentSpecial == null || isPlayer) return;

        KartController lockedTarget = targetingSystem != null ? targetingSystem.CurrentTarget : null;

        if (currentSpecial.ShouldAIUse(this, lockedTarget))
        {
            UseSpecial();
        }
    }

    public void HitBySpecial(float duration)
    {
        stunTimer = duration;
        isDrifting = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        Debug.Log($"{gameObject.name} hit by special! Stunned for {duration} seconds.");
    }
}
