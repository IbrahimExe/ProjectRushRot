using UnityEngine;

[CreateAssetMenu(menuName = "Perks/Extra Jump")]
public class ExtraJumpPerk : AbilityBase
{
    public int extraJumpsAtLevelOne = 1;
    public int extraJumpsPerLevel = 0;

    public float baseRechargeCooldown = 2f;
    public float cooldownReductionPerLevel = 0.25f;

    public override StatModifier[] GetStatModifiers(int level)
    {
        int extraJumps = extraJumpsAtLevelOne + extraJumpsPerLevel * (level - 1);

        float cooldown = Mathf.Max(
            0.5f,
            baseRechargeCooldown - cooldownReductionPerLevel * (level - 1)
        );

        return new[]
        {
            new StatModifier("bonusAirJumps", extraJumps, StatModifier.ModType.Flat),
            new StatModifier("extraJumpRechargeCooldown", cooldown, StatModifier.ModType.Flat)
        };
    }
}