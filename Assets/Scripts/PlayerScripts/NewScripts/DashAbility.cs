// DashAbility.cs
using UnityEngine;

public class DashAbility : MonoBehaviour
{
    [Header("Refs")]
    public PlayerControllerBase motor;
    public WallRunAbility wallRun; // block dashing during wallrun

    [Header("Dash Settings")]
    public KeyCode dashKey = KeyCode.LeftShift;

    [Tooltip("Sustained dash planar speed after the initial burst.")]
    public float dashSpeed = 30f;

    [Tooltip("Time the dash movement stays active (logic).")]
    public float dashDuration = 0.25f;

    public float dashCooldown = 1.0f;

    [Tooltip("Temporary max-speed multiplier applied to base movement after dash ends.")]
    public float dashSpeedBoostMultiplier = 1.4f;

    public float dashBoostDuration = 1.5f;

    [Tooltip("Initial planar burst multiplier applied on dash start.")]
    public float dashInitialBurstMultiplier = 1.5f;

    [Header("Side Dash Settings")]
    public float sideDashDistance = 20f;
    public float sideDashDuration = 0.5f;
    public float sideDashUpOffset = 28f;

    [Header("Dash Hit Window")]
    public float hitWindowAfterDash = 0.25f;
    private float hitWindowEndTime = 0f;


    [Header("Dash Flip / Roll Settings (Visual Only)")]
    public Transform cartModel;
    public float dashFlipAngle = 360f;
    public float dashFlipDuration = 0.4f;

    [Tooltip("Small hop height for forward/back dash start.")]
    public float dashHopHeight = 28f;

    public bool IsDashing => isDashing;
    public bool IsSideDashing => isSideDashing;
    public bool IsDashFlipping => isDashFlipping;

    private bool isDashing = false;
    private bool dashJustStarted = false;
    private Vector3 dashDirection = Vector3.zero;
    private float dashEndTime = 0f;
    private float dashBoostEndTime = 0f;
    private float nextDashAllowedTime = 0f;

    private enum DashType { None, Forward, Backward, Left, Right }
    private DashType currentDashType = DashType.None;

    private bool isSideDashing = false;
    private Vector3 sideDashDirectionWorld = Vector3.zero;
    private float sideDashElapsed = 0f;

    private bool isDashFlipping = false;
    private Vector3 dashFlipAxisLocal = Vector3.zero;
    private float dashFlipAngleRemaining = 0f;

    Rigidbody RB => motor.RB;

    public void TickUpdate()
    {
        HandleDashInput();
        ApplyDashFlipRoll();
    }

    public void TickFixed()
    {
        HandleDashMovement();
        HandleSideDashMovement();

        // avoid base movement fighting with dash
        motor.SuppressMoveInput = isDashing || isSideDashing;

        // prevent buffered jumps while dash is controlling motion/visual
        motor.SuppressJumpBuffer = isDashing || isSideDashing || isDashFlipping;

        // post-dash speed boost for base movement
        motor.MaxSpeedMultiplier = (Time.time < dashBoostEndTime) ? dashSpeedBoostMultiplier : 1f;
    }

    private void HandleDashInput()
    {
        if (motor == null) return;
        if (motor.IsGrounded == false) return;
        if (wallRun != null && wallRun.IsWallRunning) return;
        if (Time.time < nextDashAllowedTime) return;
        if (!Input.GetKeyDown(dashKey)) return;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 inputDir = new Vector3(h, 0f, v);
        if (inputDir.sqrMagnitude < 0.01f) return;

        currentDashType = DashType.None;

        bool w = v > 0.1f;
        bool s = v < -0.1f;
        bool d = h > 0.1f;
        bool a = h < -0.1f;

        // prioritize lateral if both pressed (feels snappier for dodges)
        if (a) currentDashType = DashType.Left;
        else if (d) currentDashType = DashType.Right;
        else if (s && !w) currentDashType = DashType.Backward;
        else if (w) currentDashType = DashType.Forward;

        if (currentDashType == DashType.None) return;

        // Lateral (side) dash: instantaneous velocity set + optional hop
        if (currentDashType == DashType.Left || currentDashType == DashType.Right)
        {
            Vector3 up = motor.IsGrounded ? motor.GroundNormal : Vector3.up;
            Vector3 rightFlat = Vector3.ProjectOnPlane(transform.right, up).normalized;
            float sideSign = currentDashType == DashType.Right ? 1f : -1f;

            sideDashDirectionWorld = rightFlat * sideSign;

            float lateralSpeed = (sideDashDuration > 0f) ? sideDashDistance / sideDashDuration : 0f;
            float upSpeed = GetJumpSpeedForHeight(sideDashUpOffset);

            Vector3 vel = RB.linearVelocity;

            // remove existing lateral component so the dodge is consistent
            Vector3 lateralCurrent = Vector3.Project(vel, sideDashDirectionWorld);
            vel -= lateralCurrent;

            vel += sideDashDirectionWorld * lateralSpeed;
            vel += up * upSpeed;

            RB.linearVelocity = vel;

            isSideDashing = true;
            sideDashElapsed = 0f;

            hitWindowEndTime = dashEndTime + hitWindowAfterDash; // for forward/back dash

            nextDashAllowedTime = Time.time + dashCooldown;

            StartDashFlipRoll(currentDashType);

            // ensure forward/back dash state is off
            isDashing = false;
            dashJustStarted = false;
            return;
        }

        // Forward/back dash: cached planar direction + burst, then maintained planar speed
        Vector3 upDash = motor.IsGrounded ? motor.GroundNormal : Vector3.up;

        Vector3 worldDir = transform.TransformDirection(inputDir.normalized);
        dashDirection = Vector3.ProjectOnPlane(worldDir, upDash).normalized;
        if (dashDirection.sqrMagnitude < 0.01f) return;

        isDashing = true;
        dashJustStarted = true;

        dashEndTime = Time.time + Mathf.Max(0.01f, dashDuration);
        dashBoostEndTime = dashEndTime + Mathf.Max(0f, dashBoostDuration);
        nextDashAllowedTime = Time.time + dashCooldown;

        StartDashFlipRoll(currentDashType);
    }

