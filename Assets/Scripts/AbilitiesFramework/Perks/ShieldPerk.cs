using UnityEngine;

[CreateAssetMenu(menuName = "Perks/Shield")]
public class ShieldPerk : AbilityBase
{
    public float baseCooldown = 45f;
    public float cooldownReductionPerLevel = 6f;
    public float shieldRadius = 3f;

    [Header("Visuals")]
    public GameObject shieldVisualPrefab;
    public float visualScaleMultiplier = 2f;

    private float cooldownTimer;
    private bool shieldReady;
    private GameObject shieldVisual;

    public override void OnApply(PlayerAbilityContext ctx, int level)
    {
        shieldReady = true;
        CreateVisual(ctx);
    }

    public override void Tick(PlayerAbilityContext ctx, int level, float deltaTime)
    {
        if (!shieldReady)
        {
            cooldownTimer -= deltaTime;

            if (cooldownTimer <= 0f)
                shieldReady = true;
        }

        UpdateVisual(ctx);
    }

    public override void FixedTick(PlayerAbilityContext ctx, int level, float fixedDeltaTime)
    {
        if (!shieldReady)
            return;

        Collider[] hits = ctx.GetNearby(shieldRadius);

        bool destroyedSomething = false;

        foreach (Collider hit in hits)
        {
            if (ctx.TryDestroyWithAbility(hit, abilityId))
                destroyedSomething = true;
        }

        if (!destroyedSomething)
            return;

        shieldReady = false;
        cooldownTimer = Mathf.Max(5f, baseCooldown - cooldownReductionPerLevel * (level - 1));

        UpdateVisual(ctx);
    }

    private void CreateVisual(PlayerAbilityContext ctx)
    {
        if (shieldVisualPrefab == null || shieldVisual != null)
            return;

        shieldVisual = Instantiate(shieldVisualPrefab);
        shieldVisual.transform.SetParent(ctx.playerTransform, false);
        shieldVisual.transform.localPosition = Vector3.zero;
        shieldVisual.transform.localRotation = Quaternion.identity;
        shieldVisual.transform.localScale = Vector3.one * shieldRadius * visualScaleMultiplier;

        Collider col = shieldVisual.GetComponent<Collider>();
        if (col != null)
            col.enabled = false;

        Rigidbody rb = shieldVisual.GetComponent<Rigidbody>();
        if (rb != null)
            rb.isKinematic = true;

        shieldVisual.layer = LayerMask.NameToLayer("Ignore Raycast");
    }

    private void UpdateVisual(PlayerAbilityContext ctx)
    {
        CreateVisual(ctx);

        if (shieldVisual == null)
            return;

        shieldVisual.transform.SetParent(ctx.playerTransform, false);
        shieldVisual.transform.localPosition = Vector3.zero;
        shieldVisual.transform.localRotation = Quaternion.identity;
        shieldVisual.SetActive(shieldReady);
    }
}