using UnityEngine;

[CreateAssetMenu(menuName = "Perks/Missiles")]
public class MissilePerk : AbilityBase
{
    public float baseIndividualCooldown = 30f;
    public int baseAmount = 3;
    public int baseMaxAmount = 5;

    public override StatModifier[] GetStatModifiers(int level) => new[]
    {
        new StatModifier("missileCooldown",  -3f * level, StatModifier.ModType.Flat),
        new StatModifier("missileAmount",     level,      StatModifier.ModType.Flat),
        new StatModifier("missileMaxAmount",  level,      StatModifier.ModType.Flat)
    };

    public override void OnApply(PlayerControllerBase player, int level)
    {
        // player.missilesEnabled = true;
    }

    public override void OnRemove(PlayerControllerBase player)
    {
        // player.missilesEnabled = false;
    }
}