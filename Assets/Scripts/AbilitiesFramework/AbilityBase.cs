using UnityEngine;

public abstract class AbilityBase : ScriptableObject
{
    [Header("Metadata")]
    public string abilityId;
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;
    public int maxLevel = 3;

    public virtual void OnApply(PlayerAbilityContext ctx, int level) { }
    public virtual void OnRemove(PlayerAbilityContext ctx) { }
    public virtual void OnUpgrade(PlayerAbilityContext ctx, int oldLevel, int newLevel) { }

    public virtual void Tick(PlayerAbilityContext ctx, int level, float deltaTime) { }
    public virtual void FixedTick(PlayerAbilityContext ctx, int level, float fixedDeltaTime) { }

    public virtual bool TryUse(PlayerAbilityContext ctx, int level)
    {
        return false;
    }

    public virtual StatModifier[] GetStatModifiers(int level)
    {
        return System.Array.Empty<StatModifier>();
    }
}