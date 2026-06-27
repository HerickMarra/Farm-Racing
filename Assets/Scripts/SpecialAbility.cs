using UnityEngine;

public abstract class SpecialAbility : ScriptableObject
{
    [Header("Ability Info")]
    public string abilityName;
    
    [Tooltip("Icon representing this special in the HUD.")]
    public Sprite hudIcon;

    /// <summary>
    /// Whether this Special needs a locked target before it can be fired.
    /// Homing missiles return true; items like bananas/bombs can return false.
    /// </summary>
    public virtual bool RequiresTarget => true;

    /// <summary>
    /// Activate the Special. <paramref name="target"/> is the kart locked by the
    /// targeting system (may be null for specials that don't require a target).
    /// </summary>
    public abstract void Activate(KartController user, KartController target);
}
