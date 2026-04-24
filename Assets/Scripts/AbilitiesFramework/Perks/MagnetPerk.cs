using UnityEngine;

[CreateAssetMenu(menuName = "Perks/Magnet")]
public class MagnetPerk : AbilityBase
{
    public float baseRadius = 10f;
    public float radiusPerLevel = 2.5f;
    public float basePullStrength = 7.5f;
    public float pullPerLevel = 1.5f;

    public string targetTag = "Collectible";

    public override void FixedTick(PlayerAbilityContext ctx, int level, float fixedDeltaTime)
    {
        float radius = baseRadius + radiusPerLevel * (level - 1);
        float pullStrength = basePullStrength + pullPerLevel * (level - 1);

        Collider[] hits = Physics.OverlapSphere(
            ctx.playerTransform.position,
            radius,
            ctx.abilityMask
        );

        foreach (Collider hit in hits)
        {
            if (!hit.CompareTag(targetTag))
                continue;

            Vector3 direction = ctx.playerTransform.position - hit.transform.position;

            if (hit.TryGetComponent(out Rigidbody rb))
            {
                rb.AddForce(direction.normalized * pullStrength, ForceMode.Acceleration);
            }
            else
            {
                hit.transform.position = Vector3.MoveTowards(
                    hit.transform.position,
                    ctx.playerTransform.position,
                    pullStrength * fixedDeltaTime
                );
            }
        }
    }
}