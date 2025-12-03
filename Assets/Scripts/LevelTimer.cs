using UnityEngine;
using TMPro; // Required for TextMeshPro UI

public class LevelTimer : MonoBehaviour
{
    private TextMeshProUGUI timerText;
    private float startTime;
    private bool isRunning = true;

    // NEW — stores the final formatted time
    private string finalFormattedTime = "00:00.00";

    void Start()
    {
        timerText = GetComponent<TextMeshProUGUI>();
        startTime = Time.time;
    }

    void Update()
    {
        if (isRunning)
        {
            float t = Time.time - startTime;

            string minutes = ((int)t / 60).ToString("00");
            string seconds = (t % 60).ToString("00");
            string milliseconds = ((int)(t * 100) % 100).ToString("00");

            timerText.text = $"{minutes}:{seconds}.{milliseconds}";
        }
    }

    public void StopTimer()
    {
        isRunning = false;

        // NEW — capture final displayed text
        finalFormattedTime = timerText.text;
    }

    public void ResetAndStart()
    {
        startTime = Time.time;
        isRunning = true;
    }

    // NEW — provides final time to FinishLine
    public string GetFormattedTime()
    {
        return finalFormattedTime;
    }
}
