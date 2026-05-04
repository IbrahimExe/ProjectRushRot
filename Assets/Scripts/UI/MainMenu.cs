using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private Canvas mainMenuCanvas;
    [SerializeField] private Canvas characterSelectCanvas;
    [SerializeField] private CharacterSelectManager characterSelectManager;

    private void Start()
    {
        if (characterSelectCanvas != null)
        {
            characterSelectCanvas.gameObject.SetActive(false);
        }

        // Ensure persistence manager exists
        if (CharacterDataPersistence.Instance == null)
        {
            new GameObject("CharacterDataPersistence").AddComponent<CharacterDataPersistence>();
        }
    }

    public void StartGame()
    {
        ShowCharacterSelect("IbrahimScene");
    }

    public void StartProcedural()
    {
        ShowCharacterSelect("ProceduralLoading");
    }

    private void ShowCharacterSelect(string sceneName)
    {
        if (mainMenuCanvas != null)
            mainMenuCanvas.gameObject.SetActive(false);

        if (characterSelectCanvas != null)
            characterSelectCanvas.gameObject.SetActive(true);

        if (characterSelectManager != null)
            characterSelectManager.SetTargetScene(sceneName);
    }

    public void OnBackFromCharacterSelect()
    {
        if (mainMenuCanvas != null)
            mainMenuCanvas.gameObject.SetActive(true);

        if (characterSelectCanvas != null)
            characterSelectCanvas.gameObject.SetActive(false);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}