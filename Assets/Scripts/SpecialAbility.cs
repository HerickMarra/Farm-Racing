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

    /// <summary>
    /// Evaluates if the AI should fire this special at the current frame.
    /// Override this in subclasses to implement specific tactical behaviors.
    /// </summary>
    public virtual bool ShouldAIUse(KartController aiKart, KartController target)
    {
        return false;
    }
}
