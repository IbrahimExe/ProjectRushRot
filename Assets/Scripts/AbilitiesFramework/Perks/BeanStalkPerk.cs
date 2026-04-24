using UnityEngine;

[CreateAssetMenu(menuName = "Perks/Beanstalk")]
public class BeanStalkPerk : AbilityBase
{
    public GameObject bridgeSegmentPrefab;
    public int baseSegments = 1;
    public int segmentsPerLevel = 1;
    public float segmentLength = 8f;
    public float segmentLifetime = 5f;
    public float spawnHeightOffset = -0.5f;

    public override bool TryUse(PlayerAbilityContext ctx, int level)
    {
        if (bridgeSegmentPrefab == null)
            return false;

        int count = baseSegments + segmentsPerLevel * (level - 1);

        Vector3 start = ctx.playerTransform.position + Vector3.up * spawnHeightOffset;
        Vector3 forward = ctx.playerTransform.forward;

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = start + forward * segmentLength * (i + 1);
            Quaternion rot = Quaternion.LookRotation(forward, Vector3.up);

            GameObject segment = Instantiate(bridgeSegmentPrefab, pos, rot);
            Destroy(segment, segmentLifetime);
        }

        return true;
    }
}