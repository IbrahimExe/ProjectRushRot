using UnityEngine;
using UnityEngine.UI;
using System;

public class CharacterSelectButton : MonoBehaviour
{
    [SerializeField] private PlayerCharacterData characterData;
    [SerializeField] private Text characterNameText;
    [SerializeField] private Text characterStatsText;
    [SerializeField] private Image characterPreview;
    [SerializeField] private Button selectButton;

    public event Action<PlayerCharacterData> OnCharacterSelected;

    private void Start()
    {
        if (characterData == null)
        {
            Debug.LogError($"CharacterSelectButton on {gameObject.name} has no character data assigned!", gameObject);
            return;
        }

        // Display character information
        DisplayCharacterInfo();

        // Setup button click listener
        if (selectButton == null)
        {
            selectButton = GetComponent<Button>();
        }

        if (selectButton != null)
        {
            selectButton.onClick.AddListener(SelectCharacter);
        }
        else
        {
            Debug.LogWarning($"No button component found on {gameObject.name}", gameObject);
        }
    }

    private void DisplayCharacterInfo()
    {
        if (characterData == null)
            return;

        // Display character name
        if (characterNameText != null)
        {
            characterNameText.text = characterData.name;
        }

        // Display character stats - customize this based on what you want to show
        if (characterStatsText != null)
        {
            string stats = $"Speed: {characterData.maxMoveSpeed}\n" +
                          $"Acceleration: {characterData.acceleration}\n" +
                          $"Jump Force: {characterData.jumpForce}\n" +
                          $"Extra Jumps: {characterData.numOfJumps}\n" +
                          $"XP Multiplier: {characterData.xpMultiplierOnKill}x";
            
            characterStatsText.text = stats;
        }

        // Display character preview (optional)
        if (characterPreview != null && characterData.modelPrefab != null)
        {
            // Try to get an Image component from the model prefab for preview
            Image prefabImage = characterData.modelPrefab.GetComponent<Image>();
            if (prefabImage != null && prefabImage.sprite != null)
            {
                characterPreview.sprite = prefabImage.sprite;
            }
            else
            {
                // If no image found, you could assign a fallback or leave it blank
                Debug.Log($"No preview image found for {characterData.name}. Consider assigning one in the prefab.");
            }
        }

        Debug.Log($"Displayed info for character: {characterData.name}");
    }

    private void SelectCharacter()
    {
        OnCharacterSelected?.Invoke(characterData);
    }

    /// <summary>
    /// Allows you to programmatically set the character data for this button.
    /// Useful if you're dynamically creating buttons.
    /// </summary>
    public void SetCharacterData(PlayerCharacterData data)
    {
        characterData = data;
        DisplayCharacterInfo();
    }

    private void OnDestroy()
    {
        if (selectButton != null)
        {
            selectButton.onClick.RemoveListener(SelectCharacter);
        }
    }
}
