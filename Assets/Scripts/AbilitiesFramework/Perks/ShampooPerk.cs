using UnityEngine;

[CreateAssetMenu(menuName = "Perks/Shampoo")]
public class ShampooPerk : AbilityBase
{
    [Header("Debuff Resistance")]
    public float baseReduction = 0.20f;
    public float reductionPerLevel = 0.10f;
    public float maxReduction = 0.50f;

    [Header("Cooldown Upgrade After Cap")]
    public float cooldownReductionPerLevelAfterCap = 0.1f;

    public override StatModifier[] GetStatModifiers(int level)
    {
        float reduction = Mathf.Min(
            maxReduction,
            baseReduction + reductionPerLevel * (level - 1)
        );

        float multiplier = 1f - reduction;

        return new[]
        {
            new StatModifier("debuffAmountMultiplier", multiplier, StatModifier.ModType.Multiplicative),
            new StatModifier("debuffDurationMultiplier", multiplier, StatModifier.ModType.Multiplicative)
        };
    }
}