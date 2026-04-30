using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages the character selection screen. Shows available characters and handles selection.
/// Integrates with the MainMenu to support both IbrahimScene and ProceduralLoading paths.
/// </summary>
public class CharacterSelectManager : MonoBehaviour
{
    [SerializeField] private CharacterSelectButton[] characterButtons;
    [SerializeField] private UnityEngine.UI.Button backButton;
    
    private string targetSceneName;
    private MainMenu mainMenu;

    private void Start()
    {
        // Get reference to MainMenu
        mainMenu = Object.FindFirstObjectByType<MainMenu>();

        // Setup button listeners for all character buttons
        foreach (CharacterSelectButton btn in characterButtons)
        {
            btn.OnCharacterSelected += OnCharacterSelected;
        }

        // Setup back button
        if (backButton != null)
        {
            backButton.onClick.AddListener(OnBackPressed);
        }

        Debug.Log($"CharacterSelectManager initialized with {characterButtons.Length} characters");
    }

    /// <summary>
    /// Set which scene to load when a character is selected.
    /// Called by MainMenu with either "IbrahimScene" or "ProceduralLoading"
    /// </summary>
    public void SetTargetScene(string sceneName)
    {
        targetSceneName = sceneName;
        Debug.Log($"Target scene set to: {sceneName}");
    }

    private void OnCharacterSelected(PlayerCharacterData characterData)
    {
        if (characterData == null)
        {
            Debug.LogError("Character data is null!");
            return;
        }

        Debug.Log($"Character selected: {characterData.name}");
        
        // Save the selection to persistence manager
        CharacterDataPersistence.Instance.SetSelectedCharacter(characterData);
        
        // Load the target scene
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError("Target scene name is not set!");
            return;
        }

        SceneManager.LoadScene(targetSceneName);
    }

    private void OnBackPressed()
    {
        Debug.Log("Back pressed - returning to main menu");
        
        // Call back handler on MainMenu
        if (mainMenu != null)
        {
            mainMenu.OnBackFromCharacterSelect();
        }
        else
        {
            Debug.LogWarning("MainMenu reference not found!");
        }
    }

    private void OnDestroy()
    {
        // Clean up listeners to prevent memory leaks
        foreach (CharacterSelectButton btn in characterButtons)
        {
            btn.OnCharacterSelected -= OnCharacterSelected;
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveListener(OnBackPressed);
        }
    }
}
