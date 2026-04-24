using UnityEngine;

[CreateAssetMenu(menuName = "Perks/Ground Pound")]
public class GroundPoundPerk : AbilityBase
{
    public float downwardImpulse = 120f;
    public float baseRadius = 5f;
    public float radiusPerLevel = 1.5f;
    public float cooldown = 8f;

    public string enemyTag = "Enemy";
    public string breakableTag = "BreakableObstacle";

    private float cooldownTimer;
    private bool waitingForLanding;

    public override void Tick(PlayerAbilityContext ctx, int level, float deltaTime)
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= deltaTime;
    }

    public override bool TryUse(PlayerAbilityContext ctx, int level)
    {
        if (cooldownTimer > 0f)
            return false;

        if (ctx.player.IsGrounded)
            return false;

        ctx.rb.linearVelocity = new Vector3(ctx.rb.linearVelocity.x, 0f, ctx.rb.linearVelocity.z);
        ctx.rb.AddForce(Vector3.down * downwardImpulse, ForceMode.Impulse);

        waitingForLanding = true;
        cooldownTimer = cooldown;

        return true;
    }

    public override void FixedTick(PlayerAbilityContext ctx, int level, float fixedDeltaTime)
    {
        if (!waitingForLanding)
            return;

        if (!ctx.player.IsGrounded)
            return;

        waitingForLanding = false;

        float radius = baseRadius + radiusPerLevel * (level - 1);

        Collider[] hits = Physics.OverlapSphere(
            ctx.playerTransform.position,
            radius,
            ctx.abilityMask
        );

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag(enemyTag) || hit.CompareTag(breakableTag))
                Destroy(hit.gameObject);
        }
    }
}