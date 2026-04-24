using UnityEngine;

[CreateAssetMenu(menuName = "Perks/Shield")]
public class ShieldPerk : AbilityBase
{
    public float baseCooldown = 45f;
    public float cooldownReductionPerLevel = 6f;
    public float shieldRadius = 3f;
    public string targetTag = "ShieldObstacle";

    private float cooldownTimer;
    private bool shieldReady;

    public override void OnApply(PlayerAbilityContext ctx, int level)
    {
        shieldReady = true;
    }

    public override void Tick(PlayerAbilityContext ctx, int level, float deltaTime)
    {
        if (shieldReady)
            return;

        cooldownTimer -= deltaTime;

        if (cooldownTimer <= 0f)
            shieldReady = true;
    }

    public override void FixedTick(PlayerAbilityContext ctx, int level, float fixedDeltaTime)
    {
        if (!shieldReady)
            return;

        Collider[] hits = Physics.OverlapSphere(
            ctx.playerTransform.position,
            shieldRadius,
            ctx.abilityMask
        );

        foreach (Collider hit in hits)
        {
            if (!hit.CompareTag(targetTag))
                continue;

            Destroy(hit.gameObject);

            shieldReady = false;
            cooldownTimer = Mathf.Max(5f, baseCooldown - cooldownReductionPerLevel * (level - 1));
            return;
        }
    }
}