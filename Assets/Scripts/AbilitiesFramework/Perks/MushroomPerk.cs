using UnityEngine;

[CreateAssetMenu(menuName = "Perks/Mushroom")]
public class MushroomPerk : AbilityBase
{
    public int baseAmount = 1;
    public float baseCooldown = 30f;
    public int baseMax = 3;
    public float baseSpeedReduction = 0.15f;

    public override StatModifier[] GetStatModifiers(int level) => new[]
    {
        new StatModifier("mushroomAmount",         level,        StatModifier.ModType.Flat),
        new StatModifier("mushroomCooldown",       -3f * level,  StatModifier.ModType.Flat),
        new StatModifier("mushroomSpeedReduction", -0.02f * level, StatModifier.ModType.Flat) // penalty shrinks
    };

    public override void OnApply(PlayerControllerBase player, int level)
    {
        // player.mushroomEnabled = true;
    }

    public override void OnRemove(PlayerControllerBase player)
    {
        // player.mushroomEnabled = false;
    }
}