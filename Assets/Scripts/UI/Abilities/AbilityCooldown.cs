using System.Transactions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class AbilityCooldown : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image cooldownOverlay;
    [SerializeField] private PlayerControllerBase playerController;

    private float cooldownDuration;
    private float cooldownRemaining;
    private bool onCooldown;
    private bool wasDashing;

    void Start()
    {
        cooldownDuration = playerController.dash.dashCooldown;
        cooldownOverlay.fillAmount = 0f;
    }

    void Update()
    {
        bool isDashingNow = playerController.dash.IsDashing || playerController.dash.IsSideDashing;

        if (isDashingNow && !wasDashing)
        {
            cooldownRemaining = cooldownDuration;
            onCooldown = true;
        }

        wasDashing = isDashingNow;

        if (!onCooldown) return;

        cooldownRemaining -= Time.deltaTime;

        if (cooldownRemaining <= 0f)
        {
            cooldownRemaining = 0f;
            onCooldown = false;
            cooldownOverlay.fillAmount = 0f;
            return;
        }
        
        cooldownOverlay.fillAmount = cooldownRemaining / cooldownDuration;
    }
}
