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
        IAbilityDestructible destructible = col.GetComponentInParent<IAbilityDestructible>();

        if (destructible == null)
            return false;

        if (!destructible.CanBeDestroyedBy(abilityId))
            return false;

        destructible.DestroyByAbility(abilityId, player.gameObject);
        return true;
    }
}