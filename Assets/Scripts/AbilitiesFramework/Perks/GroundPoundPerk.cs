using UnityEngine;

[CreateAssetMenu(menuName = "Perks/Ground Pound")]
public class GroundPoundPerk : AbilityBase
{
    public float upwardImpulse = 18f;
    public float downwardImpulse = 140f;
    public float slamDelay = 0.18f;

    public float baseRadius = 5f;
    public float radiusPerLevel = 1.5f;
    public float cooldown = 8f;

    private float cooldownTimer;
    private bool preparingSlam;
    private bool waitingForLanding;
    private float slamTimer;

    public override void Tick(PlayerAbilityContext ctx, int level, float deltaTime)
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= deltaTime;

        if (!preparingSlam)
            return;

        slamTimer -= deltaTime;

        if (slamTimer > 0f)
            return;

        preparingSlam = false;
        waitingForLanding = true;

        ctx.rb.linearVelocity = new Vector3(ctx.rb.linearVelocity.x, 0f, ctx.rb.linearVelocity.z);
        ctx.rb.AddForce(Vector3.down * downwardImpulse, ForceMode.Impulse);
    }

    public override bool TryUse(PlayerAbilityContext ctx, int level)
    {
        if (cooldownTimer > 0f)
            return false;

        if (preparingSlam || waitingForLanding)
            return false;

        ctx.rb.linearVelocity = new Vector3(ctx.rb.linearVelocity.x, 0f, ctx.rb.linearVelocity.z);
        ctx.rb.AddForce(Vector3.up * upwardImpulse, ForceMode.Impulse);

        preparingSlam = true;
        slamTimer = slamDelay;
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

        Collider[] hits = ctx.GetNearby(radius);

        foreach (Collider hit in hits)
        {
            ctx.TryDestroyWithAbility(hit, abilityId);
        }
    }
}