using UnityEngine;

[CreateAssetMenu(menuName = "Perks/Magnet")]
public class MagnetPerk : AbilityBase
{
    public float baseRadius = 10f;
    public float basePullStrength = 7.5f;

    public override StatModifier[] GetStatModifiers(int level) => new[]
    {
        new StatModifier("magnetRadius",      2.5f * level, StatModifier.ModType.Flat),
        new StatModifier("magnetPullStrength", 1f * level,  StatModifier.ModType.Flat)
    };

    public override void OnApply(PlayerControllerBase player, int level)
    {
        // player.magnetEnabled = true;
    }

    public override void OnRemove(PlayerControllerBase player)
    {
        // player.magnetEnabled = false;
    }
}