using UnityEngine;

[CreateAssetMenu(menuName = "Perks/Beanstalk")]
public class BeanStalkPerk : AbilityBase
{
    public int baseSegments = 1;
    public float baseSegmentLength = 20f;

    public override StatModifier[] GetStatModifiers(int level) => new[]
    {
        new StatModifier("beanStalkSegments",      level,        StatModifier.ModType.Flat),
        new StatModifier("beanStalkSegmentLength", 5f * level,   StatModifier.ModType.Flat)
    };

    public override void OnApply(PlayerControllerBase player, int level)
    {
        // player.beanStalkEnabled = true;
    }

    public override void OnRemove(PlayerControllerBase player)
    {
        // player.beanStalkEnabled = false;
    }
}