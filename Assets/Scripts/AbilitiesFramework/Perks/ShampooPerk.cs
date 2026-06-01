using UnityEngine;

[CreateAssetMenu(menuName = "Perks/Shampoo")]
public class ShampooPerk : AbilityBase
{
    public float baseRecoveryMultiplier = 0.85f;
    public float recoveryReductionPerLevel = 0.1f;

    public override StatModifier[] GetStatModifiers(int level)
    {
        float multiplier = Mathf.Max(0.35f, baseRecoveryMultiplier - recoveryReductionPerLevel * (level - 1));

        return new[]
        {
            new StatModifier("ailmentRecoveryMultiplier", multiplier, StatModifier.ModType.Multiplicative)
        };
    }
}