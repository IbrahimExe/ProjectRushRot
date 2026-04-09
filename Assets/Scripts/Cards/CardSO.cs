using UnityEngine;

[CreateAssetMenu(fileName = "New Card", menuName = "Card")]
public class CardSO : ScriptableObject
{
    public Sprite cardImage;
    public string cardText;

    public CardEffect cardEffect;
    public float effectValue;

    // For paired upgrades that need a second value (e.g. DashKill kill count bonus).
    // Leave at 0 if the card only uses effectValue.
    public float effectValue2;

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
    // ── Max Speed ──────────────────────────────────
    CommonMaxSpeedUpgrade,
    RareMaxSpeedUpgrade,
    LegendaryMaxSpeedUpgrade,

    // ── Acceleration ───────────────────────────────
    CommonAccelerationUpgrade,
    RareAccelerationUpgrade,
    LegendaryAccelerationUpgrade,

    // ── Jump ───────────────────────────────────────
    CommonJumpUpgrade,
    RareJumpUpgrade,
    LegendaryJumpUpgrade,

    // ── Wall Run (Duration + Speed — paired) ───────
    CommonWallRunUpgrade,
    RareWallRunUpgrade,
    LegendaryWallRunUpgrade,

    // ── Wall Jump (Up + Away — paired) ─────────────
    CommonWallJumpUpgrade,
    RareWallJumpUpgrade,
    LegendaryWallJumpUpgrade,

    // ── Dash Kill (Window + Kill Count — paired) ───
    CommonDashKillUpgrade,
    RareDashKillUpgrade,
    LegendaryDashKillUpgrade,
}