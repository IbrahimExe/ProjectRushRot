using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LevelTimer levelTimer;
    [SerializeField] private DistanceTracker distanceTracker;
    [SerializeField] private ScoreboardManager scoreboardManager;

    [Header("Score Settings")]
    [SerializeField] private float distanceMultiplier = 100f;
    [SerializeField] private float speedMultiplier = 500f;

    private bool runPrepared;
    private bool runSubmitted;

    private float finalTime;
    private float finalDistance;
    private float averageSpeed;
    private int finalScore;

    public void PrepareRun()
    {
        if (runPrepared)
            return;

        runPrepared = true;

        levelTimer.StopTimer();
        distanceTracker.StopTracking();

        finalTime = levelTimer.GetElapsedTime();
        finalDistance = distanceTracker.GetDistance();

        averageSpeed =
            finalDistance / Mathf.Max(finalTime, 0.01f);

        finalScore = Mathf.RoundToInt(
            finalDistance * distanceMultiplier +
            averageSpeed * speedMultiplier
        );
    }

    public void SubmitPreparedRun(string playerName)
    {
        Debug.Log($"Submitting score for {playerName}");
        if (runSubmitted)
            return;

        if (!runPrepared)
            PrepareRun();

        if (string.IsNullOrWhiteSpace(playerName))
            playerName = "Player";

        runSubmitted = true;

        scoreboardManager.AddScore(
            playerName.Trim(),
            finalScore,
            finalDistance,
            finalTime,
            averageSpeed
        );
    }

    public void ResetRun()
    {
        runPrepared = false;
        runSubmitted = false;

        finalTime = 0f;
        finalDistance = 0f;
        averageSpeed = 0f;
        finalScore = 0;
    }
}