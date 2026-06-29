using UnityEngine;

/// <summary>
/// Base class for all homing/guided projectiles (bazooka-style missile, red shell, drone, etc.).
/// Handles the homing movement, irregular zig-zag wobble, smooth roll, VFX trail and audio.
/// Derive from this class to create custom projectile behaviours without touching the kart controller.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class HomingProjectile : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Current flight speed (m/s). When 'Use Dynamic Speed' is enabled this is recomputed at launch from the target's speed.")]
    public float speed = 120f;
    [Tooltip("How fast the projectile can correct its heading (degrees per second). Lower = wider, lazier curves.")]
    public float turnRate = 420f;
    [Tooltip("Maximum time (in seconds) the projectile may exist. If it does not hit a target within this time it self-destructs.")]
    public float lifetime = 4f;

    [Header("Dynamic Speed (Balancing)")]
    [Tooltip("If ON, the missile speed is based on the locked target's current speed at launch instead of the fixed 'speed' value above.")]
    public bool useDynamicSpeed = true;
    [Tooltip("Extra speed added on top of the target's current speed so the missile can catch a kart driving at normal speed.")]
    public float catchUpBonus = 35f;
    [Tooltip("Lowest speed the missile can ever fly at (prevents it from crawling when the target is slow or stopped).")]
    public float minSpeed = 75f;
    [Tooltip("The missile speed is capped this many m/s BELOW the target's Drift/Boost top speed, so a kart that boosts at the right moment can escape.")]
    public float escapeMargin = 3f;

    [Header("Zig-Zag / Wobble (instability)")]
    [Tooltip("How fast the projectile weaves side to side.")]
    public float wobbleFrequency = 6f;
    [Tooltip("Lateral weave strength.")]
    public float wobbleAmplitude = 5f;
    [Tooltip("Vertical bobbing strength.")]
    public float wobbleVertical = 1.5f;
    [Tooltip("Random lateral offset injected for a chaotic, unstable rocket feel.")]
    public float randomWander = 1.5f;

    [Header("Roll Spin")]
    [Tooltip("How fast the projectile body rolls around its forward axis (degrees per second).")]
    public float rollSpeed = 260f;

    [Header("Hit Settings")]
    [Tooltip("Stun duration applied to the kart that gets hit.")]
    public float stunDuration = 1.5f;
    [Tooltip("Manual proximity hit radius (backup to physics triggers).")]
    public float hitRadius = 2.2f;

    [Header("Effects (optional placeholders)")]
    [Tooltip("Smoke / particle trail emitted while flying.")]
    public ParticleSystem smokeTrail;
    [Tooltip("Explosion particle prefab spawned on impact (optional).")]
    public ParticleSystem explosionPrefab;
    [Tooltip("Looping rocket audio (placeholder).")]
    public AudioSource rocketAudio;

    protected KartController owner;
    protected KartController target;
    protected Vector3 currentDirection;
    protected float age;
    protected float wobblePhase;
    protected float rollAngle;
    protected Vector2 wanderSeed;
    protected bool hasExploded;

    /// <summary>
    /// Called by the SpecialAbility right after the projectile is instantiated.
    /// </summary>
    public virtual void Initialize(KartController ownerKart, KartController targetKart)
    {
        owner = ownerKart;
        target = targetKart;

        // Initial direction: bias towards the target, otherwise launch along owner facing.
        Vector3 initialDir;
        if (target != null)
        {
            initialDir = (GetTargetPoint() - transform.position).normalized;
        }
        else
        {
            initialDir = ownerKart != null ? ownerKart.transform.forward : transform.forward;
        }

        if (initialDir.sqrMagnitude < 0.001f)
        {
            initialDir = transform.forward;
        }
        currentDirection = initialDir;

        // Dynamic speed balancing: match the target's current pace (so it catches a normal-speed kart),
        // but always stay below the target's Drift/Boost top speed so a well-timed boost lets them escape.
        if (useDynamicSpeed && target != null)
        {
            speed = ComputeDynamicSpeed(target);
        }

        wanderSeed = new Vector2(Random.value * 100f, Random.value * 100f);
        wobblePhase = Random.value * Mathf.PI * 2f;

        // Make sure physics never fights our manual movement.
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        if (smokeTrail != null && !smokeTrail.isPlaying) smokeTrail.Play();
        if (rocketAudio != null) rocketAudio.Play();

        OnLaunched();

        // Hard safety net so projectiles never leak.
        Destroy(gameObject, lifetime + 0.5f);
    }

    /// <summary>Hook for derived classes to react to launch.</summary>
    protected virtual void OnLaunched() { }

    /// <summary>
    /// Computes the missile's flight speed from the target's current speed.
    /// The result is always capped below the target's Drift/Boost top speed, so that a
    /// kart using a boost at the right moment can outrun the missile (counter-play mechanic).
    /// Override to implement custom balancing per projectile type.
    /// </summary>
    protected virtual float ComputeDynamicSpeed(KartController targetKart)
    {
        // Dynamic speed is exactly three times the speed of the target kart.
        float desired = targetKart.CurrentSpeed * 3.0f;
        // Clamp between our high minimum speed floor (75 m/s) and a high maximum speed limit (140 m/s)
        return Mathf.Clamp(desired, minSpeed, 140f);
    }

    protected virtual Vector3 GetTargetPoint()
    {
        if (target == null) return transform.position + currentDirection * 5f;
        return target.transform.position + Vector3.up * 0.6f;
    }

    protected virtual void Update()
    {
        if (hasExploded) return;

        float dt = Time.deltaTime;
        age += dt;
        if (age >= lifetime)
        {
            Explode(false);
            return;
        }

        UpdateMovement(dt);
        UpdateOrientation(dt);
        CheckProximityHit();
    }

    /// <summary>
    /// Core homing + zig-zag movement. Override to implement custom flight behaviour.
    /// </summary>
    protected virtual void UpdateMovement(float dt)
    {
        // Recalculate dynamic speed in real-time to respond to target's drift/boost state
        if (useDynamicSpeed && target != null)
        {
            speed = ComputeDynamicSpeed(target);
        }

        Vector3 desiredDir;

        if (target != null)
        {
            Vector3 toTarget = GetTargetPoint() - transform.position;
            float distance = toTarget.magnitude;
            Vector3 baseDir = distance > 0.001f ? toTarget / distance : currentDirection;

            // Build a wobble offset perpendicular to the base direction.
            wobblePhase += dt * wobbleFrequency;
            Vector3 right = Vector3.Cross(Vector3.up, baseDir).normalized;
            Vector3 up = Vector3.Cross(baseDir, right).normalized;

            float lateral = Mathf.Sin(wobblePhase) * wobbleAmplitude;
            float vertical = Mathf.Sin(wobblePhase * 0.7f + 1.3f) * wobbleVertical;

            // Inject Perlin-noise wander for an unstable, chaotic feel.
            float noise = (Mathf.PerlinNoise(wanderSeed.x + age * 1.7f, wanderSeed.y) - 0.5f) * 2f;
            lateral += noise * randomWander;

            // Dampen the wobble as the missile closes in so it still connects.
            float distFactor = Mathf.Clamp01(distance / 9f);

            Vector3 wobble = (right * lateral + up * vertical) * distFactor;
            desiredDir = (baseDir * speed + wobble).normalized;
        }
        else
        {
            // No target: keep flying straight ahead.
            desiredDir = currentDirection;
        }

        currentDirection = Vector3.RotateTowards(
            currentDirection,
            desiredDir,
            turnRate * Mathf.Deg2Rad * dt,
            0f).normalized;

        transform.position += currentDirection * speed * dt;
    }

    /// <summary>Orient the body along the flight path and apply a smooth roll spin.</summary>
    protected virtual void UpdateOrientation(float dt)
    {
        if (currentDirection.sqrMagnitude < 0.0001f) return;

        rollAngle += rollSpeed * dt;
        Quaternion look = Quaternion.LookRotation(currentDirection, Vector3.up);
        transform.rotation = look * Quaternion.Euler(0f, 0f, rollAngle);
    }

    protected virtual void CheckProximityHit()
    {
        if (target == null) return;
        float d = Vector3.Distance(transform.position, target.transform.position);
        if (d <= hitRadius)
        {
            HitKart(target);
        }
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        HandleCollision(other.gameObject);
    }

    protected virtual void OnCollisionEnter(Collision collision)
    {
        HandleCollision(collision.gameObject);
    }

    protected virtual void HandleCollision(GameObject otherGo)
    {
        if (hasExploded) return;

        KartController kart = otherGo.GetComponentInParent<KartController>();
        if (kart != null)
        {
            if (kart == owner) return; // never hit the launcher
            HitKart(kart);
        }
    }

    protected virtual void HitKart(KartController kart)
    {
        if (hasExploded || kart == null) return;
        kart.HitBySpecial(stunDuration);
        Explode(true);
    }

    protected virtual void Explode(bool hitSomething)
    {
        if (hasExploded) return;
        hasExploded = true;

        if (explosionPrefab != null)
        {
            ParticleSystem fx = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            Destroy(fx.gameObject, fx.main.duration + fx.main.startLifetime.constantMax);
        }

        // Detach the smoke trail so it fades out naturally instead of vanishing instantly.
        if (smokeTrail != null)
        {
            smokeTrail.transform.SetParent(null, true);
            smokeTrail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(smokeTrail.gameObject, 2f);
        }

        Destroy(gameObject);
    }
}
