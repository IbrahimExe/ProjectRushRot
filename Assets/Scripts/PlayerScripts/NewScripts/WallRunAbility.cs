using UnityEngine;

public class WallRunAbility : MonoBehaviour
{
    [Header("Refs")]
    public PlayerControllerBase motor;
    public DashAbility dash; // block wallrun while dashing
    public Transform cartModel; // for wall visual alignment

    [Header("Wall Surface")]
    public LayerMask wallLayers = ~0;
    public float wallCheckRadius = 1f;
    public float wallCheckDistance = 2f;

    [Header("Wall Run")]
    public bool wallRunEnabled = true;
    public float wallRunSpeedMultiplier = 1.0f;   // multiplier 
    public float wallRunDuration = 4f;
    public float wallRunMinHeight = 1.1f;
    public float wallRunMinForwardDot = 0.2f;
    public float wallRunCooldown = 1f;
    public float wallRunStick = 0.5f;

    [Header("Wall Run Vertical (Sands of Time)")]
    public float wallRunRiseDuration = 1.0f;
    public float wallRunRiseSpeed = 3.0f;
    [Range(0f, 1f)] public float wallRunGravityScale = 0.08f;
    public float wallRunSlowFallMaxDownSpeed = -1.25f;
    public float wallRunMaxUpSpeed = 6f;

    [Header("Tangent Control (no-yeet)")]
    public float wallRunTangentAccel = 30f;
    public float wallRunEntryBoost = 2.0f;

    [Header("Wall Facing")]
    public float wallRunFaceTurnLerp = 12f;

    public bool IsWallRunning => isWallRunning;
    public Vector3 WallNormal => wallRunNormal;
    public Vector3 WallTangent => wallRunTangent;

    private bool isWallRunning = false;
    private Vector3 wallRunNormal = Vector3.zero;
    private Vector3 wallRunTangent = Vector3.zero;
    private float wallRunEndTime;
    private float nextWallRunReadyTime;

    private float wallRunEntryTime = -999f;

    Rigidbody RB => motor.RB;

    public void TickFixed()
    {
        HandleWallRunState();
        if (isWallRunning)
        {
            WallRunMove();
            ApplyWallRunVertical();
            UpdateOrientation();
            AlignModelToWall();
        }
    }

    public void ForceStopAndCooldown()
    {
        StopWallRun(motor.IsGrounded);
    }

    private void HandleWallRunState()
    {
        if (!wallRunEnabled)
        {
            StopWallRun(motor.IsGrounded);
            return;
        }

        // don’t start while grounded, cooling down, or dashing
        if (motor.IsGrounded || Time.time < nextWallRunReadyTime)
        {
            StopWallRun(motor.IsGrounded);
            return;
        }

        if (dash != null && (dash.IsDashing || dash.IsSideDashing))
        {
            StopWallRun(false);
            return;
        }

        float v = Input.GetAxis("Vertical");

        Vector3 up = Vector3.up;
        Vector3 forwardFlat = Vector3.ProjectOnPlane(transform.forward, up).normalized;
        Vector3 planarVel = Vector3.ProjectOnPlane(RB.linearVelocity, up);

        Vector3 wishDir = forwardFlat * Mathf.Max(0f, v);
        if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();

        // START
        if (!isWallRunning)
        {
            if (TryGetWall(out Vector3 n))
            {
                Vector3 t = Vector3.Cross(Vector3.up, n).normalized;
                if (Vector3.Dot(t, wishDir) < 0f) t = -t;

                bool hasIntent =
                    (wishDir.sqrMagnitude > 0f && Vector3.Dot(t, wishDir) > wallRunMinForwardDot)
                    || (planarVel.sqrMagnitude > 0.0001f && Vector3.Dot(t, planarVel.normalized) > wallRunMinForwardDot);

                if (hasIntent && HighEnoughForWallRun())
                {
                    isWallRunning = true;
                    wallRunNormal = n;
                    wallRunTangent = t;
                    wallRunEndTime = Time.time + wallRunDuration;
                    wallRunEntryTime = Time.time;
                }
                else
                {
                    StopWallRun(motor.IsGrounded);
                }
            }
            else
            {
                StopWallRun(motor.IsGrounded);
            }
        }

        if (isWallRunning)
        {
            if (!TryGetWall(out Vector3 n2))
            {
                StopWallRun(motor.IsGrounded);
                return;
            }

            wallRunNormal = n2;
            wallRunTangent = Vector3.Cross(Vector3.up, wallRunNormal).normalized;
            if (Vector3.Dot(wallRunTangent, wishDir) < 0f)
                wallRunTangent = -wallRunTangent;

            bool hasIntent =
                (wishDir.sqrMagnitude > 0f && Vector3.Dot(wallRunTangent, wishDir) > wallRunMinForwardDot)
                || (planarVel.sqrMagnitude > 0.0001f && Vector3.Dot(wallRunTangent, planarVel.normalized) > wallRunMinForwardDot);

            if (!hasIntent || !HighEnoughForWallRun() || Time.time > wallRunEndTime)
                StopWallRun(motor.IsGrounded);
        }
    }

