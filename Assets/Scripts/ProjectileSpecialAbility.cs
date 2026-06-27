using UnityEngine;

[CreateAssetMenu(fileName = "NewProjectileSpecialAbility", menuName = "Special System/Projectile Special Ability")]
public class ProjectileSpecialAbility : SpecialAbility
{
    [Header("Projectile Settings")]
    public GameObject projectilePrefab;
    public float spawnOffset = 2.5f;

    // Straight-shot projectile: can fire forward even without a locked target (e.g. green shell).
    public override bool RequiresTarget => false;

    public override void Activate(KartController user, KartController target)
    {
        // Aim at the target if one is locked, otherwise just fire straight ahead.
        Vector3 spawnDirection;
        if (target != null)
        {
            spawnDirection = (target.transform.position - user.transform.position).normalized;
        }
        else
        {
            spawnDirection = user.transform.forward;
        }

        Vector3 spawnPosition = user.transform.position + spawnDirection * spawnOffset + Vector3.up * 0.5f;

        GameObject projGo;
        if (projectilePrefab != null)
        {
            projGo = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
        }
        else
        {
            // Fallback to a simple Cube if prefab is null
            projGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            projGo.transform.position = spawnPosition;
            projGo.transform.localScale = Vector3.one * 0.8f;

            // Make the collider a trigger so it doesn't physically block the kart on spawn
            Collider col = projGo.GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        // Ensure it has SpecialProjectile component
        SpecialProjectile specProj = projGo.GetComponent<SpecialProjectile>();
        if (specProj == null)
        {
            specProj = projGo.AddComponent<SpecialProjectile>();
        }

        specProj.Initialize(user, spawnDirection);
    }
}
