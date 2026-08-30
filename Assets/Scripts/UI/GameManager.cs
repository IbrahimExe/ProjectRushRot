using LevelGenerator;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{

    [SerializeField] private ObjectPoolManager _poolManager;

    [Header("UI (assign in inspector)")]
    public GameObject pauseMenuUI;      // Full pause panel
    public GameObject countdownUI;      // Panel for countdown
    public TMP_Text countdownText;      // TextMeshPro countdown display

    [Header("Level-up selection")]
    [SerializeField] private LevelUpCardSelector levelUpCardSelector;

    [Header("Win / Lose screens (assign so pause is disabled while they are active)")]
    public GameObject winScreen;
    public GameObject loseScreen;

    [Header("Countdown settings")]
    public int resumeCountdownSeconds = 3;

    [Header("Gameplay objects to disable while paused")]
    public MonoBehaviour[] disableOnPause;

    [Header("Options")]
    public bool pauseAudio = false;
    public string mainMenuSceneName = "00_MainMenu";

    [Header("Score System")]
    [SerializeField] private ScoreManager scoreManager;

    private bool isPaused = false;
    private bool isCountingDown = false;

    //health
    [SerializeField] private float hp = 3f;

    [Header("Sounds")]
    [SerializeField] private AudioClip countDown;
    [SerializeField] private AudioManager audioManager;


    void Start()
    {
       SystemLoader.CallOnComplete(Initialize);
    }

    void Initialize()
    {
/*        GameState.StartGame();*/ //uncomment this line if you are not in the totorial or procedural level scene to be able to move
        GameState.ResetGame();
        if (pauseMenuUI)
            pauseMenuUI.SetActive(false);

        if (countdownUI)
            countdownUI.SetActive(false);

        if (winScreen)
            winScreen.SetActive(false);

        if (loseScreen)
            loseScreen.SetActive(false);

        if (_poolManager != null)
        {
            ServiceLocator.Register<ObjectPoolManager>(_poolManager);
            _poolManager.Initialize();

            Debug.Log(
                $"GameManager registered ObjectPoolManager: " +
                $"{_poolManager.GetInstanceID()}"
            );
        }
        else
        {
            Debug.LogError(
                "GameManager: ObjectPoolManager reference is missing."
            );
        }
    }

    void Update()
    {
        // If Win or Lose screen is active, ignore pause input entirely
        if (IsWinOrLoseActive()) return;

        // Toggle pause with ESC (as long as we're not in the countdown)
        if (Input.GetKeyDown(KeyCode.Escape) && !isCountingDown)
        {
            if (isPaused)
                StartCoroutine(ResumeWithCountdown());
            else
                PauseGame();
        }
    }

    public void PauseGame()
    {
        // Defensively don't pause if a win/lose screen is showing
        if (IsWinOrLoseActive() || GameState.IsStarted == false) return;

        if (pauseMenuUI) pauseMenuUI.SetActive(true);

        // Disable gameplay scripts
        foreach (var comp in disableOnPause)
            if (comp != null) comp.enabled = false;

        if (levelUpCardSelector != null)
            levelUpCardSelector.SetPaused(true);

        Time.timeScale = 0f;

        //if (pauseAudio)
        //    AudioListener.pause = true;

        if (audioManager != null)
            audioManager.PauseMusic();

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        isPaused = true;
    }

    public bool GetPausedState() => isPaused;

    public IEnumerator ResumeWithCountdown()
    {
        // If not paused or already counting down, do nothing
        if (!isPaused || isCountingDown) yield break;

        // If a win/lose screen became active while we waited, abort
        if (IsWinOrLoseActive()) yield break;

        isCountingDown = true;

        if (pauseMenuUI) pauseMenuUI.SetActive(false);
        if (countdownUI) countdownUI.SetActive(true);

        int seconds = Mathf.Max(1, resumeCountdownSeconds);

        //if (pauseAudio)
        //    AudioListener.pause = false;

        // Play countdown sound
        if (countDown != null)
            AudioSource.PlayClipAtPoint(countDown, Camera.main.transform.position);

        // Countdown while game is paused (use realtime)
        for (int s = seconds; s > 0; s--)
        {
            // If a win/lose screen becomes active mid-countdown, abort the countdown and keep game paused
            if (IsWinOrLoseActive())
            {
                if (countdownUI) countdownUI.SetActive(false);
                isCountingDown = false;
                yield break;
            }

            if (countdownText != null)
                countdownText.text = s.ToString();
            yield return new WaitForSecondsRealtime(1f);
        }

        // Flash "GO!"
        if (countdownText != null)
            countdownText.text = "RUSH!";
        yield return new WaitForSecondsRealtime(1.306f);

        if (countdownUI) countdownUI.SetActive(false);

        // Resume gameplay
        Time.timeScale = 1f;

        if (levelUpCardSelector != null)
            levelUpCardSelector.SetPaused(false);

        foreach (var comp in disableOnPause)
            if (comp != null) comp.enabled = true;

        //if (pauseAudio)
        //    AudioListener.pause = false;

        if (audioManager != null)
            audioManager.ResumeMusic();

        // Only hide and lock the cursor if neither Win nor Lose screens are active
        if (!IsWinOrLoseActive())
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        isPaused = false;
        isCountingDown = false;
    }

    public void ShowLoseScreen()
    {
        if (loseScreen != null && loseScreen.activeSelf)
            return;

        if (levelUpCardSelector != null)
            levelUpCardSelector.SetPlayerDead(true);

        if (scoreManager != null)
            scoreManager.PrepareRun();

        foreach (var comp in disableOnPause)
        {
            if (comp != null)
                comp.enabled = false;
        }

        if (pauseMenuUI)
            pauseMenuUI.SetActive(false);

        if (countdownUI)
            countdownUI.SetActive(false);

        if (loseScreen)
            loseScreen.SetActive(true);

        Time.timeScale = 0f;

        //if (pauseAudio)
        //    AudioListener.pause = true;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Debug.Log(scoreManager);
    }

    // Button Hooks
    public void OnResumeButton() => StartCoroutine(ResumeWithCountdown());

    public void OnRestartButton()
    {
        Time.timeScale = 1f;
        GameState.ResetGame();

        //if (pauseAudio)
        //    AudioListener.pause = false;

        PlayerAbilityRunner runner =
            FindFirstObjectByType<PlayerAbilityRunner>();

        if (runner != null)
            runner.ClearAllPerks();

        EndlessTerrain.CleanupForReload();

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }

    public void OnQuitToMenuButton()
    {
        //Time.timeScale = 1f;
        //if (pauseAudio) AudioListener.pause = false;

        PlayerAbilityRunner runner = FindFirstObjectByType<PlayerAbilityRunner>();
        if (runner != null) runner.ClearAllPerks();

        EndlessTerrain.CleanupForReload();

        if (!string.IsNullOrEmpty(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName);
        else
            Application.Quit();
    }



    // Utility: returns true if either winScreen or loseScreen is present and active in hierarchy
    private bool IsWinOrLoseActive()
    {
        if (winScreen != null && winScreen.activeInHierarchy) return true;
        if (loseScreen != null && loseScreen.activeInHierarchy) return true;
        return false;
    }

    // Helpers
}
