using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardUI : MonoBehaviour
{
    private CardSO data;
    private UpgradeStats upgradeStats;

    public Image cardImage;
    public TMP_Text cardText;

    public void Initialize(CardSO card, UpgradeStats upgrade)
    {
        data = card;
        upgradeStats = upgrade ?? FindFirstObjectByType<UpgradeStats>();

        if (cardImage != null && card != null)
            cardImage.sprite = card.cardImage;

        if (cardText != null && card != null)
            cardText.text = card.cardText;
    }

    public void ApplyCardEffect()
    {
        if (data == null) return;

        if (upgradeStats == null)
        {
            upgradeStats = FindFirstObjectByType<UpgradeStats>();
            if (upgradeStats == null)
            {
                Debug.LogError("CardUI.ApplyCardEffect: UpgradeStats not found. Cannot apply " + data.name);
                return;
            }
        }

        Debug.Log($"Applying: {data.name} | Effect: {data.cardEffect} | V1: {data.effectValue} | V2: {data.effectValue2}");

        switch (data.cardEffect)
        {
            // ?? Max Speed ??????????????????????????????????????????????????
            case CardEffect.CommonMaxSpeedUpgrade:
            case CardEffect.RareMaxSpeedUpgrade:
            case CardEffect.LegendaryMaxSpeedUpgrade:
                upgradeStats.UpgradeMaxSpeedPercent(data.effectValue);
                break;

            // ?? Acceleration ???????????????????????????????????????????????
            case CardEffect.CommonAccelerationUpgrade:
            case CardEffect.RareAccelerationUpgrade:
            case CardEffect.LegendaryAccelerationUpgrade:
                upgradeStats.UpgradeAccelerationPercent(data.effectValue);
                break;

            // ?? Jump ???????????????????????????????????????????????????????
            case CardEffect.CommonJumpUpgrade:
            case CardEffect.RareJumpUpgrade:
            case CardEffect.LegendaryJumpUpgrade:
                upgradeStats.UpgradeJumpForcePercent(data.effectValue);
                break;

            // ?? Wall Run (Duration + Speed — paired) ???????????????????????
            // effectValue  = seconds added to wallRunDuration
            // effectValue2 = amount added to wallRunSpeedMultiplier
            case CardEffect.CommonWallRunUpgrade:
            case CardEffect.RareWallRunUpgrade:
            case CardEffect.LegendaryWallRunUpgrade:
                upgradeStats.UpgradeWallRun(data.effectValue, data.effectValue2);
                break;

            // ?? Wall Jump (Up + Away — paired) ?????????????????????????????
            // effectValue  = flat added to wallJumpUpImpulse
            // effectValue2 = flat added to wallJumpAwayImpulse
            case CardEffect.CommonWallJumpUpgrade:
            case CardEffect.RareWallJumpUpgrade:
            case CardEffect.LegendaryWallJumpUpgrade:
                upgradeStats.UpgradeWallJump(data.effectValue, data.effectValue2);
                break;

            // ?? Dash Kill (Window + Kill Cap — paired) ?????????????????????
            // effectValue  = flat seconds added to hitWindowAfterDash
            // effectValue2 = flat int added to dashKillCap
            case CardEffect.CommonDashKillUpgrade:
            case CardEffect.RareDashKillUpgrade:
            case CardEffect.LegendaryDashKillUpgrade:
                upgradeStats.UpgradeDashKill(data.effectValue, (int)data.effectValue2);
                break;

            default:
                Debug.LogWarning("Unknown card effect: " + data.cardEffect);
                break;
        }
    }
}