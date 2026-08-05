using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// Singleton confirmation dialog shown when the player clicks a locked, paid skin.
/// Wire up all references in the Inspector after running:
///   Tools → Rush Rot → Create Skin Purchase Dialog Panel
/// </summary>
public class SkinPurchaseDialog : MonoBehaviour
{
    public static SkinPurchaseDialog Instance { get; private set; }

    [Header("Panel Root")]
    [Tooltip("The root GameObject of the entire dialog — toggled active/inactive.")]
    [SerializeField] private GameObject dialogPanel;

    [Header("Text Fields")]
    [SerializeField] private TMP_Text skinNameText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text currentCoinsText;
    [SerializeField] private TMP_Text insufficientFundsText;

    [Header("Buttons")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    [Header("Colors")]
    [SerializeField] private Color affordableColor = new Color(0.2f, 0.9f, 0.4f, 1f);
    [SerializeField] private Color insufficientColor = new Color(0.95f, 0.25f, 0.25f, 1f);

    // Internal state ──────────────────────────────────────────────────────────

    private PlayerCharacterData pendingSkin;
    private Action<PlayerCharacterData> onConfirmed;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Wire buttons
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
        if (cancelButton != null)  cancelButton.onClick.AddListener(OnCancel);

        // Start hidden
        Hide();
    }

    private void OnDestroy()
    {
        if (confirmButton != null) confirmButton.onClick.RemoveListener(OnConfirm);
        if (cancelButton != null)  cancelButton.onClick.RemoveListener(OnCancel);
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Show the confirmation dialog for a locked skin.
    /// </summary>
    /// <param name="skinData">The skin the player wants to buy.</param>
    /// <param name="confirmedCallback">
    ///     Called with the skin data if the player presses Confirm and the
    ///     purchase succeeds. The caller is responsible for refreshing the UI.
    /// </param>
    public void Show(PlayerCharacterData skinData, Action<PlayerCharacterData> confirmedCallback)
    {
        if (skinData == null) return;

        pendingSkin  = skinData;
        onConfirmed  = confirmedCallback;

        PopulateTexts();

        if (dialogPanel != null) dialogPanel.SetActive(true);
    }

    public void Hide()
    {
        pendingSkin = null;
        onConfirmed = null;
        if (dialogPanel != null) dialogPanel.SetActive(false);
    }

    // ─── Internal helpers ─────────────────────────────────────────────────────

    private void PopulateTexts()
    {
        if (pendingSkin == null) return;

        int cost        = pendingSkin.coinCost;
        int playerCoins = InventoryManager.Instance != null
            ? InventoryManager.Instance.GetItemCount("Gold")
            : 0;
        bool canAfford  = playerCoins >= cost;

        // Skin name
        if (skinNameText != null)
        {
            string name = !string.IsNullOrEmpty(pendingSkin.skinName)
                ? pendingSkin.skinName
                : pendingSkin.name;
            skinNameText.text = $"Buy \"{name}\"?";
        }

        // Cost
        if (costText != null)
            costText.text = $"💰  Cost:  {cost:N0}";

        // Player's current wallet
        if (currentCoinsText != null)
        {
            currentCoinsText.text  = $"You have:  {playerCoins:N0} 💰";
            currentCoinsText.color = canAfford ? affordableColor : insufficientColor;
        }

        // Insufficient-funds warning
        if (insufficientFundsText != null)
            insufficientFundsText.gameObject.SetActive(!canAfford);

        // Confirm button only interactive when affordable
        if (confirmButton != null)
            confirmButton.interactable = canAfford;
    }

    private void OnConfirm()
    {
        if (pendingSkin == null) return;

        // Attempt to spend coins
        bool spent = InventoryManager.Instance != null &&
                     InventoryManager.Instance.SpendItem("Gold", pendingSkin.coinCost);

        if (!spent)
        {
            Debug.LogWarning($"[SkinPurchaseDialog] SpendItem failed for {pendingSkin.skinName}. " +
                             "Coins may have changed since dialog was opened.");
            Hide();
            return;
        }

        // Persist the unlock
        SkinUnlockManager.UnlockSkin(pendingSkin);

        // Notify the card that spawned this dialog
        onConfirmed?.Invoke(pendingSkin);

        Hide();
    }

    private void OnCancel()
    {
        Hide();
    }
}
