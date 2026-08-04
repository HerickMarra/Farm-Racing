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
    public float turnRate = 480f;
    [Tooltip("Maximum time (in seconds) the projectile may exist. If it does not hit a target within this time it self-destructs.")]
    public float lifetime = 2.5f;

    [Header("Dynamic Speed (Balancing)")]
    [Tooltip("If ON, the missile speed is based on the locked target's current speed at launch instead of the fixed 'speed' value above.")]
    public bool useDynamicSpeed = true;
    [Tooltip("Extra speed added on top of the target's current speed so the missile can catch a kart driving at normal speed.")]
    public float catchUpBonus = 35f;
    [Tooltip("Lowest speed the missile can ever fly at (prevents it from crawling when the target is slow or stopped).")]
    public float minSpeed = 75f;
    [Tooltip("The missile speed is capped this many m/s BELOW the target's Drift/Boost top speed, so a kart that boosts at the right moment can escape.")]
    public float escapeMargin = 3f;

    [Header("Launch Arc & Trajectory")]
    [Tooltip("Duration of initial rapid vertical pop-up phase.")]
    public float popDuration = 0.22f;
    [Tooltip("Duration of the quick dip-down phase following pop-up.")]
    public float dipDuration = 0.20f;
    [Tooltip("Upward pitch strength during pop-up.")]
    public float launchUpBias = 2.8f;
    [Tooltip("Minimum height above ground surface to prevent clipping.")]
    public float minHeightAboveGround = 0.7f;

    [Header("Zig-Zag / Wobble (instability)")]
    [Tooltip("How fast the projectile weaves side to side.")]
    public float wobbleFrequency = 14f;
    [Tooltip("Lateral weave strength.")]
    public float wobbleAmplitude = 5f;
    [Tooltip("Vertical bobbing strength.")]
    public float wobbleVertical = 2.5f;
    [Tooltip("Random lateral offset injected for a chaotic, unstable rocket feel.")]
    public float randomWander = 2.0f;

    [Header("Roll Spin")]
    [Tooltip("How fast the projectile body rolls around its forward axis (degrees per second).")]
    public float rollSpeed = 540f;

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

        // Initial direction: pitch upward on launch for dramatic pop-up arc
        Vector3 ownerForward = ownerKart != null ? ownerKart.transform.forward : transform.forward;
        Vector3 initialDir = (ownerForward + Vector3.up * launchUpBias).normalized;

        if (initialDir.sqrMagnitude < 0.001f)
        {
            initialDir = transform.forward + Vector3.up * launchUpBias;
        }
        currentDirection = initialDir.normalized;

        // Dynamic speed balancing: match the target's current pace
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
    /// </summary>
    protected virtual float ComputeDynamicSpeed(KartController targetKart)
    {
        float desired = targetKart.CurrentSpeed * 3.0f;
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
    /// Core homing + dynamic 3D flight behavior with ground anti-clipping protection.
    /// </summary>
    protected virtual void UpdateMovement(float dt)
    {
        if (useDynamicSpeed && target != null)
        {
            speed = ComputeDynamicSpeed(target);
        }

        Vector3 desiredDir;

        if (age < popDuration)
        {
            // FASE 1: Subida rápida com tudo para o alto (Pop-up)
            Vector3 ownerForward = owner != null ? owner.transform.forward : transform.forward;
            desiredDir = (ownerForward + Vector3.up * launchUpBias).normalized;
        }
        else if (age < popDuration + dipDuration)
        {
            // FASE 2: Dá uma abaixada rápida direcionada ao alvo/chão
            Vector3 toTarget = target != null ? (GetTargetPoint() - transform.position).normalized : (owner != null ? owner.transform.forward : transform.forward);
            desiredDir = (toTarget - Vector3.up * 0.8f).normalized;
        }
        else
        {
            // FASE 3: Voo teleguiado com Zig-Zag lateral e vertical dinâmico
            if (target != null)
            {
                Vector3 toTarget = GetTargetPoint() - transform.position;
                float distance = toTarget.magnitude;
                Vector3 baseDir = distance > 0.001f ? toTarget / distance : currentDirection;

                wobblePhase += dt * wobbleFrequency;
                Vector3 right = Vector3.Cross(Vector3.up, baseDir).normalized;
                if (right.sqrMagnitude < 0.001f) right = Vector3.right;
                Vector3 up = Vector3.Cross(baseDir, right).normalized;

                float lateral = Mathf.Sin(wobblePhase) * wobbleAmplitude;
                float vertical = Mathf.Sin(wobblePhase * 1.3f + 0.5f) * wobbleVertical;

                float noise = (Mathf.PerlinNoise(wanderSeed.x + age * 3.0f, wanderSeed.y) - 0.5f) * 2f;
                lateral += noise * randomWander;

                // Restringe a oscilação conforme se aproxima
                float distFactor = Mathf.Clamp01(distance / 6f);

                Vector3 wobble = (right * lateral + up * vertical) * distFactor;
                desiredDir = (baseDir * speed + wobble).normalized;
            }
            else
            {
                desiredDir = currentDirection;
            }
        }

        float effectiveTurnRate = age < popDuration ? 360f : (age < popDuration + dipDuration ? 500f : turnRate);

        currentDirection = Vector3.RotateTowards(
            currentDirection,
            desiredDir,
            effectiveTurnRate * Mathf.Deg2Rad * dt,
            0f).normalized;

        transform.position += currentDirection * speed * dt;

        // Trava anti-enterramento no chão (Anti Ground-Clipping Raycast)
        if (Physics.Raycast(transform.position + Vector3.up * 1.5f, Vector3.down, out RaycastHit groundHit, 5f, ~0, QueryTriggerInteraction.Ignore))
        {
            float targetMinY = groundHit.point.y + minHeightAboveGround;
            if (transform.position.y < targetMinY)
            {
                Vector3 correctedPos = transform.position;
                correctedPos.y = Mathf.Lerp(correctedPos.y, targetMinY, dt * 20f);
                transform.position = correctedPos;
            }
        }
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
