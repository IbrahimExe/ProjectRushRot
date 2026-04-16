using UnityEngine;

[CreateAssetMenu(menuName = "Perks/Snowball")]
public class SnowBallPerk : AbilityBase
{
    public float baseBridgeLength = 10f;
    public float baseBridgeDuration = 3f;

    public override StatModifier[] GetStatModifiers(int level) => new[]
    {
        new StatModifier("snowBallBridgeLength",   2f * level,  StatModifier.ModType.Flat),
        new StatModifier("snowBallBridgeDuration", 0.5f * level, StatModifier.ModType.Flat)
    };

    public override void OnApply(PlayerControllerBase player, int level)
    {
        // player.snowBallEnabled = true;
    }

    public override void OnRemove(PlayerControllerBase player)
    {
        // player.snowBallEnabled = false;
    }
}