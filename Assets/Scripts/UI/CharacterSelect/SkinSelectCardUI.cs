using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class SkinSelectCardUI : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private GameObject lockOverlay;
    [SerializeField] private GameObject selectionBorder;
    [SerializeField] private Button cardButton;
    [SerializeField] private Image cardBackgroundImage;

    [Header("Colors")]
    [SerializeField] private Color unlockedBgColor = new Color(0.2f, 0.2f, 0.25f, 0.9f);
    [SerializeField] private Color lockedBgColor = new Color(0.12f, 0.12f, 0.15f, 0.8f);
    [SerializeField] private Color selectedBorderColor = new Color(1f, 0.84f, 0f, 1f);

    public PlayerCharacterData CharacterData { get; private set; }
    public bool IsUnlocked { get; private set; }
    public bool IsSelected { get; private set; }

    public event Action<SkinSelectCardUI> OnCardClicked;

    private void Awake()
    {
        if (cardButton != null)
        {
            cardButton.onClick.AddListener(OnClick);
        }
    }

    public void Setup(PlayerCharacterData data, Action<SkinSelectCardUI> clickCallback)
    {
        CharacterData = data;
        OnCardClicked = clickCallback;

        if (data != null)
        {
            if (nameText != null)
                nameText.text = !string.IsNullOrEmpty(data.skinName) ? data.skinName : data.name;

            if (iconImage != null)
            {
                if (data.skinIcon != null)
                {
                    iconImage.sprite = data.skinIcon;
                    iconImage.gameObject.SetActive(true);
                }
                else
                {
                    iconImage.gameObject.SetActive(false);
                }
            }

            if (costText != null)
                costText.text = $"💰 {data.coinCost}";
        }

        RefreshState(false);
    }

    public void RefreshState(bool selected)
    {
        IsSelected = selected;
        IsUnlocked = SkinUnlockManager.IsSkinUnlocked(CharacterData);

        if (lockOverlay != null)
            lockOverlay.SetActive(!IsUnlocked);

        if (selectionBorder != null)
            selectionBorder.SetActive(IsSelected);

        if (cardBackgroundImage != null)
            cardBackgroundImage.color = IsUnlocked ? unlockedBgColor : lockedBgColor;
    }

    private void OnClick()
    {
        if (CharacterData == null) return;

        if (!IsUnlocked)
        {
            // Show the confirmation dialog — purchase is handled there.
            if (SkinPurchaseDialog.Instance != null)
            {
                SkinPurchaseDialog.Instance.Show(CharacterData, OnPurchaseConfirmed);
            }
            else
            {
                // Fallback: no dialog in scene — warn and do nothing.
                Debug.LogWarning("[SkinSelectCardUI] SkinPurchaseDialog not found in scene! " +
                                 "Add the dialog panel and attach SkinPurchaseDialog.cs.");
            }
        }
        else
        {
            OnCardClicked?.Invoke(this);
        }
    }

    /// <summary>
    /// Called by SkinPurchaseDialog after the player confirms the purchase
    /// and the coins have already been spent + skin unlocked.
    /// </summary>
    private void OnPurchaseConfirmed(PlayerCharacterData purchasedSkin)
    {
        if (purchasedSkin != CharacterData) return;

        IsUnlocked = true;
        RefreshState(true);
        OnCardClicked?.Invoke(this);
    }

    private void OnDestroy()
    {
        if (cardButton != null)
        {
            cardButton.onClick.RemoveListener(OnClick);
        }
    }
}
