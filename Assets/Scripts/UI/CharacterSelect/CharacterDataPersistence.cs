using UnityEngine;

/// <summary>
/// Singleton that persists the selected character data across scenes.
/// This allows the character selection from the menu to be passed to the game scene.
/// </summary>
public class CharacterDataPersistence : MonoBehaviour
{
    public static CharacterDataPersistence Instance { get; private set; }
    
    private PlayerCharacterData selectedCharacterData;

    private void Awake()
    {
        // Singleton pattern - ensure only one instance exists
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Set the character data to be used in the next scene.
    /// </summary>
    public void SetSelectedCharacter(PlayerCharacterData characterData)
    {
        if (characterData != null)
        {
            selectedCharacterData = characterData;
            Debug.Log($"Character selected: {characterData.name}");
        }
        else
        {
            Debug.LogError("Attempted to set null character data!");
        }
    }

    /// <summary>
    /// Get the currently selected character data.
    /// </summary>
    public PlayerCharacterData GetSelectedCharacter()
    {
        return selectedCharacterData;
    }

    /// <summary>
    /// Check if a character has been selected.
    /// </summary>
    public bool HasSelectedCharacter()
    {
        return selectedCharacterData != null;
    }

    /// <summary>
    /// Clear the selected character (useful for returning to menu).
    /// </summary>
    public void ClearSelectedCharacter()
    {
        selectedCharacterData = null;
    }
}
