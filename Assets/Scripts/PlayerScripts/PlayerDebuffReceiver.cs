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

    private void Awake()
    {
        player = GetComponent<PlayerControllerBase>();
        runner = GetComponent<PlayerAbilityRunner>();
    }

    public void ApplyDebuff(DebuffType type, float amount, float duration)
    {
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

        yield return new WaitForSeconds(duration);

        player.debuffMoveMultiplier = 1f;
        slowActive = false;
        slowRoutine = null;
    }

    private IEnumerator JumpRoutine(float amount, float duration)
    {
        jumpDebuffActive = true;

        player.debuffJumpMultiplier = amount;

        yield return new WaitForSeconds(duration);

        player.debuffJumpMultiplier = 1f;

        jumpDebuffActive = false;
        jumpRoutine = null;
    }

    private IEnumerator DashRoutine(float duration)
    {
        dashDebuffActive = true;

        if (player.dash != null)
            player.dash.enabled = false;

        yield return new WaitForSeconds(duration);

        if (player.dash != null)
            player.dash.enabled = true;

        dashDebuffActive = false;
        dashRoutine = null;
    }
}