    private void StopWallRun(bool grounded)
    {
        if (!isWallRunning) return;

        isWallRunning = false;
        wallRunNormal = Vector3.zero;
        wallRunTangent = Vector3.zero;
        nextWallRunReadyTime = Time.time + wallRunCooldown;
    }

    private void WallRunMove()
    {
        float v = Mathf.Max(0f, Input.GetAxis("Vertical"));

        Vector3 up = Vector3.up;
        Vector3 t = Vector3.ProjectOnPlane(wallRunTangent, up).normalized;
        Vector3 n = wallRunNormal.normalized;

        float baseSpeed = Mathf.Max(motor.currentMoveSpeed, 0.01f);
        float targetAlong = baseSpeed * wallRunSpeedMultiplier * v;

        if (Time.time - wallRunEntryTime <= 0.10f && v > 0f)
            targetAlong += wallRunEntryBoost;

        Vector3 planarVel = Vector3.ProjectOnPlane(RB.linearVelocity, up);

        float currentAlong = Vector3.Dot(planarVel, t);
        float deltaAlong = targetAlong - currentAlong;

        float maxDelta = wallRunTangentAccel * Time.fixedDeltaTime;
        deltaAlong = Mathf.Clamp(deltaAlong, -maxDelta, maxDelta);

        RB.AddForce(t * deltaAlong, ForceMode.VelocityChange);

        // stick to wall
        RB.AddForce(-n * wallRunStick, ForceMode.Acceleration);
    }

    private void ApplyWallRunVertical()
    {
        Vector3 vel = RB.linearVelocity;

        float elapsed = Time.time - wallRunEntryTime;

        if (elapsed <= wallRunRiseDuration)
        {
            vel.y = Mathf.Max(vel.y, wallRunRiseSpeed);
        }
        else
        {
            vel.y += Physics.gravity.y * wallRunGravityScale * Time.fixedDeltaTime;

            if (vel.y < wallRunSlowFallMaxDownSpeed)
                vel.y = wallRunSlowFallMaxDownSpeed;
        }

        if (vel.y > wallRunMaxUpSpeed)
            vel.y = wallRunMaxUpSpeed;

        RB.linearVelocity = vel;
    }

    private void UpdateOrientation()
    {
        Vector3 up = Vector3.up;
        Vector3 desiredForward = Vector3.ProjectOnPlane(wallRunTangent, up).normalized;
        if (desiredForward.sqrMagnitude < 0.0001f) return;

        Quaternion currentRot = transform.rotation;
        Quaternion targetRot = Quaternion.LookRotation(desiredForward, up);

        transform.rotation = Quaternion.Slerp(currentRot, targetRot, Time.deltaTime * wallRunFaceTurnLerp);
    }

    private void AlignModelToWall()
    {
        if (cartModel == null) return;
        if (wallRunNormal == Vector3.zero) return;

        Vector3 upDir = wallRunNormal;
        Vector3 forwardDir = wallRunTangent.sqrMagnitude > 0.001f
            ? wallRunTangent
            : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

        Quaternion targetRot = Quaternion.LookRotation(forwardDir, upDir);

        cartModel.rotation = Quaternion.Slerp(cartModel.rotation, targetRot, Time.deltaTime * motor.groundAlignSpeed);
    }

    private bool HighEnoughForWallRun()
    {
        Vector3 origin = transform.position + Vector3.up;
        float downDist = wallRunMinHeight + 1.0f;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, downDist, ~0, QueryTriggerInteraction.Ignore))
            return hit.distance > wallRunMinHeight;

        return true;
    }

    private bool TryGetWall(out Vector3 wallNormal)
    {
        wallNormal = Vector3.zero;

        Vector3 origin = transform.position + Vector3.up * 0.5f;
        float radius = wallCheckRadius;
        float dist = wallCheckDistance;

        Vector3[] dirs =
        {
            transform.right,
            -transform.right,
            transform.forward,
            -transform.forward
        };

        foreach (Vector3 dir in dirs)
        {
            if (Physics.SphereCast(origin, radius * 0.5f, dir, out RaycastHit hit, dist, wallLayers, QueryTriggerInteraction.Ignore))
            {
                if (Vector3.Dot(hit.normal, Vector3.up) < 0.5f)
                {
                    wallNormal = hit.normal;
                    return true;
                }
            }
        }

        return false;
    }
}
