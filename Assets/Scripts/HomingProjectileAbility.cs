using UnityEngine;

/// <summary>
/// A Special that fires a homing/guided projectile at a locked target.
/// Each character can have its own asset with a different prefab, speed and behaviour,
/// without ever touching the KartController. This is the "bazooka missile" by default.
/// </summary>
[CreateAssetMenu(fileName = "NewHomingMissileAbility", menuName = "Special System/Homing Projectile Ability")]
public class HomingProjectileAbility : SpecialAbility
{
    [Header("Projectile")]
    [Tooltip("Prefab containing a HomingProjectile (or derived) component.")]
    public GameObject projectilePrefab;
    [Tooltip("How far in front of / behind the kart the projectile spawns.")]
    public float spawnOffset = 2.2f;
    [Tooltip("Vertical spawn offset above the kart pivot.")]
    public float spawnHeight = 0.8f;

    [Header("Optional Overrides (-1 = use prefab default)")]
    public float overrideSpeed = -1f;
    public float overrideTurnRate = -1f;

    // Homing missile must lock onto a target before firing.
    public override bool RequiresTarget => true;

    public override void Activate(KartController user, KartController target)
    {
        if (user == null) return;

        // Spawn towards the target (or forward as a safety fallback).
        Vector3 dir;
        if (target != null)
        {
            dir = (target.transform.position - user.transform.position).normalized;
        }
        else
        {
            dir = user.transform.forward;
        }

        Vector3 spawnPos = user.transform.position + dir * spawnOffset + Vector3.up * spawnHeight;
        Quaternion spawnRot = Quaternion.LookRotation(dir, Vector3.up);

        GameObject projGo;
        if (projectilePrefab != null)
        {
            projGo = Object.Instantiate(projectilePrefab, spawnPos, spawnRot);
        }
        else
        {
            // Fallback placeholder so the system always works even without a prefab.
            projGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            projGo.transform.position = spawnPos;
            projGo.transform.rotation = spawnRot;
            projGo.transform.localScale = new Vector3(0.5f, 0.5f, 1.2f);
            Collider col = projGo.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        HomingProjectile projectile = projGo.GetComponent<HomingProjectile>();
        if (projectile == null)
        {
            projectile = projGo.AddComponent<HomingProjectile>();
        }

        if (overrideSpeed > 0f) projectile.speed = overrideSpeed;
        if (overrideTurnRate > 0f) projectile.turnRate = overrideTurnRate;

        projectile.Initialize(user, target);
    }

    public override bool ShouldAIUse(KartController aiKart, KartController target)
    {
        if (target == null) return false;

        // Calculate distance
        float distance = Vector3.Distance(aiKart.transform.position, target.transform.position);
        
        // Homing settings check (limit range to avoid shooting too close or too far)
        float maxRange = aiKart.TargetingSystem != null ? aiKart.TargetingSystem.lockRange : 35f;
        if (distance < 5.0f || distance > maxRange) return false;

        // Race position check: target should be ahead in the race (higher rank / lower position value)
        if (target.currentPosition > aiKart.currentPosition) return false;

        // Linecast check: Make sure there are no solid walls blocking the flight path
        Vector3 startOffset = aiKart.transform.position + Vector3.up * spawnHeight;
        Vector3 endOffset = target.transform.position + Vector3.up * 0.6f;
        if (Physics.Linecast(startOffset, endOffset, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
        {
            // If we hit static terrain or walls, don't fire
            if (hit.collider.gameObject != target.gameObject && !hit.collider.transform.IsChildOf(target.transform))
            {
                return false;
            }
        }

        // Distance to finish line adjustment
        float finishProgress = aiKart.GetRaceFinishProgress();

        // Strategic saving: If we are early in the race (first 10%) and the target is not very close,
        // we have a high tendency to save the special for later laps.
        if (finishProgress < 0.10f && distance > 15.0f)
        {
            // 75% chance to save it (only evaluate probability check 25% of the time)
            if (Random.value < 0.75f) return false;
        }

        // Periodic probability check to simulate human reaction delay
        float firingProbability = 0.03f; // Default 3% chance per frame (approx. 0.5s - 1.5s delay)

        // Near finish line: Be much more aggressive to secure victory
        if (finishProgress > 0.85f)
        {
            firingProbability = 0.10f; // 10% chance per frame (near-instant trigger)
        }

        return Random.value < firingProbability;
    }
}
