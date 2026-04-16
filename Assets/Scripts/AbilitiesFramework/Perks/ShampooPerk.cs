using UnityEngine;

[CreateAssetMenu(menuName = "Perks/Shampoo")]
public class ShampooPerk : AbilityBase
{
    public float baseRecoveryTime = 5f;
    public float baseCooldown = 25f;
    public float baseDebuffDiminish = 1f;

    public override StatModifier[] GetStatModifiers(int level) => new[]
    {
        new StatModifier("shampooRecoveryTime",   -0.5f * level, StatModifier.ModType.Flat),
        new StatModifier("shampooCooldown",        -3f * level,  StatModifier.ModType.Flat),
        new StatModifier("shampooDebuffDiminish",   0.2f * level, StatModifier.ModType.Flat)
    };

    public override void OnApply(PlayerControllerBase player, int level)
    {
        // player.shampooEnabled = true;
    }

    public override void OnRemove(PlayerControllerBase player)
    {
        // player.shampooEnabled = false;
    }
}