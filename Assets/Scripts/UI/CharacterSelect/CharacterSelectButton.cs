using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;
using UnityEngine.EventSystems;

public class CharacterSelectButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private PlayerCharacterData characterData;
    private Button button;

    public event Action<PlayerCharacterData> OnCharacterSelected;

    [Header("Purchase UI Elements")]
    [SerializeField] private GameObject PurchasePanel;
    [SerializeField] private TMP_Text StoreTitle;
    [SerializeField] private TMP_Text CostText;
    [SerializeField] private Button BuyButton;
    [SerializeField] private Button CancelButton;
    [SerializeField] private Button BackgroundButton; // Covers the whole screen behind the panel
    
    private Color originalBuyButtonColor = Color.white;
    private Image buyButtonImage;
    private bool hasEnoughCoins = false;

    private void Start()
    {
        button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(SelectCharacter);
        }

        if (BuyButton != null)
        {
            buyButtonImage = BuyButton.GetComponent<Image>();
            if (buyButtonImage != null) originalBuyButtonColor = buyButtonImage.color;
            BuyButton.onClick.AddListener(OnBuyClicked);
        }

        if (CancelButton != null)
        {
            CancelButton.onClick.AddListener(OnCancelClicked);
        }

        if (BackgroundButton != null)
        {
            BackgroundButton.onClick.AddListener(OnCancelClicked);
        }

        CharacterSelectManager manager = FindFirstObjectByType<CharacterSelectManager>();
        if (manager != null)
        {
            // Auto-subscribe to the manager so we don't need to manually add this button to the manager's array
            OnCharacterSelected += manager.HandleCharacterSelected;
        }
        else
        {
            Debug.LogWarning("[CharacterSelectButton] CharacterSelectManager not found in scene.");
        }

        UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        if (button != null)
        {
            Image btnImage = button.GetComponent<Image>();
            if (btnImage != null)
            {
                if (characterData != null && !SkinUnlockManager.IsSkinUnlocked(characterData))
                {
                    // Tint the button gray if it's not unlocked
                    btnImage.color = new Color(0.3f, 0.3f, 0.3f, 1f); 
                }
                else
                {
                    // Restore to normal if unlocked
                    btnImage.color = Color.white; 
                }
            }
        }
    }

    public void SelectCharacter()
    {
        if (characterData == null)
        {
            Debug.LogWarning("[CharacterSelectButton] characterData is NULL! Assign a PlayerCharacterData in the Inspector.");
            return;
        }

        bool isUnlocked = SkinUnlockManager.IsSkinUnlocked(characterData);

        if (!isUnlocked)
        {
            OpenPurchasePanel();
        }
        else
        {
            OnCharacterSelected?.Invoke(characterData);
        }
    }

    private void OpenPurchasePanel()
    {
        if (PurchasePanel == null) return;
        
        PurchasePanel.SetActive(true);

        if (StoreTitle != null)
        {
            StoreTitle.text = $"Purchase {characterData.skinName}?";
        }

        int currentCoins = 0;
        if (InventoryManager.Instance != null)
        {
            currentCoins = InventoryManager.Instance.GetItemCount("Gold");
        }

        hasEnoughCoins = currentCoins >= characterData.coinCost;

        if (CostText != null)
        {
            if (hasEnoughCoins)
            {
                CostText.text = $"Cost: {characterData.coinCost}";
                CostText.color = Color.white;
            }
            else
            {
                CostText.text = $"Cost: {characterData.coinCost}\nNot enough coins";
                CostText.color = Color.red;
            }
        }

        if (BuyButton != null)
        {
            BuyButton.interactable = hasEnoughCoins;
            if (buyButtonImage != null)
            {
                Color c = originalBuyButtonColor;
                c.a = hasEnoughCoins ? 1f : 0.5f;
                buyButtonImage.color = c;
            }
        }
    }

    private void OnBuyClicked()
    {
        if (!hasEnoughCoins) return;

        if (InventoryManager.Instance != null)
        {
            int currentCoins = InventoryManager.Instance.GetItemCount("Gold");
            InventoryManager.Instance.SetItemCount("Gold", currentCoins - characterData.coinCost);
        }

        SkinUnlockManager.UnlockSkin(characterData);
        
        CharacterSelectManager manager = FindFirstObjectByType<CharacterSelectManager>();
        if (manager != null)
        {
            manager.UpdateCoinsUI();
        }

        // Just update the visual state to remove the gray tint, do not start the game
        UpdateVisualState();
        
        if (PurchasePanel != null)
        {
            PurchasePanel.SetActive(false);
        }
    }

    private void OnCancelClicked()
    {
        if (PurchasePanel != null)
        {
            PurchasePanel.SetActive(false);
        }
    }

    // Hover effects logic
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button != null && button.interactable)
        {
            
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (button != null && button.interactable)
        {
            
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(SelectCharacter);
        }
        if (BuyButton != null)
        {
            BuyButton.onClick.RemoveListener(OnBuyClicked);
        }
        if (CancelButton != null)
        {
            CancelButton.onClick.RemoveListener(OnCancelClicked);
        }
        if (BackgroundButton != null)
        {
            BackgroundButton.onClick.RemoveListener(OnCancelClicked);
        }
    }
}