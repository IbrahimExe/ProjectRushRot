using UnityEngine;

[CreateAssetMenu(menuName = "Perks/Extra Jump")]
public class ExtraJumpPerk : AbilityBase
{
    public float baseCooldown = 10f;

    public override StatModifier[] GetStatModifiers(int level) => new[]
    {
        new StatModifier("extraJumpCount",    level,       StatModifier.ModType.Flat),
        new StatModifier("extraJumpCooldown", -1f * level, StatModifier.ModType.Flat)
    };

    public override void OnApply(PlayerControllerBase player, int level)
    {
       // player.OnJump += HandleJump;
    }

    public override void OnRemove(PlayerControllerBase player)
    {
       // player.OnJump -= HandleJump;
    }

    private void HandleJump(PlayerControllerBase player)
    {
      
    }
}