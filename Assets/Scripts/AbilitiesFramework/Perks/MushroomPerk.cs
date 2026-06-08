using UnityEngine;

[CreateAssetMenu(menuName = "Perks/Mushroom")]
public class MushroomPerk : AbilityBase
{
    public int baseCharges = 1;
    public int maxCharges = 3;
    public float speedReduction = 0.15f;
    public float slowDuration = 2f;
    public float holeDetectionRadius = 2.5f;
    public string holeTag = "Hole";

    private int charges;
    private float slowTimer;
    private bool slowApplied;

    public override void OnApply(PlayerAbilityContext ctx, int level)
    {
        charges = Mathf.Min(baseCharges + level - 1, maxCharges);
    }

    public override void OnUpgrade(PlayerAbilityContext ctx, int oldLevel, int newLevel)
    {
        charges = Mathf.Min(baseCharges + newLevel - 1, maxCharges);
    }

    public override void Tick(PlayerAbilityContext ctx, int level, float deltaTime)
    {
        if (!slowApplied)
            return;

        slowTimer -= deltaTime;

        if (slowTimer <= 0f)
        {
            ctx.player.SetBaseStats();
            slowApplied = false;
        }
    }

    public override void FixedTick(PlayerAbilityContext ctx, int level, float fixedDeltaTime)
    {
        if (charges <= 0)
            return;

        Collider[] hits = Physics.OverlapSphere(
            ctx.playerTransform.position,
            holeDetectionRadius,
            ctx.abilityMask
        );

        foreach (Collider hit in hits)
        {
            if (!hit.CompareTag(holeTag))
                continue;

            Vector3 safePosition = ctx.playerTransform.position - ctx.playerTransform.forward * 4f;
            safePosition.y += 1f;

            ctx.playerTransform.position = safePosition;
            ctx.rb.linearVelocity = Vector3.zero;

            ctx.player.addMaxSpeed(-ctx.player.baseMaxMoveSpeed * speedReduction);
            slowApplied = true;
            slowTimer = slowDuration;

            charges--;
            return;
        }
    }
}