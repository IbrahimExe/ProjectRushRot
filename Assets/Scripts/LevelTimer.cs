using UnityEngine;
using TMPro;

public class LevelTimer : MonoBehaviour
{
    private TextMeshProUGUI timerText;

    private float startTime;
    private float finalElapsedTime;

    private bool isRunning;

    private string finalFormattedTime = "00:00.00";

    private void Start()
    {
        SystemLoader.CallOnComplete(Initialize);
    }

    private void Initialize()
    {
        timerText = GetComponent<TextMeshProUGUI>();

        ResetAndStart();
    }

    private void Update()
    {
        if (!isRunning || timerText == null)
            return;

        float elapsedTime = Time.time - startTime;
        UpdateTimerDisplay(elapsedTime);
    }

    private void UpdateTimerDisplay(float elapsedTime)
    {
        int totalHundredths =
            Mathf.FloorToInt(elapsedTime * 100f);

        int minutes = totalHundredths / 6000;
        int seconds = (totalHundredths / 100) % 60;
        int hundredths = totalHundredths % 100;

        timerText.SetText(
            "{0:00}:{1:00}.{2:00}",
            minutes,
            seconds,
            hundredths
        );
    }

    public void StopTimer()
    {
        if (!isRunning)
            return;

        finalElapsedTime = Time.time - startTime;

        UpdateTimerDisplay(finalElapsedTime);

        isRunning = false;
        finalFormattedTime = timerText.text;
    }

    public void ResetAndStart()
    {
        startTime = Time.time;
        finalElapsedTime = 0f;

        finalFormattedTime = "00:00.00";
        isRunning = true;

        UpdateTimerDisplay(0f);
    }

    public float GetElapsedTime()
    {
        if (isRunning)
        {
            return Time.time - startTime;
        }

        return finalElapsedTime;
    }

    public string GetFormattedTime()
    {
        if (isRunning)
        {
            return FormatTime(Time.time - startTime);
        }

        return finalFormattedTime;
    }

    private string FormatTime(float elapsedTime)
    {
        int totalHundredths =
            Mathf.FloorToInt(elapsedTime * 100f);

        int minutes = totalHundredths / 6000;
        int seconds = (totalHundredths / 100) % 60;
        int hundredths = totalHundredths % 100;

        return $"{minutes:00}:{seconds:00}.{hundredths:00}";
    }

    public bool IsRunning()
    {
        return isRunning;
    }
}