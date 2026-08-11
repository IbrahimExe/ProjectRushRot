using UnityEngine;
using UnityEngine.UI;
using System;

public class CharacterSelectButton : MonoBehaviour
{
    [SerializeField] private PlayerCharacterData characterData;
    private Button button;

    public event Action<PlayerCharacterData> OnCharacterSelected;

    private void Start()
    {
        button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(SelectCharacter);
        }
    }

    public void SelectCharacter()
    {
        if (characterData == null) return;

        bool isUnlocked = SkinUnlockManager.IsSkinUnlocked(characterData);

        if (!isUnlocked)
        {
            if (SkinPurchaseDialog.Instance != null)
            {
                SkinPurchaseDialog.Instance.Show(characterData, OnPurchaseConfirmed);
            }
            else
            {
                Debug.LogWarning("[CharacterSelectButton] SkinPurchaseDialog not found in scene!");
            }
        }
        else
        {
            OnCharacterSelected?.Invoke(characterData);
        }
    }

    private void OnPurchaseConfirmed(PlayerCharacterData purchasedSkin)
    {
        if (purchasedSkin != characterData) return;
        
        // Once purchased, the player can instantly play by firing the event
        OnCharacterSelected?.Invoke(characterData);
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(SelectCharacter);
        }
    }
}