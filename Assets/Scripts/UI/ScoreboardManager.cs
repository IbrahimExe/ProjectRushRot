using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class ScoreboardManager : MonoBehaviour
{
    [Serializable]
    public class ScoreEntry
    {
        public string playerName;
        public int score;
        public float distance;
        public float time;
        public float averageSpeed;
        public string date;
    }

    [Serializable]
    private class ScoreboardData
    {
        public List<ScoreEntry> entries = new List<ScoreEntry>();
    }

    [Header("UI References")]
    [SerializeField] private GameObject resultsPanel;
    [SerializeField] private TextMeshProUGUI currentRunText;
    [SerializeField] private TextMeshProUGUI scoreboardText;
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private GameObject submitScoreButton;

    [Header("Scoreboard Settings")]
    [SerializeField] private int maximumEntries = 10;

    private const string SaveKey = "LocalScoreboard";

    private ScoreboardData scoreboardData = new ScoreboardData();

    private void Start()
    {
        LoadScoreboard();

        if (resultsPanel != null)
        {
            resultsPanel.SetActive(false);
        }

        RefreshScoreboardUI();
    }

    public void AddScore(
        string playerName,
        int score,
        float distance,
        float time,
        float averageSpeed)
    {
        Debug.Log("AddScore called");
        Debug.Log($"AddScore called: {playerName}, {score}");

        if (string.IsNullOrWhiteSpace(playerName))
        {
            playerName = "Player";
        }

        ScoreEntry newEntry = new ScoreEntry
        {
            playerName = playerName,
            score = score,
            distance = distance,
            time = time,
            averageSpeed = averageSpeed,
            date = DateTime.Now.ToString("yyyy-MM-dd")
        };

        scoreboardData.entries.Add(newEntry);

        scoreboardData.entries = scoreboardData.entries
            .OrderByDescending(entry => entry.score)
            .Take(maximumEntries)
            .ToList();

        SaveScoreboard();

        ShowCurrentRun(newEntry);
        RefreshScoreboardUI();

        if (resultsPanel != null)
        {
            resultsPanel.SetActive(true);
        }
    }

    private void ShowCurrentRun(ScoreEntry entry)
    {
        if (currentRunText == null)
            return;

        string formattedTime = FormatTime(entry.time);

        currentRunText.text =
            $"RUN COMPLETE\n\n" +
            $"Score: {entry.score:N0}\n" +
            $"Distance: {entry.distance:0.0} m\n" +
            $"Time: {formattedTime}\n" +
            $"Average Speed: {entry.averageSpeed:0.0} m/s";
    }

    public void SubmitPlayerScore()
    {
        Debug.Log("SubmitPlayerScore called.");

        if (scoreManager == null)
        {
            Debug.LogError("ScoreboardManager: ScoreManager reference is missing.");
            return;
        }

        string playerName = "Player";

        if (playerNameInput != null &&
            !string.IsNullOrWhiteSpace(playerNameInput.text))
        {
            playerName = playerNameInput.text.Trim();
        }

        scoreManager.SubmitPreparedRun(playerName);

        RefreshScoreboardUI();

        if (playerNameInput != null)
            playerNameInput.interactable = false;

        if (submitScoreButton != null)
            submitScoreButton.SetActive(false);
    }

    private void RefreshScoreboardUI()
    {
        if (scoreboardText == null)
            return;

        if (scoreboardData.entries == null ||
            scoreboardData.entries.Count == 0)
        {
            scoreboardText.text = "No scores yet.";
            return;
        }

        string display = "TOP SCORES\n\n";

        for (int i = 0; i < scoreboardData.entries.Count; i++)
        {
            ScoreEntry entry = scoreboardData.entries[i];

            display +=
                $"{i + 1}. {entry.playerName}    {entry.score:N0}\n";
        }

        scoreboardText.text = display;
    }

    private void SaveScoreboard()
    {
        string json = JsonUtility.ToJson(scoreboardData);

        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
    }

    private void LoadScoreboard()
    {
        if (!PlayerPrefs.HasKey(SaveKey))
        {
            scoreboardData = new ScoreboardData();
            return;
        }

        string json = PlayerPrefs.GetString(SaveKey);

        scoreboardData =
            JsonUtility.FromJson<ScoreboardData>(json);

        if (scoreboardData == null)
        {
            scoreboardData = new ScoreboardData();
        }

        if (scoreboardData.entries == null)
        {
            scoreboardData.entries =
                new List<ScoreEntry>();
        }

        scoreboardData.entries = scoreboardData.entries
            .OrderByDescending(entry => entry.score)
            .Take(maximumEntries)
            .ToList();
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

    public void HideResults()
    {
        if (resultsPanel != null)
        {
            resultsPanel.SetActive(false);
        }
    }

    [ContextMenu("Clear Scoreboard")]
    public void ClearScoreboard()
    {
        scoreboardData.entries.Clear();

        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();

        RefreshScoreboardUI();

        if (currentRunText != null)
        {
            currentRunText.text = "";
        }

        Debug.Log("Scoreboard cleared.");
    }
}