using UnityEngine;

[CreateAssetMenu(menuName = "Perks/Extra Jump")]
public class ExtraJumpPerk : AbilityBase
{
    public int extraJumpsAtLevelOne = 1;
    public float baseRechargeCooldown = 2f;
    public float cooldownReductionPerLevel = 0.25f;

    public override void OnApply(PlayerAbilityContext ctx, int level)
    {
        ApplyJumpBonus(ctx, level);
    }

    public override void OnUpgrade(PlayerAbilityContext ctx, int oldLevel, int newLevel)
    {
        ApplyJumpBonus(ctx, newLevel);
    }

    private void ApplyJumpBonus(PlayerAbilityContext ctx, int level)
    {
        if (ctx.player.characterData == null)
            return;

        int extraJumps = extraJumpsAtLevelOne;

        ctx.player.characterData.numOfJumps += extraJumps;
    }

    public override StatModifier[] GetStatModifiers(int level)
    {
        float cooldown = Mathf.Max(0.5f, baseRechargeCooldown - cooldownReductionPerLevel * (level - 1));

        return new[]
        {
            new StatModifier("extraJumpRechargeCooldown", cooldown, StatModifier.ModType.Flat)
        };
    }
}