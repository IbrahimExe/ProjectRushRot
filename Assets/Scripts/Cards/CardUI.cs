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
        // Debug.Log("Initializing card: " + card.name);

        data = card;
        upgradeStats = upgrade ?? FindFirstObjectByType<UpgradeStats>();

        // Debug.Log($"CardUI.Initialize: card={card?.name} upgradeStats={(upgradeStats != null)}");

        if (cardImage != null && card != null)
            cardImage.sprite = card.cardImage;

        if (cardText != null && card != null)
            cardText.text = card.cardText;
    }

    public void ApplyCardEffect()
    {
        if (data == null)
        {
            // Debug.LogError("CardUI.ApplyCardEffect called but data is null on " + gameObject.name);
            return;
        }

        if (upgradeStats == null)
        {
            // last-resort fallback so you don't crash — still log so you can fix properly
            upgradeStats = FindFirstObjectByType<UpgradeStats>();
            if (upgradeStats == null)
            {
                // Debug.LogError("CardUI.ApplyCardEffect: UpgradeStats not found. Cannot apply " + data.name);
                return;
            }
        }

        // Debug.Log("Applying card effect: " + data.name + " effect=" + data.cardEffect + " value=" + data.effectValue);

        switch (data.cardEffect)
        {
            case CardEffect.CommonMaxSpeedUpgrade:
                upgradeStats.UpgradeMaxSpeedPercent(data.effectValue);
                break;
            case CardEffect.RareMaxSpeedUpgrade:
                upgradeStats.UpgradeMaxSpeedPercent(data.effectValue);
                break;
            case CardEffect.LegendaryMaxSpeedUpgrade:
                upgradeStats.UpgradeMaxSpeedPercent(data.effectValue);
                break;

            case CardEffect.CommonAccelerationUpgrade:
                upgradeStats.UpgradeAccelerationPercent(data.effectValue);
                break;
            case CardEffect.RareAccelerationUpgrade:
                upgradeStats.UpgradeAccelerationPercent(data.effectValue);
                break;
            case CardEffect.LegendaryAccelerationUpgrade:
                upgradeStats.UpgradeAccelerationPercent(data.effectValue);
                break;

            case CardEffect.CommonJumpHeightUpgrade:
                upgradeStats.UpgradeJumpForcePercent(data.effectValue);
                break;
            case CardEffect.RareJumpHeightUpgrade:
                upgradeStats.UpgradeJumpForcePercent(data.effectValue);
                break;
            case CardEffect.LegendaryJumpHeightUpgrade:
                upgradeStats.UpgradeJumpForcePercent(data.effectValue);
                break;

            case CardEffect.CommonWallRun:
                upgradeStats.UpgradeWallRunDuration(data.effectValue);
                break;
            case CardEffect.RareWallRun:
                upgradeStats.UpgradeWallRunDuration(data.effectValue);
                upgradeStats.UpgradeWallRunSpeed(data.effectValue);
                break;
            case CardEffect.LegendaryWallRun:
                upgradeStats.UpgradeWallJumpAwayImpulse(data.effectValue);
                upgradeStats.UpgradeWallJumpUpwardImpulse(data.effectValue);
                break;

            default:
                Debug.LogWarning("Unknown card effect: " + data.cardEffect);
                break;
        }
    }
}
