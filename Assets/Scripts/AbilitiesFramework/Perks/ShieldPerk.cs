using UnityEngine;

[CreateAssetMenu(menuName = "Perks/Shield")]
public class ShieldPerk : AbilityBase
{
    public float baseCooldown = 45f;
    public int baseMaxAmount = 3;

    public override StatModifier[] GetStatModifiers(int level) => new[]
    {
        new StatModifier("shieldCooldown",   -5f * level,  StatModifier.ModType.Flat),  // shorter each level
        new StatModifier("shieldMaxAmount",   level,       StatModifier.ModType.Flat)
    };

    public override void OnApply(PlayerControllerBase player, int level)
    {
       
    }

    public override void OnRemove(PlayerControllerBase player)
    {
        // player.shieldEnabled = false;
    }
}