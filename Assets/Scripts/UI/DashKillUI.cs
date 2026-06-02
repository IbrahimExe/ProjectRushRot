using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DashKillUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The DashAbility on the player.")]
    public DashAbility dashAbility;

    [Header("Widget Root")]
    [Tooltip("The child panel to show/hide. Must be a DIFFERENT GameObject from the one " +
             "this component is attached to, otherwise disabling it kills Update.")]
    public GameObject widgetRoot;

    [Header("Multiplier Text")]
    [Tooltip("Displays  x(number)  e.g.  x6")]
    public TextMeshProUGUI multiplierText;

    [Header("Decay Timer Bar")]
    [Tooltip("Fill-type Image (Horizontal, Left origin) that drains as the combo expires.")]
    public Image timerFillImage;

    [Header("Pulse on Kill")]
    [Tooltip("RectTransform to pop-scale on each new kill. Usually the multiplier text object.")]
    public RectTransform pulseTarget;
    public float pulseScale = 1.4f;
    public float pulseDuration = 0.12f;

    [Header("Colours")]
    public Color colourLow = Color.white;                          // low multiplier
    public Color colourMid = new Color(1f, 0.85f, 0.2f, 1f);     // mid
    public Color colourHigh = new Color(1f, 0.35f, 0.1f, 1f);     // high

    [Header("Smoothing")]
    public float fillSmoothTime = 0.08f;

    // ── Private ─────────────────────────────────────────────────────
    private float fillVelocity;
    private int lastKillCount = 0;
    private float pulseTimer = 0f;
    private Vector3 baseScale = Vector3.one;

    private void Awake()
    {
        if (pulseTarget != null)
            baseScale = pulseTarget.localScale;

        // Start hidden 
        if (widgetRoot != null)
            widgetRoot.SetActive(false);
    }

    private void Start()
    {
        if (dashAbility == null)
            Debug.LogWarning("[DashKillUI] DashAbility not assigned — drag it in from the player.");

        if (widgetRoot == null)
            Debug.LogWarning("[DashKillUI] Widget Root not assigned.");
    }

    private void Update()
    {
        if (dashAbility == null || widgetRoot == null) return;

        int kills = dashAbility.DashKillCount;
        float timeLeft = dashAbility.DashKillTimeLeft;
        float duration = dashAbility.secondsToKeepMultiplier;
        float mult = AltExpManager.Instance != null
            ? AltExpManager.Instance.dashKillMultiplier : 1f;

        bool comboActive = kills > 0 && timeLeft > 0f;

        // ── Show / hide widget ─────────────────────────────────────
        widgetRoot.SetActive(comboActive);

        if (!comboActive)
        {
            // Reset so it's clean on next appearance
            if (timerFillImage != null) timerFillImage.fillAmount = 1f;
            if (pulseTarget != null) pulseTarget.localScale = baseScale;
            lastKillCount = 0;
            pulseTimer = 0f;
            return;
        }

        // ── Multiplier text  →  x6 ────────────────────────────────
        if (multiplierText != null)
        {
            multiplierText.text = $"x{mult:F0}";

            float t = Mathf.Clamp01((mult - 1f) / Mathf.Max(1f, 2f * dashAbility.dashKillCap - 1f));
            Color c = t < 0.5f
                ? Color.Lerp(colourLow, colourMid, t * 2f)
                : Color.Lerp(colourMid, colourHigh, (t - 0.5f) * 2f);

            multiplierText.color = c;
        }

        // ── Timer fill bar ─────────────────────────────────────────
        if (timerFillImage != null)
        {
            float targetFill = duration > 0f ? timeLeft / duration : 0f;
            timerFillImage.fillAmount = Mathf.SmoothDamp(
                timerFillImage.fillAmount, targetFill,
                ref fillVelocity, fillSmoothTime);

            // Drain colour: white → red in last 50%
            float urgency = Mathf.Clamp01(1f - targetFill * 2f);
            timerFillImage.color = Color.Lerp(Color.white, new Color(1f, 0.2f, 0.2f), urgency);
        }

        // ── Pulse on new kill ──────────────────────────────────────
        if (kills != lastKillCount)
        {
            lastKillCount = kills;
            pulseTimer = pulseDuration;
        }

        if (pulseTarget != null)
        {
            if (pulseTimer > 0f)
            {
                pulseTimer -= Time.unscaledDeltaTime;
                float p = 1f - Mathf.Clamp01(pulseTimer / pulseDuration);
                float s = 1f + Mathf.Sin(p * Mathf.PI) * (pulseScale - 1f);
                pulseTarget.localScale = baseScale * s;
            }
            else
            {
                pulseTarget.localScale = baseScale;
            }
        }
    }
}