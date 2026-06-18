using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PerkCooldownUI : MonoBehaviour
{
    public PlayerAbilityRunner runner;
    public string abilityId;

    public GameObject visualRoot;
    public Image icon;
    public Image cooldownFill;
    public TMP_Text keyText;

    public bool showKeyPrompt = true;
    public string keyLabel = "Press 1";

    public float wiggleSpeed = 8f;
    public float wiggleAmount = 8f;

    private Vector3 startScale;
    private Quaternion startRotation;

    private void Awake()
    {
        SystemLoader.CallOnComplete(Initialize);
    }

    void Initialize()
    {
        startScale = transform.localScale;
        startRotation = transform.localRotation;

        if (keyText != null)
        {
            keyText.text = keyLabel;
            keyText.gameObject.SetActive(showKeyPrompt);
        }
    }

    private void Update()
    {
        if (runner == null || runner.Perks == null)
            return;

        RuntimePerk perk = null;

        foreach (RuntimePerk p in runner.Perks.ActivePerks)
        {
            if (p.ability.abilityId == abilityId)
            {
                perk = p;
                break;
            }
        }

        if (perk == null)
        {
            if (visualRoot != null)
                visualRoot.SetActive(false);
            return;
        }

        if (visualRoot != null)
            visualRoot.SetActive(true);

        float percent = perk.ability.GetCooldownPercent();

        if (cooldownFill != null)
            cooldownFill.fillAmount = percent;

        if (perk.ability.IsReady())
            Wiggle();
        else
            ResetVisual();
    }

    private void Wiggle()
    {
        float angle = Mathf.Sin(Time.time * wiggleSpeed) * wiggleAmount;
        transform.localRotation = startRotation * Quaternion.Euler(0f, 0f, angle);
        transform.localScale = startScale * 1.08f;
    }

    private void ResetVisual()
    {
        transform.localRotation = startRotation;
        transform.localScale = startScale;
    }
}