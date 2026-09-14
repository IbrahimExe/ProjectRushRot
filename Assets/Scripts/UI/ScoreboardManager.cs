using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class ScoreboardManager : MonoBehaviour
{
    [Serializable]
    public class ScoreEntry
    {
        public string playerName;
        public int score;
        public float distance;
        public int targetsDestroyed;
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

    // New key prevents old time/average-speed entries from being loaded.
    private const string SaveKey = "LocalScoreboard_DistanceDestroyed_V2";

    private ScoreboardData scoreboardData = new ScoreboardData();
    private string currentRunDisplay = "";

    private void Start()
    {
        LoadScoreboard();

        if (playerNameInput != null)
        {
            playerNameInput.characterLimit = 15; 
            playerNameInput.onValidateInput = ValidateNameCharacter;
            playerNameInput.onValueChanged.AddListener(_ => ClearNameError());
        }

        if (resultsPanel != null)
            resultsPanel.SetActive(false);

        RefreshScoreboardUI();
    }

    public void AddScore(
        string playerName,
        int score,
        float distance,
        int targetsDestroyed)
    {
        if (!IsValidName(playerName) || NameAlreadyExists(playerName))
            return;

        ScoreEntry newEntry = new ScoreEntry
        {
            playerName = playerName,
            score = score,
            distance = distance,
            targetsDestroyed = targetsDestroyed,
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
            resultsPanel.SetActive(true);
    }

    public void PreviewRun(int score, float distance, int targetsDestroyed)
    {
        ScoreEntry previewEntry = new ScoreEntry
        {
            playerName = "Player",
            score = score,
            distance = distance,
            targetsDestroyed = targetsDestroyed
        };

        ShowCurrentRun(previewEntry);
        RefreshScoreboardUI();

        if (resultsPanel != null)
            resultsPanel.SetActive(true);

        if (playerNameInput != null)
            playerNameInput.interactable = true;

        if (submitScoreButton != null)
            submitScoreButton.SetActive(true);
    }

    private void ShowCurrentRun(ScoreEntry entry)
    {
        if (currentRunText == null)
            return;

        currentRunDisplay =
            $"<size=180%><b>RUN COMPLETE</b></size>\n\n" +

            $"Enemies Killed: {entry.targetsDestroyed}\n" +
            $"Distance Travelled: {entry.distance:0.0} m\n\n" +

            $"<size=140%><b>FINAL SCORE</b></size>\n" +
            $"<size=200%><b>{entry.score:N0}</b></size>";

        currentRunText.text = currentRunDisplay;
    }

    public void SubmitPlayerScore()
    {
        if (scoreManager == null)
        {
            Debug.LogError(
                "ScoreboardManager: ScoreManager reference is missing."
            );

            return;
        }

        string playerName = playerNameInput != null
            ? playerNameInput.text.Trim()
            : "";

        if (!IsValidName(playerName))
        {
            ShowNameError("Enter a name using letters and numbers only.");
            return;
        }

        if (NameAlreadyExists(playerName))
        {
            ShowNameError("That name has already been used.");
            return;
        }

        scoreManager.SubmitPreparedRun(playerName);

        RefreshScoreboardUI();

        if (playerNameInput != null)
            playerNameInput.interactable = false;

        if (submitScoreButton != null)
            submitScoreButton.SetActive(false);
    }

    private char ValidateNameCharacter(string text, int charIndex, char addedChar)
    {
        return IsAsciiLetterOrNumber(addedChar) ? addedChar : '\0';
    }

    private bool IsValidName(string playerName)
    {
        if (string.IsNullOrEmpty(playerName))
            return false;

        return playerName.All(IsAsciiLetterOrNumber);
    }

    private bool NameAlreadyExists(string playerName)
    {
        return scoreboardData.entries.Any(entry =>
            string.Equals(
                entry.playerName,
                playerName,
                StringComparison.OrdinalIgnoreCase));
    }

    private bool IsAsciiLetterOrNumber(char c)
    {
        return (c >= 'A' && c <= 'Z') ||
               (c >= 'a' && c <= 'z') ||
               (c >= '0' && c <= '9');
    }

    private void ShowNameError(string message)
    {
        if (currentRunText != null)
            currentRunText.text = currentRunDisplay + "\n\n" + message;
    }

    private void ClearNameError()
    {
        if (currentRunText != null && !string.IsNullOrEmpty(currentRunDisplay))
            currentRunText.text = currentRunDisplay;
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

        string display = "<size=130%><b>TOP SCORES</b></size>\n\n";

        for (int i = 0; i < scoreboardData.entries.Count; i++)
        {
            ScoreEntry entry = scoreboardData.entries[i];

            display +=
                $"{i + 1}. {entry.playerName}" +
                $"<pos=82%>{entry.score:N0}\n";
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
            scoreboardData = new ScoreboardData();

        if (scoreboardData.entries == null)
            scoreboardData.entries = new List<ScoreEntry>();

        scoreboardData.entries = scoreboardData.entries
            .OrderByDescending(entry => entry.score)
            .Take(maximumEntries)
            .ToList();
    }

    public void HideResults()
    {
        if (resultsPanel != null)
            resultsPanel.SetActive(false);
    }

    [ContextMenu("Clear Scoreboard")]
    public void ClearScoreboard()
    {
        scoreboardData.entries.Clear();

        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();

        RefreshScoreboardUI();

        if (currentRunText != null)
            currentRunText.text = "";

        Debug.Log("Scoreboard cleared.");
    }
}