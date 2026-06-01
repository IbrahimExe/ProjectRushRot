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
    private float currentCooldown;

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
        if (ctx == null || ctx.player == null || ctx.rb == null)
        {
            Debug.LogError("GroundPoundPerk: Missing context, player, or Rigidbody.");
            return false;
        }

        if (cooldownTimer > 0f)
            return false;

        if (preparingSlam || waitingForLanding)
            return false;

        ctx.rb.linearVelocity = new Vector3(ctx.rb.linearVelocity.x, 0f, ctx.rb.linearVelocity.z);
        ctx.rb.AddForce(Vector3.up * upwardImpulse, ForceMode.Impulse);

        preparingSlam = true;
        slamTimer = slamDelay;

        currentCooldown = cooldown;
        cooldownTimer = currentCooldown;

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

    public override float GetCooldownPercent()
    {
        if (currentCooldown <= 0f)
            return 1f;

        return 1f - Mathf.Clamp01(cooldownTimer / currentCooldown);
    }

    public override bool IsReady()
    {
        return cooldownTimer <= 0f && !preparingSlam && !waitingForLanding;
    }
}