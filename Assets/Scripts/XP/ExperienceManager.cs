using System;
using System.Reflection;
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

    [Header("Player Prefab")]
    [Tooltip("Drag your player GameObject (scene instance) here. If null the speed multiplier will not be driven by the player.")]
    public GameObject player;

    public enum SpeedSource
    {
        Auto,           // try Rigidbody -> CharacterController -> custom component
        Rigidbody3D,
        CharacterController,
        CustomComponent // use customComponentTypeName + customSpeedFieldName
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
    public float speedExponent = 1.3f; //  >1 emphasizes higher speeds.
    [Tooltip("Final Speed Multiplier Cap")]
    public float maxSpeedMultiplierCap = 4f; // Hard cap for the final speed multiplier (safety).

    [Header("UI References")]
    [SerializeField] private LevelUpImageSelector levelUpUI;

    [HideInInspector] public float xp; // current XP (float to allow smooth fill)
    [HideInInspector] public int level = 1;

    // External multipliers (set from other systems)
    [HideInInspector] public float speedMultiplier = 1f; // public read of current multiplier
    [HideInInspector] public float globalMultiplier = 1f; // other buffs

    // Events
    public event Action<int> OnLevelUp; // passes new level

    // internal
    private float targetSpeedMultiplier = 1f;

    // Cached references for performance
    private Rigidbody cachedRb;
    private CharacterController cachedChar;
    private Component cachedCustomComp;
    private FieldInfo cachedCustomField;
    private PropertyInfo cachedCustomProp;

    [Header("Debug")]
    public bool debugLog = true;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Subscribe level-up event to the UI — no FindObjectOfType
        OnLevelUp += HandleLevelUp;

        // Try to cache components if player already assigned in inspector
        CachePlayerComponents();
    }

    private void OnValidate()
    {
        // Keep inspector changes reflected at edit time
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
            // find by type name among MonoBehaviours on the player
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
                {
                    cachedCustomProp = t.GetProperty(customSpeedFieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
            }
        }

        if (debugLog)
        {
            Debug.Log($"[ExperienceManager] Cached components - Rigidbody:{cachedRb != null}, CharacterController:{cachedChar != null}, Custom:{cachedCustomComp != null}");
        }
    }

    private void Update()
    {
        // If a player is assigned, sample their speed and compute a target speedMultiplier.
        if (player != null)
        {
            float currentSpeed = GetPlayerSpeed();

            // Allow normalized to exceed 1 so >referenceSpeed increases multiplier further
            float normalized = (referenceSpeed > 0f) ? (currentSpeed / referenceSpeed) : 0f;

            // Apply exponent and scaling to emphasize higher speeds
            float powered = Mathf.Pow(Mathf.Max(0f, normalized), speedExponent);
            float raw = multiplierBase + powered * multiplierScale;

            // Clamp to a safe cap
            targetSpeedMultiplier = Mathf.Min(raw, maxSpeedMultiplierCap);

            // Smoothly move public speedMultiplier toward target so XP doesn't jitter
            speedMultiplier = Mathf.Lerp(speedMultiplier, targetSpeedMultiplier, Time.deltaTime * Mathf.Max(1f, speedMultiplierSmoothing));

            if (debugLog)
            {
                Debug.Log($"[ExperienceManager] playerSpeed={currentSpeed:F2}, " +
                    $"normalized={normalized:F3}, powered={powered:F3}, " +
                    $"targetMult={targetSpeedMultiplier:F3}, smoothed={speedMultiplier:F3}");
            }
        }

        // accumulate XP based on base + multipliers
        float xpThisFrame = baseXPPerSecond * Time.deltaTime * speedMultiplier * globalMultiplier;
        AddXP(xpThisFrame);
    }

    private float GetPlayerSpeed()
    {
        // 3D Rigidbody first
        if (cachedRb != null) return cachedRb.linearVelocity.magnitude;

        // CharacterController (if present)
        if (cachedChar != null)
        {
            // CharacterController doesn't expose velocity directly before Unity 2020; attempt to get via property if available
            // Fallback: estimate via difference in position (not implemented here) — so try 'velocity' property if exists.
            try
            {
                return cachedChar.velocity.magnitude;
            }
            catch
            {
                // if not available, return 0
                return 0f;
            }
        }

        // Custom component field / property
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

            // last-resort: look for common property names
            var t = cachedCustomComp.GetType();
            var p = t.GetProperty("currentMoveSpeed") ?? t.GetProperty("currentSpeed") ?? t.GetProperty("speed") ?? t.GetProperty("Velocity");
            if (p != null)
            {
                var val = p.GetValue(cachedCustomComp);
                if (val is float f2) return Mathf.Abs(f2);
                if (val is Vector3 v3) return v3.magnitude;
            }
        }

        // Nothing found
        return 0f;
    }

    public int XPRequiredForLevel(int lvl)
    {
        return Mathf.FloorToInt(baseXP * Mathf.Pow(xpGrowth, lvl - 1));
    }

    public void AddXP(float amount)
    {
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

    // Utility: set speed multiplier from other scripts manually (overrides automatic sampling for one frame)
    public void SetSpeedMultiplier(float m) => speedMultiplier = m;
    public void AddGlobalMultiplier(float m) => globalMultiplier *= m;
}
