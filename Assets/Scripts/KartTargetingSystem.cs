using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles automatic target acquisition (lock-on) for a kart's Special.
/// Searches for the nearest valid enemy inside a configurable radius and view cone.
/// The search direction depends on the player's camera: forward by default,
/// or backward while holding CTRL (mirroring the look-behind camera).
/// This component is fully decoupled from the projectile and the kart movement logic.
/// </summary>
[RequireComponent(typeof(KartController))]
public class KartTargetingSystem : MonoBehaviour
{
    [Header("Lock-On Settings")]
    [Tooltip("Maximum distance (meters) to acquire a target. Kept relatively short on purpose so the player must get close before using the Special. Karts farther than this are completely ignored.")]
    public float lockRange = 35f;
    [Tooltip("Half-angle of the search cone in degrees (front or rear).")]
    [Range(5f, 180f)]
    public float lockHalfAngle = 75f;
    [Tooltip("If true, the targeting only runs for the human player. AI uses forward search only.")]
    public bool enableForAI = true;

    /// <summary>The currently locked enemy kart (null when nothing is in range/cone).</summary>
    public KartController CurrentTarget { get; private set; }

    /// <summary>True when the player is currently aiming behind (CTRL held).</summary>
    public bool IsAimingBackward { get; private set; }

    public bool HasTarget => CurrentTarget != null;

    private KartController owner;

    private void Awake()
    {
        owner = GetComponent<KartController>();
    }

    private void Update()
    {
        // Only acquire a target while a Special is available and ready to fire.
        if (owner == null || !owner.hasSpecial)
        {
            CurrentTarget = null;
            return;
        }

        if (!owner.isPlayer && !enableForAI)
        {
            CurrentTarget = null;
            return;
        }

        IsAimingBackward = ShouldSearchBackward();
        CurrentTarget = AcquireTarget(IsAimingBackward);
    }

    /// <summary>
    /// The player looks back while holding CTRL (matching the camera look-behind).
    /// </summary>
    private bool ShouldSearchBackward()
    {
        if (!owner.isPlayer) return false;
        if (Keyboard.current == null) return false;
        return Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
    }

    /// <summary>
    /// Returns the nearest valid kart inside the search cone, or null.
    /// </summary>
    private KartController AcquireTarget(bool backward)
    {
        Vector3 origin = transform.position;
        Vector3 searchDir = backward ? -transform.forward : transform.forward;
        searchDir.y = 0f;
        searchDir.Normalize();

        float cosLimit = Mathf.Cos(lockHalfAngle * Mathf.Deg2Rad);

        KartController best = null;
        float bestDist = float.MaxValue;

        var karts = KartController.ActiveKarts;
        for (int i = 0; i < karts.Count; i++)
        {
            KartController candidate = karts[i];
            if (candidate == null || candidate == owner) continue;

            Vector3 toTarget = candidate.transform.position - origin;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;
            if (dist < 0.01f || dist > lockRange) continue;

            Vector3 dirToTarget = toTarget / dist;
            float dot = Vector3.Dot(searchDir, dirToTarget);
            if (dot < cosLimit) continue; // outside the front/rear cone

            if (dist < bestDist)
            {
                bestDist = dist;
                best = candidate;
            }
        }

        return best;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, lockRange);
    }
}
