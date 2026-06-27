using UnityEngine;

public abstract class SpecialAbility : ScriptableObject
{
    [Header("Ability Info")]
    public string abilityName;
    
    [Tooltip("Icon representing this special in the HUD.")]
    public Sprite hudIcon;

    public abstract void Activate(KartController user, bool forward);
}
