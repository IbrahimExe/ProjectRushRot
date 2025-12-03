using UnityEngine;
using TMPro;

public class FinishLine : MonoBehaviour
{
    [Header("Timer Reference")]
    [SerializeField] private LevelTimer timer;

    [Header("Win Screen UI")]
    [SerializeField] private GameObject winScreen;   // The WinScreen panel
    [SerializeField] private TMP_Text finalTimeText; // TextMeshPro field showing time

    private bool levelCompleted = false; // Prevents multiple triggers

    private void OnTriggerEnter(Collider other)
    {
        if (levelCompleted) return; // safety guard

        if (other.CompareTag("Player"))
        {
            levelCompleted = true;

            // Stop the timer
            if (timer != null)
            {
                timer.StopTimer();
            }
            else
            {
                Debug.LogError("FinishLine: Timer reference is missing!");
                return;
            }

            // Display WinScreen
            if (winScreen != null)
            {
                winScreen.SetActive(true);
            }
            else
            {
                Debug.LogError("FinishLine: WinScreen reference is missing!");
            }

            // Show the final time
            if (finalTimeText != null)
            {
                finalTimeText.text = timer.GetFormattedTime();
            }
            else
            {
                Debug.LogError("FinishLine: FinalTimeText (TMP) reference is missing!");
            }

            // Pause the game
            Time.timeScale = 0f;

            // Unlock and show the cursor
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Debug.Log("LEVEL COMPLETED: Timer stopped + WinScreen displayed.");
        }
    }
}
