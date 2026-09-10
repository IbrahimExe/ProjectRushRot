using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private Canvas mainMenuCanvas;
    [SerializeField] private Canvas characterSelectCanvas;
    [SerializeField] private CharacterSelectManager characterSelectManager;

    [Header("UI Buttons")]
    [SerializeField] private Button tutorialButton;
    [SerializeField] private Button startGameButton;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource mainMenuAudioSource;
    [SerializeField] private AudioClip mainMenuMusicClip;
    [SerializeField] private AudioClip hoverButtons;
    [SerializeField] private AudioClip clickButtons;
    [SerializeField] private AudioClip quitButton;
    [SerializeField] private AudioClip skateboardHover;
    [SerializeField] private AudioClip trolleyHover;
    [SerializeField] private AudioClip cheeseHover;
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

        if (mainMenuMusicClip != null)
        {
            mainMenuAudioSource.clip = mainMenuMusicClip;
            mainMenuAudioSource.Play();
        }
    }





    public void StartGame()
    {
        if (clickButtons != null && mainMenuAudioSource != null)
        {
            mainMenuAudioSource.PlayOneShot(clickButtons, volume);
        }
        SceneManager.LoadScene("IbrahimScene");
        //if (mainMenuAudioSource != null)
        //{
        //    mainMenuAudioSource.Stop();
        //}

    }

    public void StartProcedural()
    {
        if (clickButtons != null && mainMenuAudioSource != null)
        {
            mainMenuAudioSource.PlayOneShot(clickButtons, volume);
        }
        //if (mainMenuAudioSource != null)
        //{
        //    mainMenuAudioSource.Stop();
        //}
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

    public void PlayHoverSound()
    {
        if (hoverButtons != null && mainMenuAudioSource != null)
        {
            mainMenuAudioSource.PlayOneShot(hoverButtons, volume);
        }
    }

    public void PlaySelectSound()
    {
        if (clickButtons != null && mainMenuAudioSource != null)
        {
            mainMenuAudioSource.PlayOneShot(clickButtons, volume);
        }
    }

    public void PlaySkateboardHoverSound()
    {
        if (skateboardHover != null && mainMenuAudioSource != null)
        {
            mainMenuAudioSource.PlayOneShot(skateboardHover, volume);
        }
    }

    public void PlayTrolleyHoverSound()
    {
        if (trolleyHover != null && mainMenuAudioSource != null)
        {
            mainMenuAudioSource.PlayOneShot(trolleyHover, volume);
        }
    }

    public void PlayCheeseHoverSound()
    {
        if (cheeseHover != null && mainMenuAudioSource != null)
        {
            mainMenuAudioSource.PlayOneShot(cheeseHover, volume);
        }
    }
    public void PlayQuitSound()
    {
        if (quitButton != null && mainMenuAudioSource != null)
        {
            mainMenuAudioSource.PlayOneShot(quitButton, volume);
        }
    }
}