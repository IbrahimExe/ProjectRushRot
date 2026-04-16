using UnityEngine;

public abstract class AbilityBase : ScriptableObject
{
    [Header("Metadata")]
    public string abilityId;
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;
    public int maxLevel = 3;

    public virtual void OnApply(PlayerControllerBase player, int level) { }
    public virtual void OnRemove(PlayerControllerBase player) { }
    public virtual void OnUpgrade(PlayerControllerBase player, int oldLevel, int newLevel) { }
    public virtual StatModifier[] GetStatModifiers(int level) => System.Array.Empty<StatModifier>();
}