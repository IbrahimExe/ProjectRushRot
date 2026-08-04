using UnityEngine;

public class PlayerAbilityContext
{
    public PlayerControllerBase player;
    public Rigidbody rb;
    public Transform playerTransform;
    public LayerMask abilityMask;
    public PerkManager perkManager;

    public PlayerAbilityContext(PlayerControllerBase player, LayerMask abilityMask, PerkManager perkManager)
    {
        this.player = player;

        this.rb = player.RB;

        if (this.rb == null)
            this.rb = player.GetComponent<Rigidbody>();

        this.playerTransform = player.transform;
        this.abilityMask = abilityMask;
        this.perkManager = perkManager;
    }

    public Collider[] GetNearby(float radius)
    {
        return Physics.OverlapSphere(playerTransform.position, radius, abilityMask);
    }

    public bool TryDestroyWithAbility(Collider col, string abilityId)
    {
        if (col == null)
            return false;

        IAbilityDestructible destructible =
            col.GetComponentInParent<IAbilityDestructible>();

        if (destructible == null)
            return false;

        if (!destructible.CanBeDestroyedBy(abilityId))
            return false;

        destructible.DestroyByAbility(
            abilityId,
            player != null ? player.gameObject : null
        );

        if (RunStatsTracker.Instance != null)
            RunStatsTracker.Instance.RegisterDestruction();

        return true;
    }
}