using System;
using UnityEngine;

public class ExperienceManager : MonoBehaviour
{
    public static ExperienceManager Instance { get; private set; }

    [Header("XP Curve")]
    [Tooltip("Base XP required for level 1")]
    public int baseXP = 50;
    [Tooltip("Growth multiplier per level (e.g. 1.25 => 25% more each level)")]
    public float xpGrowth = 1.25f;

    [Header("XP Gain")]
    [Tooltip("Base XP gained per second")]
    public float baseXPPerSecond = 5f;

    [Header("UI References")]
    [SerializeField] private LevelUpImageSelector levelUpUI;

    [HideInInspector] public float xp; // current XP (float to allow smooth fill)
    [HideInInspector] public int level = 1;

    // External multipliers (set from other systems)
    [HideInInspector] public float speedMultiplier = 1f; // e.g., player speed affects gain
    [HideInInspector] public float globalMultiplier = 1f; // other buffs

    // Events
    public event Action<int> OnLevelUp; // passes new level

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // Optionally: DontDestroyOnLoad(gameObject);
        // Subscribe level-up event to the UI — no FindObjectOfType
        OnLevelUp += HandleLevelUp;
    }

    private void Update()
    {
        // accumulate XP based on base + multipliers
        float xpThisFrame = baseXPPerSecond * Time.deltaTime * speedMultiplier * globalMultiplier;
        AddXP(xpThisFrame);
    }

    public int XPRequiredForLevel(int lvl)
    {
        // Example: baseXP * xpGrowth^(lvl-1)
        return Mathf.FloorToInt(baseXP * Mathf.Pow(xpGrowth, lvl - 1));
    }

    public void AddXP(float amount)
    {
        xp += amount;
        // check for level up(s) — allow multiple levels if xp is big
        while (xp >= XPRequiredForLevel(level))
        {
            xp -= XPRequiredForLevel(level);
            level++;
            OnLevelUp?.Invoke(level);
        }
    }
    private void HandleLevelUp(int newLevel)
    {
        if (levelUpUI != null)
            levelUpUI.TriggerLevelUp();
        else
            Debug.LogWarning("ExperienceManager: LevelUpUI reference is missing.");
    }


    // Utility: set speed multiplier from other scripts
    public void SetSpeedMultiplier(float m) => speedMultiplier = m;
    public void AddGlobalMultiplier(float m) => globalMultiplier *= m;
}