using UnityEngine;

public class WallJumpAbility : MonoBehaviour
{
    [Header("Refs")]
    public PlayerControllerBase motor;
    public WallRunAbility wallRun;
    public DashAbility dash;

    [Header("Wall Jump")]
    public float wallJumpUpImpulse = 7.5f;
    public float wallJumpAwayImpulse = 8.5f;
    public float extraAwayImpulse = 6f;
    public float wallJumpCooldown = 0.15f;
    //private float wallJumpLockUntil = 0f; <-------------------------------------------------------- modified this line
    private float nextWallJumpAllowedTime = 0f;

    [Header("Side Influence")]
    [Range(0f, 1f)] public float alongWallInfluence = 0.25f;

    [Header("Smooth Kick Ramp")]
    public float wallKickRampTime = 0.08f;

    private float wallJumpLockUntil = 0f;

    private Vector3 pendingWallKick = Vector3.zero;
    private float wallKickEndTime = 0f;

    private Rigidbody RB => motor != null ? motor.RB : null;

    public void TickFixed()
    {
        if (motor == null || wallRun == null || RB == null)
            return;

        // If dash owns movement, don't start a jump here
        if (dash != null && (dash.IsDashing || dash.IsSideDashing || dash.IsDashFlipping))
            return;

        // 1) ALWAYS apply any pending ramp kick (independent of jump buffering)
        ApplyPendingWallKick();

        // 2) If no buffered jump, do nothing else
        if (!motor.WantsJumpBuffered())
            return;

        // 3) Prefer wall jump when wallrunning
        if (wallRun.IsWallRunning)
        {
            TryDoWallJump();
            return; // IMPORTANT: don't also do normal jump in same tick
        }

        // 4) Otherwise do normal jump (ground or coyote)
        if (motor.IsGrounded || motor.CanCoyoteJump())
        {
            motor.DoNormalJump();
            motor.ConsumeJumpBuffer();
        }
    }

    private void ApplyPendingWallKick()
    {
        if (Time.time >= wallKickEndTime) return;
        if (pendingWallKick == Vector3.zero) return;

        float frac = Time.fixedDeltaTime / Mathf.Max(0.01f, wallKickRampTime);
        frac = Mathf.Clamp01(frac);

        Vector3 step = pendingWallKick * frac;

        RB.AddForce(step, ForceMode.VelocityChange);
        pendingWallKick -= step;

        if (pendingWallKick.magnitude < 0.01f)
            pendingWallKick = Vector3.zero;
    }

    private void TryDoWallJump()
    {
        if (Time.time < wallJumpLockUntil)
            return;

        // Cache wall info FIRST (ForceStop clears state)
        Vector3 n = wallRun.WallNormal.normalized;   // away from wall
        Vector3 t = wallRun.WallTangent.normalized;  // along wall

        // Safety: if for any reason normals are invalid, bail to avoid zero-kick
        if (n.sqrMagnitude < 0.5f || t.sqrMagnitude < 0.5f)
            return;

        wallRun.ForceStopAndCooldown();

        // Clear planar velocity so kick isn't overwritten by current movement
        Vector3 vel = RB.linearVelocity;
        vel = Vector3.Project(vel, Vector3.up);
        RB.linearVelocity = vel;

        // Kick direction (mostly away, a bit along wall for style)
        Vector3 kickDir = (n * (1f - alongWallInfluence) + t * alongWallInfluence).normalized;

        // Up impulse happens instantly
        RB.AddForce(Vector3.up * wallJumpUpImpulse, ForceMode.VelocityChange);

        // Away impulse ramps smoothly (prevents yank)
        pendingWallKick = kickDir * (wallJumpAwayImpulse + extraAwayImpulse);
        wallKickEndTime = Time.time + Mathf.Max(0.01f, wallKickRampTime);

        // Prevent BaseMove from erasing kick this frame / next couple ticks
        motor.MoveLockTimer = 0.12f;

            // added this block ---------------------------------------------------------
            // Prevent immediate re-entry into wallrun
            wallRun.ForceStopAndCooldown();
            wallRun.LockWallRun(0.2f); // we need to tweek this number if needed, maybe make it variable
            // ---------------------------------------------------------

            // Lock out air-upright briefly so the kick isn't visually cancelled
            motor.NotifyWallJump();

            // ---------------------------------------------------------
            //wallJumpLockUntil = Time.time + wallJumpCooldown;
            nextWallJumpAllowedTime = Time.time + wallJumpCooldown;
            // ---------------------------------------------------------

            motor.ConsumeJumpBuffer();
            return;
        }
}
