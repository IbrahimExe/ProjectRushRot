using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private Canvas mainMenuCanvas;
    [SerializeField] private Canvas characterSelectCanvas;

    private CharacterSelectManager characterSelectManager;
    private bool isShowingCharacterSelect = false;

    private void Start()
    {
        // Ensure persistence manager exists
        if (CharacterDataPersistence.Instance == null)
        {
            new GameObject("CharacterDataPersistence").AddComponent<CharacterDataPersistence>();
        }

        // Get reference to CharacterSelectManager
        characterSelectManager = characterSelectCanvas.GetComponent<CharacterSelectManager>();

        // Make sure character select is hidden on startup
        if (characterSelectCanvas != null)
        {
            characterSelectCanvas.gameObject.SetActive(false);
        }
    }

    public void StartGame()
    {
        // Show character select instead of directly loading
        ShowCharacterSelect("IbrahimScene");
    }

    public void StartProcedural()
    {
        // Show character select for procedural mode
        ShowCharacterSelect("ProceduralLoading");
    }

    private void ShowCharacterSelect(string targetScene)
    {
        isShowingCharacterSelect = true;

        // Hide main menu and show character select
        if (mainMenuCanvas != null)
        {
            mainMenuCanvas.gameObject.SetActive(false);
        }

        if (characterSelectCanvas != null)
        {
            characterSelectCanvas.gameObject.SetActive(true);
        }

        // Set the target scene in the character select manager
        if (characterSelectManager != null)
        {
            characterSelectManager.SetTargetScene(targetScene);
        }
    }

    public void OnBackFromCharacterSelect()
    {
        isShowingCharacterSelect = false;

        // Show main menu and hide character select
        if (mainMenuCanvas != null)
        {
            mainMenuCanvas.gameObject.SetActive(true);
        }

        if (characterSelectCanvas != null)
        {
            characterSelectCanvas.gameObject.SetActive(false);
        }
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}