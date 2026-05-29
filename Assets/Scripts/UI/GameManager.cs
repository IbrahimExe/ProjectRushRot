using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    [Header("UI (assign in inspector)")]
    public GameObject pauseMenuUI;      // Full pause panel
    public GameObject countdownUI;      // Panel for countdown
    public TMP_Text countdownText;      // TextMeshPro countdown display

    [Header("Win / Lose screens (assign so pause is disabled while they are active)")]
    public GameObject winScreen;
    public GameObject loseScreen;

    [Header("Countdown settings")]
    public int resumeCountdownSeconds = 3;

    [Header("Gameplay objects to disable while paused")]
    public MonoBehaviour[] disableOnPause;

    [Header("Options")]
    public bool pauseAudio = true;
    public string mainMenuSceneName = "00_MainMenu";

    private bool isPaused = false;
    private bool isCountingDown = false;

    //health
    [SerializeField] private float hp = 3f;

    void Start()
    {
        if (pauseMenuUI) pauseMenuUI.SetActive(false);
        if (countdownUI) countdownUI.SetActive(false);
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
        if (IsWinOrLoseActive()) return;

        if (pauseMenuUI) pauseMenuUI.SetActive(true);

        // Disable gameplay scripts
        foreach (var comp in disableOnPause)
            if (comp != null) comp.enabled = false;

        Time.timeScale = 0f;

        if (pauseAudio)
            AudioListener.pause = true;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        isPaused = true;
    }

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
        yield return new WaitForSecondsRealtime(0.35f);

        if (countdownUI) countdownUI.SetActive(false);

        // Resume gameplay
        Time.timeScale = 1f;

        if (pauseAudio)
            AudioListener.pause = false;

        foreach (var comp in disableOnPause)
            if (comp != null) comp.enabled = true;

        // Only hide and lock the cursor if neither Win nor Lose screens are active
        if (!IsWinOrLoseActive())
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        isPaused = false;
        isCountingDown = false;
    }

    // Button Hooks
    public void OnResumeButton() => StartCoroutine(ResumeWithCountdown());

    public void OnRestartButton()
    {
        Time.timeScale = 1f;
        if (pauseAudio) AudioListener.pause = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void OnQuitToMenuButton()
    {
        Time.timeScale = 1f;
        if (pauseAudio) AudioListener.pause = false;

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
}
