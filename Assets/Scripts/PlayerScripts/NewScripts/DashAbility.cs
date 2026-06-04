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
    // NOTE: public so PlayerControllerBase upgrade helpers can modify it at runtime.
    public float hitWindowAfterDash = 0.25f;
    private float hitWindowEndTime = 0f;

    [Tooltip("Fraction of the player's current speed that is removed when killing an enemy with a late dash.")]
    [Range(0f, 1f)]
    public float lateDashSpeedPenaltyFraction = 0.5f;

    // True while the active dash movement is running (isDashing or isSideDashing)
    public bool IsWithinDashWindow => isDashing || isSideDashing;

    // True during the grace window AFTER an active dash ends (hitWindowAfterDash). False once that window expires.
    public bool IsWithinHitWindow => !IsWithinDashWindow && Time.time <= hitWindowEndTime;

    [Header("Dash Kill -> XP Multiplier")]
    public int dashKillCap = 10;                 // cap at 10 kills
    public float secondsToKeepMultiplier = 10f;  // stays for 10s after last kill

    private int dashKillCount = 0;
    private float lastDashKillTime = -999f;

    // Read-only for UI
    public int DashKillCount => dashKillCount;
    public float DashKillTimeLeft => dashKillCount > 0
        ? Mathf.Max(0f, secondsToKeepMultiplier - (Time.time - lastDashKillTime))
        : 0f;

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

        // post-dash speed boost for base movement - smoothly decrease to 1.0
        if (Time.time < dashBoostEndTime)
        {
            float timeSinceDashEnd = Time.time - dashEndTime;
            float boostDuration = dashBoostEndTime - dashEndTime;
            float t = Mathf.Clamp01(timeSinceDashEnd / boostDuration);
            motor.MaxSpeedMultiplier = Mathf.Lerp(dashSpeedBoostMultiplier, 1f, t);
        }
        else
        {
            motor.MaxSpeedMultiplier = 1f;
        }

        UpdateDashKillMultiplier();
    }

    private void HandleDashInput()
    {
        if (motor == null) return;

        bool hasAirSideDash = motor.characterData != null && motor.characterData.canAirSideDash;

        // Block all dashes when grounded check fails, UNLESS the character has canAirSideDash
        // (in which case only side dashes are allowed in the air)
        if (!motor.IsGrounded && !hasAirSideDash) return;

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

        if (a) currentDashType = DashType.Left;
        else if (d) currentDashType = DashType.Right;
        else if (s && !w) currentDashType = DashType.Backward;
        else if (w) currentDashType = DashType.Forward;

        if (currentDashType == DashType.None) return;

        // If airborne with canAirSideDash, ONLY allow side dashes
        if (!motor.IsGrounded && hasAirSideDash)
        {
            if (currentDashType != DashType.Left && currentDashType != DashType.Right)
                return;
        }

        // Lateral (side) dash: instantaneous velocity set + optional hop
        if (currentDashType == DashType.Left || currentDashType == DashType.Right)
        {
            Vector3 up = motor.IsGrounded ? motor.GroundNormal : Vector3.up;
            Vector3 rightFlat = Vector3.ProjectOnPlane(transform.right, up).normalized;
            float sideSign = currentDashType == DashType.Right ? 1f : -1f;

            sideDashDirectionWorld = rightFlat * sideSign;

            float lateralSpeed = (sideDashDuration > 0f) ? sideDashDistance / sideDashDuration : 0f;

            Vector3 vel = RB.linearVelocity;
            Vector3 planarVel = Vector3.ProjectOnPlane(vel, up);
            Vector3 lateralCurrent = Vector3.Project(planarVel, sideDashDirectionWorld);

            vel -= lateralCurrent;
            vel += sideDashDirectionWorld * lateralSpeed;

            // Only apply the upward hop when grounded; skip it for air side dashes
            // so we don't mess up the player's existing air trajectory
            if (motor.IsGrounded)
            {
                float upSpeed = GetJumpSpeedForHeight(sideDashUpOffset);
                Vector3 verticalVel = Vector3.Project(vel, up);
                vel -= verticalVel;
                vel += up * upSpeed;
            }

            RB.linearVelocity = vel;

            isSideDashing = true;
            sideDashElapsed = 0f;

            hitWindowEndTime = dashEndTime + hitWindowAfterDash;

            nextDashAllowedTime = Time.time + dashCooldown;

            StartDashFlipRoll(currentDashType);

            isDashing = false;
            dashJustStarted = false;
            return;
        }

        Vector3 upDash = motor.IsGrounded ? motor.GroundNormal : Vector3.up;

        Vector3 worldDir = transform.TransformDirection(inputDir.normalized);
        dashDirection = Vector3.ProjectOnPlane(worldDir, upDash).normalized;
        if (dashDirection.sqrMagnitude < 0.01f) return;

        isDashing = true;
        dashJustStarted = true;

        dashEndTime = Time.time + Mathf.Max(0.01f, dashDuration);
        dashBoostEndTime = dashEndTime + Mathf.Max(0f, dashBoostDuration);
        nextDashAllowedTime = Time.time + dashCooldown;

        // Open the hit-window starting from when the dash ends
        hitWindowEndTime = dashEndTime + hitWindowAfterDash;

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

            Vector3 currentVel = RB.linearVelocity;
            Vector3 planarVel = Vector3.ProjectOnPlane(currentVel, up);
            Vector3 planarDashDir = Vector3.ProjectOnPlane(dashDirection, up).normalized;

            float currentSpeedInDashDir = Vector3.Dot(planarVel, planarDashDir);
            float burstSpeed = dashSpeed * dashInitialBurstMultiplier;

            float targetSpeed = Mathf.Max(burstSpeed, currentSpeedInDashDir + burstSpeed * 0.5f);
            Vector3 newPlanarVel = planarDashDir * targetSpeed;

            motor.MaxSpeedMultiplier = dashSpeedBoostMultiplier;

            Vector3 newVel = newPlanarVel;
            float upSpeed = GetJumpSpeedForHeight(dashHopHeight);
            newVel += up * upSpeed;

            RB.linearVelocity = newVel;
        }
        else
        {
            Vector3 planarVel = Vector3.ProjectOnPlane(RB.linearVelocity, up);
            Vector3 desiredVel = Vector3.ProjectOnPlane(dashDirection, up).normalized * dashSpeed;

            Vector3 velDiff = desiredVel - planarVel;
            RB.AddForce(velDiff, ForceMode.Acceleration);
        }

        if (Time.time >= dashEndTime)
            isDashing = false;
    }

    private void HandleSideDashMovement()
    {
        if (!isSideDashing) return;

        sideDashElapsed += Time.fixedDeltaTime;
        if (sideDashElapsed >= sideDashDuration)
        {
            isSideDashing = false;
            // Open the hit-window after side dash ends
            hitWindowEndTime = Time.time + hitWindowAfterDash;
        }
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

    /// <summary>Immediately cancels the visual dash flip roll. Call this when another ability takes over (e.g. wall run).</summary>
    public void CancelDashFlip()
    {
        isDashFlipping = false;
        dashFlipAngleRemaining = 0f;
        dashFlipAxisLocal = Vector3.zero;
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

    private void UpdateDashKillMultiplier()
    {
        if (dashKillCount > 0 && Time.time > lastDashKillTime + secondsToKeepMultiplier)
        {
            dashKillCount = 0;
            PushDashKillMultiplierToXP();
        }
    }

    private void PushDashKillMultiplierToXP()
    {
        float killMult = 2f;
        if (motor != null && motor.characterData != null)
        {
            killMult = motor.characterData.xpMultiplierOnKill;
        }

        // linear: based on xpMultiplierOnKill per kill
        float mult = (dashKillCount <= 0) ? 1f : Mathf.Min(killMult * dashKillCount, killMult * dashKillCap);

        if (AltExpManager.Instance != null)
            AltExpManager.Instance.SetDashKillMultiplier(mult);
    }

    private bool CanDashKill()
    {
        return isDashing || isSideDashing || Time.time <= hitWindowEndTime;
    }

    private void TryKillEnemy(GameObject otherGO)
    {
        if (!CanDashKill()) return;
        if (!otherGO.CompareTag("Enemy")) return;

        // Apply a speed penalty when the kill happens in the late hit-window
        bool isLateDashHit = IsWithinHitWindow;
        if (isLateDashHit && lateDashSpeedPenaltyFraction > 0f)
        {
            RB.linearVelocity *= (1f - lateDashSpeedPenaltyFraction);
        }

        Destroy(otherGO);

        if (dashKillCount < dashKillCap)
            dashKillCount++;

        lastDashKillTime = Time.time;

        PushDashKillMultiplierToXP();
    }

    private void OnTriggerEnter(Collider other)
    {
        // Skip colliders that are purely for proximity/sensing
        if (other.TryGetComponent<PresenceTriggerRelay>(out _) || other.TryGetComponent<PlayerDetector>(out _)) return;
        TryKillEnemy(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryKillEnemy(collision.gameObject);
        
    }
}