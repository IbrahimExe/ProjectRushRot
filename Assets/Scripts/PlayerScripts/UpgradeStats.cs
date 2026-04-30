using System;
using UnityEngine;

public class UpgradeStats : MonoBehaviour
{
    public PlayerControllerBase player;

    private void Awake()
    {
        if (player == null)
            player = GetComponent<PlayerControllerBase>();
    }

    // ─────────────────────────────────────────────
    //  SINGLE STAT UPGRADES
    // ─────────────────────────────────────────────

    public void UpgradeMaxSpeedPercent(float percent)
    {
        float amount = player.baseMaxMoveSpeed * percent;
        player.addMaxSpeed(amount);
        Debug.Log($"Upgraded Max Speed by {amount}");
    }

    public void UpgradeAccelerationPercent(float percent)
    {
        float amount = player.baseAcceleration * percent;
        player.addAcceleration(amount);
        Debug.Log($"Upgraded Acceleration by {amount}");
    }

    public void UpgradeJumpForcePercent(float percent)
    {
        float amount = player.baseJumpForce * percent;
        player.addJumpForce(amount);
        Debug.Log($"Upgraded Jump Force by {amount}");
    }

    // ─────────────────────────────────────────────
    //  PAIRED UPGRADE: Wall Run (Duration + Speed)
    // ─────────────────────────────────────────────

    /// effectValue  = flat seconds added to wallRunDuration.
    /// effectValue2 = flat amount added to wallRunSpeedMultiplier.
 
    public void UpgradeWallRun(float durationFlat, float speedMultiplierFlat)
    {
        player.addWallRunDuration(durationFlat);
        player.addWallRunSpeed(speedMultiplierFlat);
        Debug.Log($"Upgraded Wall Run — Duration +{durationFlat}s, SpeedMultiplier +{speedMultiplierFlat}");
    }

    public void UpgradeWallRun(float flat) => UpgradeWallRun(flat, flat);

    // ─────────────────────────────────────────────
    //  PAIRED UPGRADE: Wall Jump (Up + Away Impulse)
    // ─────────────────────────────────────────────

    /// effectValue  = flat units added to wallJumpUpImpulse.
    /// effectValue2 = flat units added to wallJumpAwayImpulse.
    public void UpgradeWallJump(float upFlat, float awayFlat)
    {
        player.addWallJumpUpImpulse(upFlat);
        player.addWallJumpAwayImpulse(awayFlat);
        Debug.Log($"Upgraded Wall Jump — Up +{upFlat}, Away +{awayFlat}");
    }

    public void UpgradeWallJump(float flat) => UpgradeWallJump(flat, flat);

    // ─────────────────────────────────────────────
    //  PAIRED UPGRADE: Dash Kill (Window + Kill Cap)
    // ─────────────────────────────────────────────

    /// effectValue  = flat seconds added to hitWindowAfterDash.
    /// effectValue2 = flat integer added to dashKillCap.
    public void UpgradeDashKill(float windowFlat, int killCapFlat)
    {
        player.addDashKillWindow(windowFlat);
        player.addDashKillCount(killCapFlat);
        Debug.Log($"Upgraded Dash Kill — Window +{windowFlat}s, KillCap +{killCapFlat}");
    }

    internal void Initialize()
    {
        throw new NotImplementedException();
    }
}