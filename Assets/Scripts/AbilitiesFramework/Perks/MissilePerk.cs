using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Perks/Missile")]
public class MissilePerk : AbilityBase
{
    public float baseCooldown = 30f;
    public int baseMissileAmount = 1;
    public int maxMissiles = 5;
    public float searchRadius = 45f;

    [Header("Visuals")]
    public GameObject missileVisualPrefab;
    public float visualOrbitRadius = 1.5f;
    public float visualHeight = 1.2f;
    public float visualSpinSpeed = 90f;

    private int currentMissiles;
    private float rechargeTimer;
    private readonly List<GameObject> visuals = new();

    public override void OnApply(PlayerAbilityContext ctx, int level)
    {
        currentMissiles = GetMaxMissiles(level);
    }

    public override void Tick(PlayerAbilityContext ctx, int level, float deltaTime)
    {
        int maxAvailable = GetMaxMissiles(level);

        if (currentMissiles < maxAvailable)
        {
            rechargeTimer -= deltaTime;

            if (rechargeTimer <= 0f)
            {
                currentMissiles++;
                rechargeTimer = GetCooldown(level);
            }
        }

        UpdateVisuals(ctx, level);
    }

    public override bool TryUse(PlayerAbilityContext ctx, int level)
    {
        if (currentMissiles <= 0)
            return false;

        Collider[] hits = ctx.GetNearby(searchRadius);

        Collider bestTarget = null;
        float bestScore = float.MaxValue;

        Vector3 playerPos = ctx.playerTransform.position;
        Vector3 forward = ctx.playerTransform.forward;

        foreach (Collider hit in hits)
        {
            if (hit == null)
                continue;

            IAbilityDestructible destructible = hit.GetComponentInParent<IAbilityDestructible>();

            if (destructible == null)
                continue;

            if (!destructible.CanBeDestroyedBy(abilityId))
                continue;

            Vector3 toTarget = hit.bounds.center - playerPos;

            if (Vector3.Dot(forward, toTarget.normalized) <= 0.25f)
                continue;

            float distance = toTarget.sqrMagnitude;

            if (distance < bestScore)
            {
                bestScore = distance;
                bestTarget = hit;
            }
        }

        if (bestTarget == null)
            return false;

        ctx.TryDestroyWithAbility(bestTarget, abilityId);

        currentMissiles--;

        if (currentMissiles < GetMaxMissiles(level) && rechargeTimer <= 0f)
            rechargeTimer = GetCooldown(level);

        return true;
    }

    private int GetMaxMissiles(int level)
    {
        return Mathf.Min(baseMissileAmount + level - 1, maxMissiles);
    }

    private float GetCooldown(int level)
    {
        return Mathf.Max(3f, baseCooldown - 3f * (level - 1));
    }

    private void UpdateVisuals(PlayerAbilityContext ctx, int level)
    {
        if (missileVisualPrefab == null)
            return;

        int maxAvailable = GetMaxMissiles(level);
        EnsureVisualCount(maxAvailable);

        for (int i = 0; i < visuals.Count; i++)
        {
            GameObject visual = visuals[i];

            if (visual == null)
                continue;

            bool shouldShow = i < currentMissiles;
            visual.SetActive(shouldShow);

            if (!shouldShow)
                continue;

            float angle = ((360f / Mathf.Max(1, currentMissiles)) * i) + Time.time * visualSpinSpeed;
            float radians = angle * Mathf.Deg2Rad;

            Vector3 offset = new Vector3(
                Mathf.Cos(radians) * visualOrbitRadius,
                visualHeight,
                Mathf.Sin(radians) * visualOrbitRadius
            );

            visual.transform.position = ctx.playerTransform.position + offset;
            visual.transform.rotation = Quaternion.LookRotation(offset.normalized);
        }
    }

    private void EnsureVisualCount(int amount)
    {
        while (visuals.Count < amount)
        {
            GameObject visual = Instantiate(missileVisualPrefab);
            visual.SetActive(false);

            Collider col = visual.GetComponent<Collider>();
            if (col != null)
                col.enabled = false;

            Rigidbody rb = visual.GetComponent<Rigidbody>();
            if (rb != null)
                rb.isKinematic = true;

            visual.layer = LayerMask.NameToLayer("Ignore Raycast");

            visuals.Add(visual);
        }
    }
}