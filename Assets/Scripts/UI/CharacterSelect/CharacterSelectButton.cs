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

        // Auto-subscribe to the manager — no need to manually add this button to the manager's array
        CharacterSelectManager manager = FindFirstObjectByType<CharacterSelectManager>();
        if (manager != null)
        {
            OnCharacterSelected += manager.HandleCharacterSelected;
        }
        else
        {
            Debug.LogWarning("[CharacterSelectButton] CharacterSelectManager not found in scene.");
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
            SkinPurchaseDialog.Instance.Show(characterData, OnPurchaseConfirmed);
        }
        else
        {
            OnCharacterSelected?.Invoke(characterData);
        }
    }

    private void OnPurchaseConfirmed(PlayerCharacterData purchasedSkin)
    {
        if (purchasedSkin != characterData) return;
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