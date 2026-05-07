using UnityEngine;

[CreateAssetMenu(fileName = "New Card", menuName = "Card")]
public class CardSO : ScriptableObject
{
    public Sprite cardImage;
    public string cardText;

    public CardEffect cardEffect;
    public float effectValue;
    public float effectValue2;

    [Header("Perk Card")]
    public AbilityBase perkToApply;

    public CardRarity rarity;

    public bool isUnique;
    public int unlockLevel;
}

public enum CardRarity
{
    Common,
    Rare,
    Legendary
}

public enum CardEffect
{
    CommonMaxSpeedUpgrade,
    RareMaxSpeedUpgrade,
    LegendaryMaxSpeedUpgrade,

    CommonAccelerationUpgrade,
    RareAccelerationUpgrade,
    LegendaryAccelerationUpgrade,

    CommonJumpUpgrade,
    RareJumpUpgrade,
    LegendaryJumpUpgrade,

    CommonWallRunUpgrade,
    RareWallRunUpgrade,
    LegendaryWallRunUpgrade,

    CommonWallJumpUpgrade,
    RareWallJumpUpgrade,
    LegendaryWallJumpUpgrade,

    CommonDashKillUpgrade,
    RareDashKillUpgrade,
    LegendaryDashKillUpgrade,

    ApplyPerk
}