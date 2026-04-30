using UnityEngine;
using UnityEngine.SceneManagement;

// Manages the character selection screen
public class CharacterSelectManager : MonoBehaviour
{
    [SerializeField] private CharacterSelectButton[] characterButtons;
    [SerializeField] private string targetScene = "ProceduralLoading"; // "IbrahimScene"

    private void Start()
    {
        foreach (CharacterSelectButton btn in characterButtons)
        {
            btn.OnCharacterSelected += OnCharacterSelected;
        }
    }

    // Set which scene to load when a character is selected.
    public void SetTargetScene(string sceneName)
    {
        targetScene = sceneName;
    }

    private void OnCharacterSelected(PlayerCharacterData characterData)
    {
        if (characterData == null)
            return;

        // Save the selection to persistence manager
        CharacterDataPersistence.Instance.SetSelectedCharacter(characterData);

        SceneManager.LoadScene(targetScene);
    }

    private void OnDestroy()
    {
        // Clean up listeners to prevent memory leaks
        foreach (CharacterSelectButton btn in characterButtons)
        {
            btn.OnCharacterSelected -= OnCharacterSelected;
        }
    }
}