    private float GetJumpSpeedForHeight(float height)
    {
        if (height <= 0f) return 0f;
        float g = Physics.gravity.magnitude;
        return Mathf.Sqrt(2f * g * height);
    }

    private void HandleDashMovement()
    {
        if (!isDashing) return;

        Vector3 up = motor.IsGrounded ? motor.GroundNormal : Vector3.up;

        if (dashJustStarted)
        {
            dashJustStarted = false;

            float burstSpeed = dashSpeed * dashInitialBurstMultiplier;
            Vector3 planarDashDir = Vector3.ProjectOnPlane(dashDirection, up).normalized;

            Vector3 newVel = planarDashDir * burstSpeed;

            float upSpeed = GetJumpSpeedForHeight(dashHopHeight);
            newVel += up * upSpeed;

            RB.linearVelocity = newVel;
        }
        else
        {
            Vector3 planarVel = Vector3.ProjectOnPlane(RB.linearVelocity, up);
            Vector3 desiredVel = Vector3.ProjectOnPlane(dashDirection, up).normalized * dashSpeed;

            // Smoothly converge to desired planar speed without stomping vertical
            Vector3 velDiff = desiredVel - planarVel;
            RB.AddForce(velDiff, ForceMode.Acceleration);
        }

        if (Time.time >= dashEndTime)
            isDashing = false;
    }

    private void HandleSideDashMovement()
    {
        if (!isSideDashing) return;

        hitWindowEndTime = Time.time + sideDashDuration + hitWindowAfterDash;

        sideDashElapsed += Time.fixedDeltaTime;
        if (sideDashElapsed >= sideDashDuration)
            isSideDashing = false;
    }

    private void StartDashFlipRoll(DashType dashType)
    {
        isDashFlipping = false;
        dashFlipAxisLocal = Vector3.zero;
        dashFlipAngleRemaining = 0f;

        float sign = 1f;

        switch (dashType)
        {
            case DashType.Backward:
                dashFlipAxisLocal = Vector3.right;
                sign = -1f;
                break;
            case DashType.Forward:
                dashFlipAxisLocal = Vector3.right;
                sign = 1f;
                break;
            case DashType.Right:
                dashFlipAxisLocal = Vector3.forward;
                sign = -1f;
                break;
            case DashType.Left:
                dashFlipAxisLocal = Vector3.forward;
                sign = 1f;
                break;
            default:
                return;
        }

        dashFlipAngleRemaining = dashFlipAngle * sign;
        isDashFlipping = true;
    }

    private void ApplyDashFlipRoll()
    {
        if (!isDashFlipping || dashFlipAxisLocal == Vector3.zero || cartModel == null)
            return;

        float dt = Time.deltaTime;

        float totalAnglePerSecond = dashFlipAngle / Mathf.Max(0.01f, dashFlipDuration);
        float angleStep = totalAnglePerSecond * dt * Mathf.Sign(dashFlipAngleRemaining);

        if (Mathf.Abs(angleStep) > Mathf.Abs(dashFlipAngleRemaining))
            angleStep = dashFlipAngleRemaining;

        dashFlipAngleRemaining -= angleStep;

        cartModel.Rotate(dashFlipAxisLocal, angleStep, Space.Self);

        if (Mathf.Abs(dashFlipAngleRemaining) <= 0.01f)
        {
            isDashFlipping = false;
            dashFlipAngleRemaining = 0f;
        }
    }

    private bool CanDashKill()
    {
        return isDashing || isSideDashing || Time.time <= hitWindowEndTime;
    }

    private void TryKillEnemy(GameObject otherGO)
    {
        if (!CanDashKill()) return;
        // if (!(isDashing || isSideDashing)) return;
        if (!otherGO.CompareTag("Enemy")) return;

        Destroy(otherGO);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryKillEnemy(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryKillEnemy(collision.gameObject);
    }


    //private void OnTriggerEnter(Collider other)
    //{
    //    Debug.Log($"Dash collided with: {other.name} | isDashing: {isDashing}");

    //    if (isDashing && other.CompareTag("Enemy"))
    //        Destroy(other.gameObject);
    //}
}
