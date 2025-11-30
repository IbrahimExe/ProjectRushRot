using UnityEngine;

public class UpgradeStats : MonoBehaviour
{
    public GPlayerController player;
    
    public float maxSpeedPercentage = 0.15f;
    public float accelerationPercentage = 0.20f;
    public float jumpForcePercentage = 0.15f;

    private void Awake()
    {
        if (player == null)
        {
            player = GetComponent<GPlayerController>();
        }
    }

    // Increase max speed
    public void UpgradeMaxSpeed()
    {
        float amount = player.baseMaxMoveSpeed * maxSpeedPercentage;
        player.addMaxSpeed(amount);
    }

    // Increase acceleration
    public void UpgradeAcceleration()
    {
        float amount = player.baseAcceleration * accelerationPercentage;
        player.addAcceleration(amount);
    }

    // Increase jump force
    public void UpgradeJumpForce()
    {
        float amount = player.baseJumpForce * jumpForcePercentage;
        player.addJumpForce(amount);
    }
}
