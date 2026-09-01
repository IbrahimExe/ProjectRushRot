using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private Canvas mainMenuCanvas;
    [SerializeField] private Canvas characterSelectCanvas;
    [SerializeField] private CharacterSelectManager characterSelectManager;

    [SerializeField] private AudioSource mainMenuAudioSource;
    [SerializeField][Range(0f, 1f)] private float volume = 1f;

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

        mainMenuAudioSource = GetComponent<AudioSource>();
        mainMenuAudioSource.loop = true;
        mainMenuAudioSource.volume = volume;

        mainMenuAudioSource.Play();
    }

    public void StartGame()
    {
        SceneManager.LoadScene("IbrahimScene");
        if (mainMenuAudioSource != null)
        {
            mainMenuAudioSource.Stop();
        }
    }

    public void StartProcedural()
    {
        if (mainMenuAudioSource != null)
        {
            mainMenuAudioSource.Stop();
        }
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