using System.Collections;
using UnityEngine;

public class PlayerDebuffReceiver : MonoBehaviour
{
    private PlayerControllerBase player;
    private PlayerAbilityRunner runner;

    private Coroutine slowRoutine;
    private Coroutine jumpRoutine;
    private Coroutine dashRoutine;

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
                if (slowRoutine != null)
                    StopCoroutine(slowRoutine);

                slowRoutine = StartCoroutine(SlowRoutine(amount, duration));
                break;

            case DebuffType.ReducedJump:
                if (jumpRoutine != null)
                    StopCoroutine(jumpRoutine);
                jumpRoutine = StartCoroutine(JumpRoutine(amount, duration));
                break;

            case DebuffType.DisableDash:
                if (dashRoutine != null)
                    StopCoroutine(dashRoutine);
                dashRoutine = StartCoroutine(DashRoutine(duration));
                break;
        }
    }

    private IEnumerator SlowRoutine(float amount, float duration)
    {
        player.debuffMoveMultiplier = amount;

        yield return new WaitForSeconds(duration);

        player.debuffMoveMultiplier = 1f;
        slowRoutine = null;
    }

    private IEnumerator JumpRoutine(float amount, float duration)
    {
        player.addJumpForce(-player.baseJumpForce * amount);

        yield return new WaitForSeconds(duration);

        if (runner != null)
            runner.RecalculateStats();
        else
            player.SetBaseStats();
    }

    private IEnumerator DashRoutine(float duration)
    {
        if (player.dash != null)
            player.dash.enabled = false;

        yield return new WaitForSeconds(duration);

        if (player.dash != null)
            player.dash.enabled = true;
    }
}