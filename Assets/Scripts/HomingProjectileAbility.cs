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
}
