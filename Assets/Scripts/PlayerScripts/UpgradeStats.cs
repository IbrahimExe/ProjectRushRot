using UnityEngine;

public class UpgradeStats : MonoBehaviour
{
    public PlayerController2 player;

    private void Awake()
    {
        if (player == null)
        {
            player = GetComponent<PlayerController2>();
        }
    }

    public void UpgradeMaxSpeedPercent(float percent)
    {
        float amount = player.baseMaxMoveSpeed * percent;
        player.addMaxSpeed(amount);
    }

    public void UpgradeAccelerationPercent(float percent)
    {
        float amount = player.baseAcceleration * percent;
        player.addAcceleration(amount);
    }

    public void UpgradeJumpForcePercent(float percent)
    {
        float amount = player.baseJumpForce * percent;
        player.addJumpForce(amount);
    }
}
