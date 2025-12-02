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

    [Header("Countdown settings")]
    public int resumeCountdownSeconds = 3;

    [Header("Gameplay objects to disable while paused")]
    public MonoBehaviour[] disableOnPause;

    [Header("Options")]
    public bool pauseAudio = true;
    public string mainMenuSceneName = "MainMenu";

    private bool isPaused = false;
    private bool isCountingDown = false;

    void Start()
    {
        if (pauseMenuUI) pauseMenuUI.SetActive(false);
        if (countdownUI) countdownUI.SetActive(false);
    }

    void Update()
    {
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
        if (!isPaused || isCountingDown)
            yield break;

        isCountingDown = true;

        if (pauseMenuUI) pauseMenuUI.SetActive(false);
        if (countdownUI) countdownUI.SetActive(true);

        int seconds = Mathf.Max(1, resumeCountdownSeconds);

        // Countdown while game is paused (use realtime)
        for (int s = seconds; s > 0; s--)
        {
            if (countdownText != null)
                countdownText.text = s.ToString();
            yield return new WaitForSecondsRealtime(1f);
        }

        // Flash "GO!"
        if (countdownText != null)
            countdownText.text = "GO!";
        yield return new WaitForSecondsRealtime(0.35f);

        if (countdownUI) countdownUI.SetActive(false);

        // Resume gameplay
        Time.timeScale = 1f;

        if (pauseAudio)
            AudioListener.pause = false;

        foreach (var comp in disableOnPause)
            if (comp != null) comp.enabled = true;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

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

        if (!string.IsNullOrEmpty("MainMenu"))
            SceneManager.LoadScene(0);
        else
            Application.Quit();
    }
}
