using UnityEngine;

public class WallJumpAbility : MonoBehaviour
{
    [Header("Refs")]
    public PlayerControllerBase motor;
    public WallRunAbility wallRun;
    public DashAbility dash; // optional: blocks jump buffering during dash/flip

    [Header("Wall Jump")]
    public float wallJumpUpImpulse = 7.5f;
    public float wallJumpAwayImpulse = 8.5f;

    [Tooltip("Extra push away from the wall (use this to make the kick-off stronger).")]
    public float extraAwayImpulse = 6f; 

    [Tooltip("Prevents repeated wall jumps in the same instant.")]
    public float wallJumpCooldown = 0.15f;

    private float wallJumpLockUntil = 0f;

    private Rigidbody RB => motor != null ? motor.RB : null;

    public void TickFixed()
    {
        if (motor == null || wallRun == null || RB == null)
            return;

        // don't use buffered jump during dash/flip/side dash
        if (dash != null && (dash.IsDashing || dash.IsSideDashing || dash.IsDashFlipping))
            return;

        if (Time.time < wallJumpLockUntil)
            return;

        // Jump buffer check comes from the base controller
        if (!motor.WantsJumpBuffered())
            return;

        //  WALL JUMP (only if currently wall-running)
        if (wallRun.IsWallRunning)
        {
            Vector3 n = wallRun.WallNormal.normalized;
            Vector3 up = Vector3.up;

            // Stop wall-run so it doesn't fight the jump 
            wallRun.ForceStopAndCooldown();

            Vector3 vel = RB.linearVelocity;

            // Remove any planar velocity going into the wall (prevents re-sticking / dampened kick)
            Vector3 planar = Vector3.ProjectOnPlane(vel, up);
            float intoWall = Vector3.Dot(planar, -n);
            if (intoWall > 0f)
                planar += n * intoWall;

            // Stronger kick: keep planar momentum + add away impulse + add upward impulse
            Vector3 kickPlanar = planar + n * (wallJumpAwayImpulse + extraAwayImpulse);
            RB.linearVelocity = kickPlanar + up * wallJumpUpImpulse;

            wallJumpLockUntil = Time.time + wallJumpCooldown;
            motor.ConsumeJumpBuffer();
            return;
        }

        //  NORMAL JUMP (ground + coyote)
        if (motor.IsGrounded || motor.CanCoyoteJump())
        {
            motor.DoNormalJump();
            wallJumpLockUntil = Time.time + wallJumpCooldown;
            motor.ConsumeJumpBuffer();
            return;
        }

        // motor.ConsumeJumpBuffer();
    }
}
