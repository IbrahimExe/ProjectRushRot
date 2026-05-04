using System;
using System.Reflection;
using UnityEngine;

public class AltExpManager : MonoBehaviour
{
    public static AltExpManager Instance { get; private set; }

    [Header("XP System Toggle")]
    [Tooltip("Master switch — disable to freeze all XP gain and level-ups. " +
             "Controlled automatically by XPSystemController based on scene name, " +
             "or manually via XPSystemController.Instance.EnableXPSystem() / DisableXPSystem().")]
    public bool xpSystemEnabled = false;

    [Header("XP Curve")]
    [Tooltip("Base XP required for level 1")]
    public int baseXP = 50;
    [Tooltip("Growth multiplier per level (e.g. 1.25 => 25% more each level)")]
    public float xpGrowth = 1.25f;

    [Header("XP Gain")]
    [Tooltip("Base XP gained per second")]
    public float baseXPPerSecond = 5f;

    [Header("Player Prefab")]
    [Tooltip("Drag your player GameObject (scene instance) here. If null the speed multiplier will not be driven by the player.")]
    public GameObject player;

    public enum SpeedSource
    {
        Auto,               // try Rigidbody -> CharacterController -> custom component
        Rigidbody3D,
        CharacterController,
        CustomComponent     // use customComponentTypeName + customSpeedFieldName
    }

    [Tooltip("Which component to use for reading player speed. Auto tries common components in order.")]
    public SpeedSource speedSource = SpeedSource.Auto;

    [Tooltip("When using CustomComponent, the name of the component class (e.g. PlayerMovement).")]
    public string customComponentTypeName = "GPlayerController";

    [Tooltip("When using CustomComponent, the name of the float field or property that contains the current speed (e.g. currentMoveSpeed).")]
    public string customSpeedFieldName = "currentMoveSpeed";

    [Tooltip("Reference (target) speed used to normalize the player's actual speed. Example: set this to approx top cruising speed.")]
    public float referenceSpeed = 70f;

    [Tooltip("How quickly the internal speedMultiplier smooths toward the target (higher = snappier).")]
    public float speedMultiplierSmoothing = 10f;

    [Header("Speed → XP tuning")]
    [Tooltip("Base multiplier (usually 1).")]
    public float multiplierBase = 1f;
    [Tooltip("Scale applied to the powered normalized speed. Bigger = stronger effect.")]
    public float multiplierScale = 1.5f;
    [Tooltip("Exponent applied to normalized speed")]
    public float speedExponent = 1.3f; // >1 emphasizes higher speeds.
    [Tooltip("Final Speed Multiplier Cap")]
    public float maxSpeedMultiplierCap = 4f;

    [Header("UI References")]
    [SerializeField] private LevelUpCardSelector levelUpUI;

    [HideInInspector] public float xp;
    [HideInInspector] public int level = 1;

    // External multipliers (set from other systems)
    [HideInInspector] public float speedMultiplier = 1f;
    [HideInInspector] public float globalMultiplier = 1f;
    [HideInInspector] public float dashKillMultiplier = 1f;

    // Events
    public event Action<int> OnLevelUp;

    // Internal
    private float targetSpeedMultiplier = 1f;

    // Cached references for performance
    private Rigidbody cachedRb;
    private CharacterController cachedChar;
    private Component cachedCustomComp;
    private FieldInfo cachedCustomField;
    private PropertyInfo cachedCustomProp;

    [Header("Debug")]
    public bool debugLog = true;

