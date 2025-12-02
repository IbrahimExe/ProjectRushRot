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
        upgradeStats = upgrade;

        if (cardImage == null) Debug.LogError("cardImage is NULL!");
        if (cardText == null) Debug.LogError("cardText is NULL!");

        cardImage.sprite = card.cardImage;
        cardText.text = card.cardText;
    }

    public void ApplyCardEffect()
    {
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
        }
    }
}
