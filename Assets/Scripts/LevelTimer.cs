using UnityEngine;
using TMPro; // Required for TextMeshPro UI

public class LevelTimer : MonoBehaviour
{
    private TextMeshProUGUI timerText;
    private float startTime;
    private bool isRunning = true; // Timer starts running when the script starts

    void Start()
    {
        // Get the TextMeshPro component attached to this GameObject
        timerText = GetComponent<TextMeshProUGUI>();

        // Record the time when the level/script officially starts
        startTime = Time.time;
    }

    void Update()
    {
        if (isRunning)
        {
            // Calculate elapsed time
            float t = Time.time - startTime;

            // Format time as Minutes:Seconds.Milliseconds
            // "00" ensures two digits are always displayed (e.g., 05 instead of 5)
            string minutes = ((int)t / 60).ToString("00");
            string seconds = (t % 60).ToString("00");
            string milliseconds = ((int)(t * 100) % 100).ToString("00");

            // Update the UI text
            timerText.text = $"Time: {minutes}:{seconds}.{milliseconds}";
        }
    }

    // Public method to be called when the player reaches the end of the level
    public void StopTimer()
    {
        isRunning = false;
        // At this point, t holds the final completion time.
        // You would typically save this time and send it to the leaderboard here.
    }

    // Public method to reset and start a new run (e.g., on level restart)
    public void ResetAndStart()
    {
        startTime = Time.time;
        isRunning = true;
    }
}