    [SerializeField] public AltXPBarUI xpBar;
    [SerializeField] public LevelUpCardSelector levelUpCardSelector;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        OnLevelUp += HandleLevelUp;
        CachePlayerComponents();
    }

    private void OnValidate()
    {
        CachePlayerComponents();
    }

    private void CachePlayerComponents()
    {
        cachedRb = null;
        cachedChar = null;
        cachedCustomComp = null;
        cachedCustomField = null;
        cachedCustomProp = null;

        if (player == null) return;

        if (speedSource == SpeedSource.Rigidbody3D || speedSource == SpeedSource.Auto)
            cachedRb = player.GetComponent<Rigidbody>();

        if (speedSource == SpeedSource.CharacterController || (speedSource == SpeedSource.Auto && cachedRb == null))
            cachedChar = player.GetComponent<CharacterController>();

        if (speedSource == SpeedSource.CustomComponent || (speedSource == SpeedSource.Auto && cachedRb == null && cachedChar == null))
        {
            var comps = player.GetComponents<MonoBehaviour>();
            foreach (var c in comps)
            {
                if (c == null) continue;
                var t = c.GetType();
                if (t.Name == customComponentTypeName)
                {
                    cachedCustomComp = c;
                    break;
                }
            }

            if (cachedCustomComp != null)
            {
                var t = cachedCustomComp.GetType();
                cachedCustomField = t.GetField(customSpeedFieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (cachedCustomField == null)
                    cachedCustomProp = t.GetProperty(customSpeedFieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
        }

        if (debugLog)
            Debug.Log($"[ExperienceManager] Cached — Rigidbody:{cachedRb != null}, CharacterController:{cachedChar != null}, Custom:{cachedCustomComp != null}");
    }

    private void Update()
    {
        // ── Master gate ──────────────────────────────────────────────
        // Everything below is skipped when the system is disabled.
        // Speed sampling still runs so the multiplier doesn't snap
        // when the system is re-enabled mid-session.
        if (!xpSystemEnabled)
        {
            if (player != null) SampleSpeed();
            return;
        }
        // ─────────────────────────────────────────────────────────────

        if (player != null) SampleSpeed();

        float xpThisFrame = baseXPPerSecond * Time.deltaTime * speedMultiplier * globalMultiplier * dashKillMultiplier;
        AddXP(xpThisFrame);
    }

    // Extracted so it can run regardless of xpSystemEnabled (avoids multiplier snap on re-enable)
    private void SampleSpeed()
    {
        float currentSpeed = GetPlayerSpeed();
        float normalized = (referenceSpeed > 0f) ? (currentSpeed / referenceSpeed) : 0f;
        float powered = Mathf.Pow(Mathf.Max(0f, normalized), speedExponent);
        float raw = multiplierBase + powered * multiplierScale;
        targetSpeedMultiplier = Mathf.Min(raw, maxSpeedMultiplierCap);
        speedMultiplier = Mathf.Lerp(speedMultiplier, targetSpeedMultiplier, Time.deltaTime * Mathf.Max(1f, speedMultiplierSmoothing));
    }

    private float GetPlayerSpeed()
    {
        if (cachedRb != null) return cachedRb.linearVelocity.magnitude;

        if (cachedChar != null)
        {
            try { return cachedChar.velocity.magnitude; }
            catch { return 0f; }
        }

        if (cachedCustomComp != null)
        {
            if (cachedCustomField != null)
            {
                var val = cachedCustomField.GetValue(cachedCustomComp);
                if (val is float f) return Mathf.Abs(f);
                if (val is double d) return Mathf.Abs((float)d);
                if (val is int i) return Mathf.Abs(i);
                if (val is Vector3 v) return v.magnitude;
            }
            if (cachedCustomProp != null)
            {
                var val = cachedCustomProp.GetValue(cachedCustomComp);
                if (val is float f) return Mathf.Abs(f);
                if (val is double d) return Mathf.Abs((float)d);
                if (val is int i) return Mathf.Abs(i);
                if (val is Vector3 v) return v.magnitude;
            }

            var t = cachedCustomComp.GetType();
            var p = t.GetProperty("currentMoveSpeed") ?? t.GetProperty("currentSpeed") ?? t.GetProperty("speed") ?? t.GetProperty("Velocity");
            if (p != null)
            {
                var val = p.GetValue(cachedCustomComp);
                if (val is float f2) return Mathf.Abs(f2);
                if (val is Vector3 v3) return v3.magnitude;
            }
        }

        return 0f;
    }

    public int XPRequiredForLevel(int lvl)
    {
        return Mathf.FloorToInt(baseXP * Mathf.Pow(xpGrowth, lvl - 1));
    }

    public void AddXP(float amount)
    {
        // Honour the gate even if AddXP is called externally
        if (!xpSystemEnabled) return;

        xp += amount;
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

    public void ResetLevel()
    {
        xp = 0f;
        level = 1;
        speedMultiplier = 1f;
        globalMultiplier = 1f;
        dashKillMultiplier = 1f;
        xpBar.ResetXPBar();
    }

    public void SetSpeedMultiplier(float m) => speedMultiplier = m;
    public void AddGlobalMultiplier(float m) => globalMultiplier *= m;
    public void SetDashKillMultiplier(float m) => dashKillMultiplier = Mathf.Max(1f, m);
}