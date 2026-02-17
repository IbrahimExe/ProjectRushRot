using UnityEngine;
using UnityEngine.UIElements;

public class WallJumpAbility : MonoBehaviour
{
    [Header("Refs")]
    public PlayerControllerBase motor;
    public WallRunAbility wallRun;
    public DashAbility dash; // optional: blocks jump buffering during dash/flip
    public Transform aimTransform; // set to your camera (or player view) in inspector

    [Header("Wall Jump")]
    public float wallJumpUpImpulse = 7.5f;
    public float wallJumpAwayImpulse = 8.5f;

    [Tooltip("Extra push away from the wall (use this to make the kick-off stronger).")]
    public float extraAwayImpulse = 6f; 

    [Tooltip("Prevents repeated wall jumps in the same instant.")]
    public float wallJumpCooldown = 0.15f;

    //private float wallJumpLockUntil = 0f; <-------------------------------------------------------- modified this line
    private float nextWallJumpAllowedTime = 0f;

    private Rigidbody RB => motor != null ? motor.RB : null;

    public void TickFixed()
    {
        if (motor == null || wallRun == null || RB == null)
            return;

        // don't use buffered jump during dash/flip/side dash
        if (dash != null && (dash.IsDashing || dash.IsSideDashing || dash.IsDashFlipping))
            return;

        //if (Time.time < wallJumpLockUntil) <-------------------------------------------------------- modified this line
        //    return;

        bool WallJumpOnCooldown = Time.time < nextWallJumpAllowedTime;
        // -------------------------------------------------------------------------------------------------------

        // Jump buffer check comes from the base controller
        if (!motor.WantsJumpBuffered())
            return;

        //  WALL JUMP (controlled kick, no forward speed boost)
        if (wallRun.IsWallRunning && !WallJumpOnCooldown)
        {
            Vector3 n = wallRun.WallNormal.normalized; // should point away from wall
            Vector3 up = Vector3.up;

            // Stop wall-run so it doesn't fight the jump
            wallRun.ForceStopAndCooldown();

            // Read input
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            // Use camera/view if provided, else motor
            Transform t = (aimTransform != null) ? aimTransform : motor.transform;

            Vector3 camFwd = Vector3.ProjectOnPlane(t.forward, up).normalized;
            Vector3 camRight = Vector3.ProjectOnPlane(t.right, up).normalized;

            Vector3 desired = (camFwd * v + camRight * h);
            if (desired.sqrMagnitude < 0.0001f)
                desired = camFwd;

            desired.Normalize();

            // "Along wall" direction guided by input (remove into-wall component)
            Vector3 along = Vector3.ProjectOnPlane(desired, n);
            if (along.sqrMagnitude < 0.0001f)
                along = Vector3.ProjectOnPlane(camFwd, n);

            along.Normalize();

            // Blend: mostly away, some along-wall (sideways)
            float awayWeight = 0.75f;
            float alongWeight = 0.25f;

            Vector3 kickDir = (n * awayWeight + along * alongWeight).normalized;

            // ----------------------------------------------------------------
            // MOMENTUM PRESERVING WALL JUMP
            Vector3 vel = RB.linearVelocity;

            // Preserve the tangential (along-wall) velocity component
            Vector3 planarVel = Vector3.ProjectOnPlane(vel, up);
            Vector3 tangentialVel = Vector3.ProjectOnPlane(planarVel, n);
            
            // Remove only velocity going INTO the wall (negative dot product)
            float intoWall = Vector3.Dot(vel, n);
            if (intoWall < 0f)
                vel -= n * intoWall;

            // Add jump impulses (these ADD to existing momentum, not replace)
            float kickStrength = wallJumpAwayImpulse + extraAwayImpulse;
            vel += kickDir * kickStrength;
            vel += up * wallJumpUpImpulse;
            
            // Preserve the tangential momentum from wall running
            // Re-add the tangential component to maintain forward speed
            Vector3 newPlanar = Vector3.ProjectOnPlane(vel, up);
            Vector3 newTangential = Vector3.ProjectOnPlane(newPlanar, n);
            
            // If the new tangential is less than what we had, restore the original
            if (newTangential.magnitude < tangentialVel.magnitude)
            {
                vel += (tangentialVel - newTangential);
            }

            // Only clamp if the speed is extremely high
            // This prevents the clamp from reducing normal wall run speeds
            //float maxPlanar = 60f;
            //Vector3 planar = Vector3.ProjectOnPlane(vel, up);
            //if (planar.magnitude > maxPlanar)
            //    vel -= (planar - planar.normalized * maxPlanar);

            RB.linearVelocity = vel;

            // ----------------------------------------------------------------

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



        //NORMAL JUMP(ground +coyote)
        if (motor.IsGrounded || motor.CanCoyoteJump())
        {
            motor.DoNormalJump();
            //wallJumpLockUntil = Time.time + wallJumpCooldown;

            motor.ConsumeJumpBuffer();
            return;
        }

        // motor.ConsumeJumpBuffer();
    }
}
