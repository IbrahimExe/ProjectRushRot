using UnityEngine;

[CreateAssetMenu(fileName = "New Card", menuName = "Card")]
public class CardSO : ScriptableObject
{
    public Sprite cardImage;
    public string cardText;

    public CardEffect cardEffect;
    public float effectValue;

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
    CommonJumpHeightUpgrade,
    RareJumpHeightUpgrade,
    LegendaryJumpHeightUpgrade,
    CommonWallRun,
    RareWallRun,
    LegendaryWallRun,
    CommonDash,
    RareDash,
    LegendaryDash

}