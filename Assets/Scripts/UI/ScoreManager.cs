using UnityEngine;

[DisallowMultipleComponent]
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private DistanceTracker distanceTracker;
    [SerializeField] private ScoreboardManager scoreboardManager;

    [Header("Score Settings")]
    [Tooltip("Points awarded for each metre travelled.")]
    [SerializeField] private float distanceMultiplier = 1f;

    [Tooltip("Points awarded for each enemy or obstacle destroyed.")]
    [SerializeField] private float destructionMultiplier = 100f;

    private bool runPrepared;
    private bool runSubmitted;

    private float finalDistance;
    private int targetsDestroyed;
    private int finalScore;

    public float FinalDistance => finalDistance;
    public int TargetsDestroyed => targetsDestroyed;
    public int FinalScore => finalScore;

    public float DistanceMultiplier => distanceMultiplier;
    public float DestructionMultiplier => destructionMultiplier;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                "Multiple ScoreManager instances detected. " +
                "The newest instance will replace the previous reference."
            );
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void RegisterDestroyedTarget()
    {
        // Do not change a completed run after its results have been prepared.
        if (runPrepared)
            return;

        targetsDestroyed++;

        Debug.Log(
            $"Destroyed targets: {targetsDestroyed}"
        );
    }

    public void PrepareRun()
    {
        if (runPrepared)
            return;

        runPrepared = true;

        if (distanceTracker != null)
        {
            distanceTracker.StopTracking();
            finalDistance = distanceTracker.GetDistance();
        }
        else
        {
            Debug.LogError(
                "ScoreManager: DistanceTracker reference is missing."
            );

            finalDistance = 0f;
        }

        finalScore = Mathf.RoundToInt(
            finalDistance * distanceMultiplier +
            targetsDestroyed * destructionMultiplier
        );

        Debug.Log(
            $"Run prepared — Distance: {finalDistance:0.0} m, " +
            $"Destroyed: {targetsDestroyed}, Score: {finalScore}"
        );
    }

    public void SubmitPreparedRun(string playerName)
    {
        if (runSubmitted)
            return;

        if (!runPrepared)
            PrepareRun();

        if (string.IsNullOrWhiteSpace(playerName))
            playerName = "Player";

        if (scoreboardManager == null)
        {
            Debug.LogError(
                "ScoreManager: ScoreboardManager reference is missing."
            );

            return;
        }

        runSubmitted = true;

        scoreboardManager.AddScore(
            playerName.Trim(),
            finalScore,
            finalDistance,
            targetsDestroyed
        );
    }

    public void ResetRun()
    {
        runPrepared = false;
        runSubmitted = false;

        finalDistance = 0f;
        targetsDestroyed = 0;
        finalScore = 0;
    }
}