using UnityEngine;

// Singleton that persists the selected character data across scenes.
// This allows the character selection from the menu to be passed to the game scene.
public class CharacterDataPersistence : MonoBehaviour
{
    public static CharacterDataPersistence Instance { get; private set; }

    private PlayerCharacterData selectedCharacterData;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetSelectedCharacter(PlayerCharacterData characterData)
    {
        if (characterData != null)
        {
            selectedCharacterData = characterData;
        }
    }

    public PlayerCharacterData GetSelectedCharacter()
    {
        return selectedCharacterData;
    }

    public bool HasSelectedCharacter()
    {
        return selectedCharacterData != null;
    }

    public void ClearSelectedCharacter()
    {
        selectedCharacterData = null;
    }
}