using UnityEngine;

[CreateAssetMenu(menuName = "Perks/Mushroom")]
public class MushroomPerk : AbilityBase
{
    public float upwardImpulse = 45f;
    public float forwardImpulse = 80f;
    public float protectionDuration = 1.25f;

    public int baseCharges = 1;
    public int chargesPerLevel = 1;
    public int maxCharges = 3;

    private int charges;
    private float protectionTimer;

    public bool IsProtecting => protectionTimer > 0f;

    public override void OnApply(PlayerAbilityContext ctx, int level)
    {
        charges = GetCharges(level);
    }

    public override void OnUpgrade(PlayerAbilityContext ctx, int oldLevel, int newLevel)
    {
        charges = GetCharges(newLevel);
    }

    public override void Tick(PlayerAbilityContext ctx, int level, float deltaTime)
    {
        if (protectionTimer > 0f)
            protectionTimer -= deltaTime;
    }

    public override void FixedTick(PlayerAbilityContext ctx, int level, float fixedDeltaTime)
    {
        if (charges <= 0)
            return;

        string region = ctx.player.lastGroundRegion;

        if (region != "SAND" && region != "WATER" && region != "DEEPWATER")
            return;

        ctx.rb.linearVelocity = new Vector3(
            ctx.rb.linearVelocity.x,
            0f,
            ctx.rb.linearVelocity.z
        );

        Vector3 launch =
            Vector3.up * upwardImpulse +
            ctx.playerTransform.forward * forwardImpulse;

        ctx.rb.AddForce(launch, ForceMode.Impulse);

        protectionTimer = protectionDuration;
        charges--;
    }

    private int GetCharges(int level)
    {
        return Mathf.Min(baseCharges + chargesPerLevel * (level - 1), maxCharges);
    }

    public override StatModifier[] GetStatModifiers(int level)
    {
        return System.Array.Empty<StatModifier>();
    }
}