using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardUI : MonoBehaviour
{
    private CardSO data;
    private UpgradeStats upgradeStats;
    private PlayerAbilityRunner abilityRunner;

    public Image cardImage;
    public TMP_Text cardText;

    public void Initialize(CardSO card, UpgradeStats upgrade)
    {
        data = card;
        upgradeStats = upgrade ?? FindFirstObjectByType<UpgradeStats>();
        abilityRunner = FindFirstObjectByType<PlayerAbilityRunner>();

        if (cardImage != null && card != null)
            cardImage.sprite = card.cardImage;

        if (cardText != null && card != null)
            cardText.text = card.cardText;
    }

    public void ApplyCardEffect()
    {
        if (data == null)
            return;

        if (upgradeStats == null)
            upgradeStats = FindFirstObjectByType<UpgradeStats>();

        if (abilityRunner == null)
            abilityRunner = FindFirstObjectByType<PlayerAbilityRunner>();

        Debug.Log($"Applying: {data.name} | Effect: {data.cardEffect}");

        switch (data.cardEffect)
        {
            case CardEffect.CommonMaxSpeedUpgrade:
            case CardEffect.RareMaxSpeedUpgrade:
            case CardEffect.LegendaryMaxSpeedUpgrade:
                upgradeStats.UpgradeMaxSpeedPercent(data.effectValue);
                break;

            case CardEffect.CommonAccelerationUpgrade:
            case CardEffect.RareAccelerationUpgrade:
            case CardEffect.LegendaryAccelerationUpgrade:
                upgradeStats.UpgradeAccelerationPercent(data.effectValue);
                break;

            case CardEffect.CommonJumpUpgrade:
            case CardEffect.RareJumpUpgrade:
            case CardEffect.LegendaryJumpUpgrade:
                upgradeStats.UpgradeJumpForcePercent(data.effectValue);
                break;

            case CardEffect.CommonWallRunUpgrade:
            case CardEffect.RareWallRunUpgrade:
            case CardEffect.LegendaryWallRunUpgrade:
                upgradeStats.UpgradeWallRun(data.effectValue, data.effectValue2);
                break;

            case CardEffect.CommonWallJumpUpgrade:
            case CardEffect.RareWallJumpUpgrade:
            case CardEffect.LegendaryWallJumpUpgrade:
                upgradeStats.UpgradeWallJump(data.effectValue, data.effectValue2);
                break;

            case CardEffect.CommonDashKillUpgrade:
            case CardEffect.RareDashKillUpgrade:
            case CardEffect.LegendaryDashKillUpgrade:
                upgradeStats.UpgradeDashKill(data.effectValue, (int)data.effectValue2);
                break;

            case CardEffect.ApplyPerk:
                if (abilityRunner == null)
                {
                    Debug.LogError("CardUI: PlayerAbilityRunner not found.");
                    return;
                }

                if (data.perkToApply == null)
                {
                    Debug.LogError("CardUI: No perk assigned on card " + data.name);
                    return;
                }

                abilityRunner.AddPerk(data.perkToApply);
                break;

            default:
                Debug.LogWarning("Unknown card effect: " + data.cardEffect);
                break;
        }
    }
}