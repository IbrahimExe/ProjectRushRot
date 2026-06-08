using System.Collections;
using UnityEngine;

public class PlayerDebuffReceiver : MonoBehaviour
{
    private PlayerControllerBase player;
    private PlayerAbilityRunner runner;

    private Coroutine slowRoutine;
    private Coroutine jumpRoutine;
    private Coroutine dashRoutine;

    private bool slowActive;
    private bool jumpDebuffActive;
    private bool dashDebuffActive;

    public bool IsSlowActive => slowActive;
    public bool IsJumpDebuffActive => jumpDebuffActive;
    public bool IsDashDebuffActive => dashDebuffActive;

    public float SlowTimeLeft { get; private set; }
    public float JumpDebuffTimeLeft { get; private set; }
    public float DashDebuffTimeLeft { get; private set; }

    private void Awake()
    {
        player = GetComponent<PlayerControllerBase>();
        runner = GetComponent<PlayerAbilityRunner>();
    }

    public void ApplyDebuff(DebuffType type, float amount, float duration)
    {

        amount *= player.debuffAmountMultiplier;
        duration *= player.debuffDurationMultiplier;

        switch (type)
        {
            case DebuffType.Slow:
                if (slowActive)
                    return;

                slowRoutine = StartCoroutine(SlowRoutine(amount, duration));
                break;

            case DebuffType.ReducedJump:
                if (jumpDebuffActive)
                    return;

                jumpRoutine = StartCoroutine(JumpRoutine(amount, duration));
                break;

            case DebuffType.DisableDash:
                if (dashDebuffActive)
                    return;

                dashRoutine = StartCoroutine(DashRoutine(duration));
                break;
        }
    }

    private IEnumerator SlowRoutine(float amount, float duration)
    {
        slowActive = true;
        player.debuffMoveMultiplier = amount;
        SlowTimeLeft = duration;

        while (SlowTimeLeft > 0f)
        {
            SlowTimeLeft -= Time.deltaTime;
            yield return null;
        }

        player.debuffMoveMultiplier = 1f;
        slowActive = false;
        SlowTimeLeft = 0f;
    }

    private IEnumerator JumpRoutine(float amount, float duration)
    {
        jumpDebuffActive = true;
        JumpDebuffTimeLeft = duration;

        player.debuffJumpMultiplier = amount;

        while (JumpDebuffTimeLeft > 0f)
        {
            JumpDebuffTimeLeft -= Time.deltaTime;
            yield return null;
        }

        player.debuffJumpMultiplier = 1f;

        JumpDebuffTimeLeft = 0f;
        jumpDebuffActive = false;
        jumpRoutine = null;
    }

    private IEnumerator DashRoutine(float duration)
    {
        dashDebuffActive = true;
        DashDebuffTimeLeft = duration;

        if (player.dash != null)
            player.dash.enabled = false;

        while (DashDebuffTimeLeft > 0f)
        {
            DashDebuffTimeLeft -= Time.deltaTime;
            yield return null;
        }

        if (player.dash != null)
            player.dash.enabled = true;

        DashDebuffTimeLeft = 0f;
        dashDebuffActive = false;
        dashRoutine = null;
    }
}