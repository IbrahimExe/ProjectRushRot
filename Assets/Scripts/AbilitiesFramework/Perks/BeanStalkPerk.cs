using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Perks/Beanstalk")]
public class BeanStalkPerk : AbilityBase
{
    public GameObject bridgeSegmentPrefab;

    [Header("Segments")]
    public int baseSegments = 3;
    public int segmentsPerLevel = 1;
    public float segmentLength = 16f;
    public float segmentLifetime = 5f;
    public float spawnHeightOffset = -0.5f;

    [Header("Cooldown")]
    public float baseCooldown = 8f;
    public float cooldownReductionPerLevel = 0.5f;
    public float minimumCooldown = 3f;

    [Header("Path Shape")]
    public float upwardAngle = 25f;

    private float cooldownTimer;
    private readonly List<GameObject> activeSegments = new();

    public override void Tick(PlayerAbilityContext ctx, int level, float deltaTime)
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= deltaTime;
    }

    public override bool TryUse(PlayerAbilityContext ctx, int level)
    {
        if (cooldownTimer > 0f)
            return false;

        if (bridgeSegmentPrefab == null)
            return false;

        ClearOldSegments();

        int count = Mathf.Max(1, baseSegments + segmentsPerLevel * (level - 1));

        Debug.Log("Beanstalk spawning segments: " + count);

        Vector3 start = ctx.playerTransform.position + Vector3.up * spawnHeightOffset;

        Vector3 forward = ctx.playerTransform.forward.normalized;
        Vector3 pathDirection = Quaternion.AngleAxis(-upwardAngle, ctx.playerTransform.right) * forward;
        pathDirection.Normalize();

        Quaternion rot = Quaternion.FromToRotation(Vector3.up, pathDirection);

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = start + pathDirection * segmentLength * (i + 0.5f);

            GameObject segment = Instantiate(bridgeSegmentPrefab, pos, rot);

            if (segment.GetComponent<BeanstalkSurface>() == null)
                segment.AddComponent<BeanstalkSurface>();

            activeSegments.Add(segment);
            Destroy(segment, segmentLifetime);
        }

        cooldownTimer = GetCooldown(level);
        return true;
    }

    private float GetCooldown(int level)
    {
        return Mathf.Max(
            minimumCooldown,
            baseCooldown - cooldownReductionPerLevel * (level - 1)
        );
    }

    private void ClearOldSegments()
    {
        for (int i = activeSegments.Count - 1; i >= 0; i--)
        {
            if (activeSegments[i] != null)
                Destroy(activeSegments[i]);
        }

        activeSegments.Clear();
    }
}