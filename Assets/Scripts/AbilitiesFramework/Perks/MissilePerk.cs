using UnityEngine;

[CreateAssetMenu(menuName = "Perks/Missile")]
public class MissilePerk : AbilityBase
{
    public float baseCooldown = 30f;
    public int baseMissileAmount = 1;
    public int maxMissiles = 5;
    public float searchRadius = 45f;

    public string targetTag = "MissileObstacle";

    private float cooldownTimer;

    public override void Tick(PlayerAbilityContext ctx, int level, float deltaTime)
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= deltaTime;
    }

    public override bool TryUse(PlayerAbilityContext ctx, int level)
    {
        if (cooldownTimer > 0f)
            return false;

        int missileAmount = Mathf.Min(baseMissileAmount + level - 1, maxMissiles);

        Collider[] hits = Physics.OverlapSphere(
            ctx.playerTransform.position,
            searchRadius,
            ctx.abilityMask
        );

        int used = 0;

        foreach (Collider hit in hits)
        {
            if (!hit.CompareTag(targetTag))
                continue;

            Destroy(hit.gameObject);
            used++;

            if (used >= missileAmount)
                break;
        }

        if (used <= 0)
            return false;

        cooldownTimer = Mathf.Max(3f, baseCooldown - 3f * (level - 1));
        return true;
    }
}