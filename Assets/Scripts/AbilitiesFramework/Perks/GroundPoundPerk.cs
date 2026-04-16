using UnityEngine;

[CreateAssetMenu(menuName = "Perks/Ground Pound")]
public class GroundPoundPerk : AbilityBase
{
    public float baseCooldown = 35f;
    public float baseExplosionRadius = 1.2f;

    public override StatModifier[] GetStatModifiers(int level) => new[]
    {
        new StatModifier("groundPoundCooldown", -4f * level,  StatModifier.ModType.Flat),
        new StatModifier("groundPoundRadius",    0.3f * level, StatModifier.ModType.Flat)
    };

    public override void OnApply(PlayerControllerBase player, int level)
    {
        
    }

    public override void OnRemove(PlayerControllerBase player)
    {
      
    }

    private void HandleLand(PlayerControllerBase player)
    {
       
    }
}