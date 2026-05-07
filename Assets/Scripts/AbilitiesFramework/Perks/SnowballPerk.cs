using UnityEngine;

[CreateAssetMenu(menuName = "Perks/Snowball")]
public class SnowBallPerk : AbilityBase
{
    public GameObject bridgePrefab;
    public float baseBridgeLength = 10f;
    public float lengthPerLevel = 3f;
    public float bridgeDuration = 3f;
    public float detectionRadius = 8f;
    public string holeTag = "Hole";

    public override bool TryUse(PlayerAbilityContext ctx, int level)
    {
        if (bridgePrefab == null)
            return false;

        Collider[] hits = Physics.OverlapSphere(
            ctx.playerTransform.position,
            detectionRadius,
            ctx.abilityMask
        );

        foreach (Collider hit in hits)
        {
            if (!hit.CompareTag(holeTag))
                continue;

            Vector3 pos = hit.bounds.center;
            pos.y = ctx.playerTransform.position.y - 0.5f;

            GameObject bridge = Instantiate(bridgePrefab, pos, ctx.playerTransform.rotation);

            float length = baseBridgeLength + lengthPerLevel * (level - 1);
            bridge.transform.localScale = new Vector3(
                bridge.transform.localScale.x,
                bridge.transform.localScale.y,
                length
            );

            Destroy(bridge, bridgeDuration);
            return true;
        }

        return false;
    }
}