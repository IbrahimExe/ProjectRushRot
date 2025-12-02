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
        Debug.Log("Initializing card: " + card.name);

        data = card;
        upgradeStats = upgrade ?? FindFirstObjectByType<UpgradeStats>();

        Debug.Log($"CardUI.Initialize: card={card?.name} upgradeStats={(upgradeStats != null)}");

        if (cardImage != null && card != null)
            cardImage.sprite = card.cardImage;

        if (cardText != null && card != null)
            cardText.text = card.cardText;
    }

    public void ApplyCardEffect()
    {
        if (data == null)
        {
            Debug.LogError("CardUI.ApplyCardEffect called but data is null on " + gameObject.name);
            return;
        }

        if (upgradeStats == null)
        {
            // last-resort fallback so you don't crash — still log so you can fix properly
            upgradeStats = FindFirstObjectByType<UpgradeStats>();
            if (upgradeStats == null)
            {
                Debug.LogError("CardUI.ApplyCardEffect: UpgradeStats not found. Cannot apply " + data.name);
                return;
            }
        }

        Debug.Log("Applying card effect: " + data.name + " effect=" + data.cardEffect + " value=" + data.effectValue);

        switch (data.cardEffect)
        {
            case CardEffect.MaxSpeedIncrease:
                upgradeStats.UpgradeMaxSpeedPercent(data.effectValue);
                break;

            case CardEffect.AccelerationIncrease:
                upgradeStats.UpgradeAccelerationPercent(data.effectValue);
                break;

            case CardEffect.JumpHeightIncrease:
                upgradeStats.UpgradeJumpForcePercent(data.effectValue);
                break;

            default:
                Debug.LogWarning("Unknown card effect: " + data.cardEffect);
                break;
        }
    }
}
