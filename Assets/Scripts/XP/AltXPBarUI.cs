using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AltXPBarUI : MonoBehaviour
{
    [Header("References")]
    public Image fillImage; // set to XPBar_Fill (fill type)
    public TextMeshProUGUI levelTextTMP; // optional
    public Text levelTextLegacy; // fallback if not using TMP

    [Header("Smoothing")]
    public float fillSmoothTime = 0.1f;
    private float currentFillVelocity;

    void Start()
    {
        if (AltExpManager.Instance == null)
        {
            Debug.LogError("No ExperienceManager found in scene.");
            enabled = false;
            return;
        }
    }

    void Update()
    {
        var em = AltExpManager.Instance;
        int required = em.XPRequiredForLevel(em.level);
        float targetFill = Mathf.Clamp01(em.xp / required);
        // smooth fill
        float f = Mathf.SmoothDamp(fillImage.fillAmount, targetFill, ref currentFillVelocity, fillSmoothTime);
        fillImage.fillAmount = f;

        string s = $"Lvl {em.level}  {Mathf.FloorToInt(em.xp)}/{required}";
        if (levelTextTMP) levelTextTMP.text = s;
        if (levelTextLegacy) levelTextLegacy.text = s;
    }
}
