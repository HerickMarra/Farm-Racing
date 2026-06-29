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

    public override bool ShouldAIUse(KartController aiKart, KartController target)
    {
        // A straight shot does not require a lock-on target. AI needs to look for karts in front.
        var karts = KartController.ActiveKarts;
        float finishProgress = aiKart.GetRaceFinishProgress();

        for (int i = 0; i < karts.Count; i++)
        {
            KartController opponent = karts[i];
            if (opponent == null || opponent == aiKart) continue;

            // Race ranking check: Avoid shooting karts that are behind us in placement (e.g. lapped karts)
            if (opponent.currentPosition > aiKart.currentPosition) continue;

            Vector3 toOpponent = opponent.transform.position - aiKart.transform.position;
            float dist = toOpponent.magnitude;

            // Target must be in front within reasonable straight-shot range
            if (dist > 5.0f && dist < 25.0f)
            {
                Vector3 dirToOpponent = toOpponent / dist;
                float dot = Vector3.Dot(aiKart.transform.forward, dirToOpponent);

                // Cone of ~20 degrees (dot > 0.94f)
                if (dot > 0.94f)
                {
                    // Linecast check for walls
                    Vector3 startOffset = aiKart.transform.position + Vector3.up * 0.8f;
                    Vector3 endOffset = opponent.transform.position + Vector3.up * 0.5f;
                    if (!Physics.Linecast(startOffset, endOffset, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) ||
                        hit.collider.gameObject == opponent.gameObject ||
                        hit.collider.transform.IsChildOf(opponent.transform))
                    {
                        // Strategic saving: If early in the race (first 10%) and distance is not close, save the item
                        if (finishProgress < 0.10f && dist > 15.0f)
                        {
                            if (Random.value < 0.75f) continue;
                        }

                        // Determine firing probability (approx. 3% per frame default, up to 10% near finish)
                        float firingProbability = (finishProgress > 0.85f) ? 0.10f : 0.03f;

                        if (Random.value < firingProbability)
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }
}
