using UnityEngine;

// Physics-based player controller with wall run + dash + XP/upgrade integration
[RequireComponent(typeof(Rigidbody))]
public class PlayerController2 : MonoBehaviour
{
    [Header("Character")]
    public PlayerCharacterData characterData;
    private GameObject currentModel;


    [Header("Movement Settings")]
    // Base stats (used by upgrades)
    public float baseStartMoveSpeed = 5f;
    public float baseMaxMoveSpeed = 50f;
    public float baseAcceleration = 25f;
    public float baseDeceleration = 25f;

    // Runtime modified stats (these are what actually drive movement)
    public float currentMoveSpeed;
    private float startMoveSpeed;
    private float maxMoveSpeed;
    private float acceleration;
    private float deceleration;

    private float currentReverseSpeed = 0f;

    public float manualDeceleration = 30f;
    public float backwardMaxMoveSpeed = 10f;
    public float backwardAcceleration = 15f;
    public float backwardDeceleration = 20f;

    public float rotationSpeed = 75f;
    public float linearDrag = 5f;

    [Header("Jump and Gravity Settings")]
    public float baseJumpForce = 125f;
    private float jumpForce;

    public float fallMultiplier = 3f;
    public float lowJumpMultiplier = 1f;
    public float maxFallSpeed = -150f;

    [Header("Ground Check Settings")]
    public Transform feetTransform;

    [Header("Visual Tilt Settings")]
    public Transform cartModel;
    public Transform rayOrigin;
    public float rayLength = 1.5f;
    public float tiltForwardAmount = 10f;
    public float tiltSideAmount = 10f;
    public float tiltSpeed = 5f;
    public float airTiltAmount = 5f;
    public float groundAlignSpeed = 8f;
    public float maxVisualRoll = 25f;

    // -------- Wall / state --------

    [Header("Wall Surface")]
    public LayerMask wallLayers = ~0;
    public float wallCheckRadius = 1f;
    public float wallCheckDistance = 2f;

    [Header("Wall Jump")]
    public float baseJumpUpImpulse = 7.5f;
    public float baseJumpAwayImpulse = 8.5f;

    private float wallJumpUpImpulse;
    private float wallJumpAwayImpulse;
    public float wallStickMaxSpeed = 3.5f;
    public float wallJumpCooldown = 0.15f;

    [Header("Wall Run")]
    public bool wallRunEnabled = true;

    public float baseWallRunSpeed = 75f;
    public float baseWallRunDuration = 4f;

    private float wallRunSpeed;
    private float wallRunDuration;
    public float wallRunGravityScale = 0.2f;
    public float wallRunMinHeight = 1.1f;
    public float wallRunMinForwardDot = 0.2f;
    public float wallRunCooldown = 1f;
    public float wallRunStick = 0.5f;

    [Header("Wall Run Facing / Lean")]
    public float wallRunFaceTurnLerp = 12f;
    public float wallRunLeanDegrees = 15f;

    [Header("Jump Timing")]
    public float coyoteTime = 0.12f;
    public float jumpBuffer = 0.12f;

    [Header("Dash Settings")]
    public KeyCode dashKey = KeyCode.LeftShift;
    public float dashSpeed = 30f;
    public float dashCooldown = 1.0f;
    public float dashSpeedBoostMultiplier = 1.4f;
    public float dashBoostDuration = 1.5f;
    // How strong the first frame burst is
    public float dashInitialBurstMultiplier = 1.5f;

    [Header("Side Dash Settings")]
    [Tooltip("How far the cart moves sideways when side-dashing (in meters).")]
    public float sideDashDistance = 20f;
    [Tooltip("How long the side hop lasts (seconds).")]
    public float sideDashDuration = 0.5f;
    [Tooltip("Max height of the side hop arc (in meters).")]
    public float sideDashUpOffset = 28f;

    [Header("Dash Flip / Roll Settings")]
    public float dashFlipAngle = 360f;
    // Also used as the forward/back dash duration
    public float dashFlipDuration = 0.4f;
    [Tooltip("Max height of the forward/back dash hop (in meters).")]
    public float dashHopHeight = 28f;

    // Core state

    public enum CharState { Grounded, Airborne, WallRunning }
    public CharState CurrentState => state;
    private CharState state = CharState.Grounded;

    private Rigidbody rb;
    private bool isGrounded;
    private Vector3 lastGroundNormal = Vector3.up;

    // Dash flip / roll state

    private bool isDashFlipping = false;
    private Vector3 dashFlipAxisLocal = Vector3.zero;
    private float dashFlipAngleRemaining = 0f;

    // forward/back dash state
    private bool isDashing = false;
    private bool dashJustStarted = false;
    private Vector3 dashDirection = Vector3.zero;
    private float dashEndTime = 0f;
    private float dashBoostEndTime = 0f;
    private float nextDashAllowedTime = 0f;
    private enum DashType { None, Forward, Backward, Left, Right }
    private DashType currentDashType = DashType.None;

    // wall run state
    private bool isWallRunning = false;
    private Vector3 wallRunNormal = Vector3.zero;
    private Vector3 wallRunTangent = Vector3.zero;
    private float wallRunEndTime;
    private float nextWallRunReadyTime;
    private float wallJumpLockUntil;

    // forward/back dash hop state
    private float dashElapsed = 0f;
    private float dashLastHeight = 0f;
    private Vector3 dashUp = Vector3.up;

    // smooth side dash state
    private bool isSideDashing = false;
    private Vector3 sideDashDirectionWorld = Vector3.zero;
    private float sideDashElapsed = 0f;
    private Vector3 sideDashStartPosition;

    private float sideDashLastNorm = 0f;
    private float sideDashLastHeight = 0f;

    private float lastGroundedTime;
    private float lastJumpPressedTime = -999f;


    void Start()
    {
        Cursor.visible = false; // Hides the cursor
        Cursor.lockState = CursorLockMode.Locked; // Locks it to the center
        rb = GetComponent<Rigidbody>();

        if (characterData != null)
        {
            ApplyCharacter(characterData);
        }
        else
        {
            SetBaseStats();
        }
    }

    public void ApplyCharacter(PlayerCharacterData data)
    {
        characterData = data;

        // ---- STATS ----
        baseStartMoveSpeed = data.startMoveSpeed;
        baseMaxMoveSpeed = data.maxMoveSpeed;
        baseAcceleration = data.acceleration;
        baseDeceleration = data.deceleration;

        baseJumpForce = data.jumpForce;

        baseJumpUpImpulse = data.wallJumpUpImpulse;
        baseJumpAwayImpulse = data.wallJumpAwayImpulse;

        baseWallRunSpeed = data.wallRunSpeed;
        baseWallRunDuration = data.wallRunDuration;

        // Re-apply to runtime values
        SetBaseStats();

        // ---- MODEL ----
        if (currentModel != null)
            Destroy(currentModel);

        currentModel = Instantiate(
            data.modelPrefab,
            cartModel.parent
        );

        cartModel = currentModel.transform;
    }
    public void SetBaseStats()
    {
        startMoveSpeed = baseStartMoveSpeed;
        maxMoveSpeed = baseMaxMoveSpeed;
        acceleration = baseAcceleration;
        deceleration = baseDeceleration;

        jumpForce = baseJumpForce;

        wallJumpUpImpulse = baseJumpUpImpulse;
        wallJumpAwayImpulse = baseJumpAwayImpulse;
        wallRunSpeed = baseWallRunSpeed;
        wallRunDuration = baseWallRunDuration;
    }

    void Update()
    {
        // Ground check each frame
        isGrounded = CheckGrounded();
        if (isGrounded)
            lastGroundedTime = Time.time;

        // Buffer jump press for FixedUpdate (coyote time + jump buffer),
        // NOT while dashing / side dashing / dash flip.
        if (!isDashing && !isSideDashing && !isDashFlipping)
        {
            if (Input.GetButtonDown("Jump"))
                lastJumpPressedTime = Time.time;
        }

        // Dash input (direction and key)
        HandleDashInput();

        // UI speed update for XP system & Bar (uses currentMoveSpeed / maxMoveSpeed)
        float normalized = maxMoveSpeed > 0f ? currentMoveSpeed / maxMoveSpeed : 0f;
        if (ExperienceManager.Instance != null)
            ExperienceManager.Instance.SetSpeedMultiplier(1f + normalized * 0.20f); // +20% XP gain
    }


    private void FixedUpdate()
    {
        // Dash physics first (can modify velocity used by other systems)
        HandleDashMovement();      // forward/back dashes
        HandleSideDashMovement();  // smooth side hops

        HandleWallRun();           // state machine: start/stop wall run
        HandleJump();              // uses state + jump buffer / coyote
        Move();                    // momentum logic (+ wall-run branch)
        ApplyCustomGravity();      // gravity with special wall-run behaviour
        AlignModelToGroundAndTilt(); // ground OR wall alignment + tilt
        UpdateOrientation();       // character rotation during wall-run

        // keep rigidbody upright (no tipping) when airborne
        if (!isGrounded)
        {
           
            Quaternion rot = rb.rotation;
            rot.eulerAngles = new Vector3(0f, rot.eulerAngles.y, 0f);
            rb.MoveRotation(rot);
        }
    }

    // ---------------- Movement ----------------

    private void Move()
    {
        if (Time.time < wallJumpLockUntil && !isWallRunning)
            return;

        // When wall running, we override movement with wall-run motion
        if (isWallRunning)
        {
            WallRunMove();
            return;
        }

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        if (isDashing)
        {
            // during dash, no extra accel/decel from input
            h = 0f;
            v = 0f;
        }

        // steering axis
        Vector3 up = isGrounded ? lastGroundNormal : Vector3.up;

        // rotate by input
        float yawDelta = h * rotationSpeed * Time.fixedDeltaTime;
        transform.rotation = Quaternion.AngleAxis(yawDelta, up) * transform.rotation;

        // planar basis
        Vector3 forwardFlat = Vector3.ProjectOnPlane(transform.forward, up).normalized;
        Vector3 planarVel = Vector3.ProjectOnPlane(rb.linearVelocity, up);
        float planarSpeed = planarVel.magnitude;

        // direction of current motion
        float motionSign = (planarVel.sqrMagnitude > 0.001f)
            ? Mathf.Sign(Vector3.Dot(planarVel, forwardFlat))
            : 0f;

        // keep speed floors
        if (motionSign >= 0f && currentMoveSpeed < planarSpeed)
            currentMoveSpeed = planarSpeed;

        if (motionSign < 0f && currentReverseSpeed < planarSpeed)
            currentReverseSpeed = planarSpeed;

        bool pressingForward = v > 0.01f;
        bool pressingBackward = v < -0.01f;

        if (pressingForward)
        {
            if (currentMoveSpeed < startMoveSpeed)
                currentMoveSpeed = startMoveSpeed;

            currentMoveSpeed = Mathf.MoveTowards(
                currentMoveSpeed,
                GetCurrentMaxForwardSpeed(),
                acceleration * Time.deltaTime
            );
            currentReverseSpeed = 0f;
        }
        else if (pressingBackward)
        {
            if (currentMoveSpeed > 0.001f)
            {
                currentMoveSpeed = Mathf.MoveTowards(
                    currentMoveSpeed,
                    0f,
                    manualDeceleration * Time.deltaTime
                );
                currentReverseSpeed = 0f;
            }
            else
            {
                currentReverseSpeed = Mathf.MoveTowards(
                    currentReverseSpeed,
                    backwardMaxMoveSpeed,
                    backwardAcceleration * Time.deltaTime
                );
                currentMoveSpeed = 0f;
            }
        }
        else
        {
            currentMoveSpeed = Mathf.MoveTowards(
                currentMoveSpeed,
                0f,
                deceleration * Time.deltaTime
            );
            currentReverseSpeed = Mathf.MoveTowards(
                currentReverseSpeed,
                0f,
                backwardDeceleration * Time.deltaTime
            );
        }

        Vector3 targetVelocity;
        if (currentMoveSpeed > 0.001f)
            targetVelocity = forwardFlat * currentMoveSpeed;
        else if (currentReverseSpeed > 0.001f)
            targetVelocity = -forwardFlat * currentReverseSpeed;
        else
            targetVelocity = Vector3.zero;

        Vector3 velocityChange = targetVelocity - planarVel;
        rb.AddForce(velocityChange, ForceMode.VelocityChange);
    }

    private float GetCurrentMaxForwardSpeed()
    {
        // During the post-dash window, allow a slightly higher max speed.
        if (Time.time < dashBoostEndTime)
            return maxMoveSpeed * dashSpeedBoostMultiplier;

        return maxMoveSpeed;
    }

    public void BlockForwardMovement() => currentMoveSpeed = 0f;
    public void BlockBackwardMovement() => currentReverseSpeed = 0f;

    // ---------------- Dash Input ----------------

    private void HandleDashInput()
    {
        // do not start a dash while wall-running
        if (isWallRunning)
            return;

        // cooldown
        if (Time.time < nextDashAllowedTime)
            return;

        // only allow dash when grounded
        if (!isGrounded)
            return;

        if (!Input.GetKeyDown(dashKey))
            return;

        // use WASD input to define dash direction in local space
        float h = Input.GetAxisRaw("Horizontal"); // A/D
        float v = Input.GetAxisRaw("Vertical");   // W/S

        Vector3 inputDir = new Vector3(h, 0f, v);
        if (inputDir.sqrMagnitude < 0.01f)
            return; // no direction pressed -> no dash

        // Decide dash *type* based on axes
        currentDashType = DashType.None;

        bool w = v > 0.1f;   // W
        bool s = v < -0.1f;  // S
        bool d = h > 0.1f;   // D
        bool a = h < -0.1f;  // A

        // FIRST PRIORITY : LEFT OR RIGHT
        if (a)
            currentDashType = DashType.Left;
        else if (d)
            currentDashType = DashType.Right;
        // SECOND PRIORITY : BACKWARD (only if S is not mixed with W)
        else if (s && !w)
            currentDashType = DashType.Backward;
        // LAST PRIORITY : FORWARD (only if no A/D/S)
        else if (w)
            currentDashType = DashType.Forward;

        if (currentDashType == DashType.None)
            return;

        // === SIDE DASH CASE (physics-based side hop) ===
        if (currentDashType == DashType.Left || currentDashType == DashType.Right)
        {
            Vector3 up = isGrounded ? lastGroundNormal : Vector3.up;
            Vector3 rightFlat = Vector3.ProjectOnPlane(transform.right, up).normalized;
            float sideSign = currentDashType == DashType.Right ? 1f : -1f;

            sideDashDirectionWorld = rightFlat * sideSign;

            // how fast we need to move sideways to roughly cover sideDashDistance in sideDashDuration
            float lateralSpeed = (sideDashDuration > 0f)
                ? sideDashDistance / sideDashDuration
                : 0f;

            // vertical speed from desired side hop height
            float upSpeed = GetJumpSpeedForHeight(sideDashUpOffset);

            // start from current velocity, but replace sideways + vertical components
            Vector3 vel = rb.linearVelocity;

            // remove existing component along sideDashDirectionWorld so dash feels sharp
            Vector3 lateralCurrent = Vector3.Project(vel, sideDashDirectionWorld);
            vel -= lateralCurrent;

            // set new lateral + vertical components
            vel += sideDashDirectionWorld * lateralSpeed;
            vel += up * upSpeed;

            rb.linearVelocity = vel;

            isSideDashing = true;
            sideDashElapsed = 0f;

            nextDashAllowedTime = Time.time + dashCooldown;

            // flip/roll for side dash
            StartDashFlipRoll(currentDashType);

            // Do NOT start the normal forward/back dash here
            isDashing = false;
            dashJustStarted = false;
            return;
        }



        Vector3 upDash = isGrounded ? lastGroundNormal : Vector3.up;

        // Convert local input (W/S) to world space, then project onto movement plane
        Vector3 worldDir = transform.TransformDirection(inputDir.normalized);
        dashDirection = Vector3.ProjectOnPlane(worldDir, upDash).normalized;

        if (dashDirection.sqrMagnitude < 0.01f)
            return;

        // Start dash state
        isDashing = true;
        dashJustStarted = true;

        // forward/back dash uses dashFlipDuration as its length
        dashEndTime = Time.time + dashFlipDuration;
        dashBoostEndTime = dashEndTime + dashBoostDuration;
        nextDashAllowedTime = Time.time + dashCooldown;

        // init vertical hop arc
        dashElapsed = 0f;
        dashLastHeight = 0f;
        dashUp = isGrounded ? lastGroundNormal : Vector3.up;

        // Kick off flip/roll for this dash
        StartDashFlipRoll(currentDashType);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isDashing)
        {
            if (other.CompareTag("Enemy"))
            {
                Destroy(other.gameObject);
            }
        }
    }

    // ---------------- Forward/Back Dash Movement ----------------

    private float GetJumpSpeedForHeight(float height)
    {
        if (height <= 0f)
            return 0f;

        float g = Physics.gravity.magnitude;
        return Mathf.Sqrt(2f * g * height);
    }

    private void HandleDashMovement()
    {
        if (!isDashing)
            return;

        // choose up axis for this frame (slope-aware)
        Vector3 up = isGrounded ? lastGroundNormal : Vector3.up;

        // --- Horizontal dash behaviour ---
        if (dashJustStarted)
        {
            dashJustStarted = false;

            float burstSpeed = dashSpeed * dashInitialBurstMultiplier;

            // horizontal dash direction (planar only)
            Vector3 planarDashDir = Vector3.ProjectOnPlane(dashDirection, up).normalized;

            // base dash velocity
            Vector3 newVel = planarDashDir * burstSpeed;

            // vertical component from desired hop height (physics-based)
            float upSpeed = GetJumpSpeedForHeight(dashHopHeight);
            newVel += up * upSpeed;

            rb.linearVelocity = newVel;
        }
        else
        {
            // DURING DASH WINDOW: keep planar speed close to dashSpeed
            Vector3 planarVel = Vector3.ProjectOnPlane(rb.linearVelocity, up);
            Vector3 desiredVel = Vector3.ProjectOnPlane(dashDirection, up).normalized * dashSpeed;

            Vector3 velDiff = desiredVel - planarVel;
            rb.AddForce(velDiff, ForceMode.Acceleration);
        }

        // end of dash window
        if (Time.time >= dashEndTime)
        {
            isDashing = false;
        }
    }


    // ---------------- Smooth Side Dash Movement ----------------

    private void HandleSideDashMovement()
    {
        if (!isSideDashing)
            return;

        sideDashElapsed += Time.fixedDeltaTime;
        if (sideDashElapsed >= sideDashDuration)
        {
            isSideDashing = false;
        }
    }


    // ---------------- Dash Flip / Roll ----------------

    private void StartDashFlipRoll(DashType dashType)
    {
        isDashFlipping = false;
        dashFlipAxisLocal = Vector3.zero;
        dashFlipAngleRemaining = 0f;

        float sign = 1f;

        switch (dashType)
        {
            case DashType.Backward:
                // Front flip: rotate around local X, negative angle
                dashFlipAxisLocal = Vector3.right;
                sign = -1f;
                break;

            case DashType.Forward:
                // Back flip: rotate around local X, positive angle
                dashFlipAxisLocal = Vector3.right;
                sign = 1f;
                break;

            case DashType.Right:
                // Barrel roll right: rotate around local Z, negative angle
                dashFlipAxisLocal = Vector3.forward;
                sign = -1f;
                break;

            case DashType.Left:
                // Barrel roll left: rotate around local Z, positive angle
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
        if (!isDashFlipping || dashFlipAxisLocal == Vector3.zero)
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

    // -------------- Wall run state machine --------------

    private void HandleWallRun()
    {
        if (Time.time < wallJumpLockUntil)
        {
            isWallRunning = false;
            return;
        }

        if (!wallRunEnabled)
        {
            StopWallRun(isGrounded);
            return;
        }

        float v = Input.GetAxis("Vertical");

        if (isGrounded || Time.time < nextWallRunReadyTime)
        {
            StopWallRun(isGrounded);
            return;
        }

        Vector3 up = Vector3.up;
        Vector3 forwardFlat = Vector3.ProjectOnPlane(transform.forward, up).normalized;
        Vector3 planarVel = Vector3.ProjectOnPlane(rb.linearVelocity, up);

        Vector3 wishDir = forwardFlat * Mathf.Max(0f, v);
        if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();

        // START conditions
        if (!isWallRunning && Time.time >= wallJumpLockUntil)
        {
            if (TryGetWall(out Vector3 n))
            {
                Vector3 t = Vector3.Cross(Vector3.up, n).normalized;
                if (Vector3.Dot(t, wishDir) < 0f)
                    t = -t;

                bool hasIntent =
                    (wishDir.sqrMagnitude > 0f && Vector3.Dot(t, wishDir) > wallRunMinForwardDot)
                    || (planarVel.sqrMagnitude > 0.0001f &&
                        Vector3.Dot(t, planarVel.normalized) > wallRunMinForwardDot);

                if (hasIntent && HighEnoughForWallRun())
                {
                    isWallRunning = true;
                    wallRunNormal = n;
                    wallRunTangent = t;
                    wallRunEndTime = Time.time + wallRunDuration;
                    state = CharState.WallRunning;
                }
                else
                {
                    StopWallRun(isGrounded);
                }
            }
            else
            {
                StopWallRun(isGrounded);
            }
        }

        // MAINTAIN conditions
        if (isWallRunning)
        {
            if (!TryGetWall(out Vector3 n2))
            {
                StopWallRun(isGrounded);
                return;
            }

            wallRunNormal = n2;
            wallRunTangent = Vector3.Cross(Vector3.up, wallRunNormal).normalized;
            if (Vector3.Dot(wallRunTangent, wishDir) < 0f)
                wallRunTangent = -wallRunTangent;

            bool hasIntent =
                (wishDir.sqrMagnitude > 0f && Vector3.Dot(wallRunTangent, wishDir) > wallRunMinForwardDot)
                || (planarVel.sqrMagnitude > 0.0001f &&
                    Vector3.Dot(wallRunTangent, planarVel.normalized) > wallRunMinForwardDot);

            if (!hasIntent || !HighEnoughForWallRun() || Time.time > wallRunEndTime)
            {
                StopWallRun(isGrounded);
            }
        }

        if (!isWallRunning && !isGrounded)
            state = CharState.Airborne;
        else if (!isWallRunning && isGrounded)
            state = CharState.Grounded;
    }

    private void StopWallRun(bool grounded)
    {
        if (!isWallRunning)
        {
            state = grounded ? CharState.Grounded : CharState.Airborne;
            return;
        }

        isWallRunning = false;
        wallRunNormal = Vector3.zero;
        wallRunTangent = Vector3.zero;
        nextWallRunReadyTime = Time.time + wallRunCooldown;
        state = grounded ? CharState.Grounded : CharState.Airborne;
    }

    private void WallRunMove()
    {
        float v = Mathf.Max(0f, Input.GetAxis("Vertical"));

        Vector3 up = Vector3.up;
        Vector3 t = wallRunTangent;
        t = Vector3.ProjectOnPlane(t, up).normalized;

        Vector3 desiredVel = t * (wallRunSpeed * v);
        Vector3 planarVel = Vector3.ProjectOnPlane(rb.linearVelocity, up);

        Vector3 velChange = desiredVel - planarVel;
        rb.AddForce(velChange, ForceMode.VelocityChange);

        Vector3 vel = rb.linearVelocity;
        if (vel.y > wallStickMaxSpeed)
            vel.y = wallStickMaxSpeed;
        rb.linearVelocity = vel;
    }

    // ---------------- Jump ----------------

    private void HandleJump()
    {
        bool wantsJump = (Time.time - lastJumpPressedTime) <= jumpBuffer;
        if (!wantsJump)
            return;

        bool canCoyote = (Time.time - lastGroundedTime) <= coyoteTime;

        // WALL JUMP
        if (isWallRunning)
        {
            Vector3 cachedNormal = wallRunNormal.normalized;

            Vector3 jumpVelocity =
                cachedNormal * wallJumpAwayImpulse +
                Vector3.up * wallJumpUpImpulse;

            if (jumpVelocity.sqrMagnitude < 0.0001f)
                jumpVelocity = Vector3.up * wallJumpUpImpulse;

            rb.linearVelocity = jumpVelocity;

            isWallRunning = false;
            wallRunNormal = Vector3.zero;
            wallRunTangent = Vector3.zero;

            wallJumpLockUntil = Time.time + wallJumpCooldown;
            nextWallRunReadyTime = Time.time + wallRunCooldown;

            lastJumpPressedTime = -999f;
            state = CharState.Airborne;
            return;
        }
        // NORMAL JUMP (ground / coyote)
        else if (isGrounded || canCoyote)
        {
            Jump();
            lastJumpPressedTime = -999f;
        }
    }

    private void Jump()
    {
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    // ---------------- Gravity ----------------

    private void ApplyCustomGravity()
    {
        if (isWallRunning)
        {
            rb.AddForce(Physics.gravity * wallRunGravityScale, ForceMode.Acceleration);
            return;
        }

        rb.AddForce(Physics.gravity * fallMultiplier, ForceMode.Acceleration);

        if (rb.linearVelocity.y < 0)
        {
            rb.AddForce(Physics.gravity * (fallMultiplier - 1f), ForceMode.Acceleration);
        }
        else if (rb.linearVelocity.y > 0 && !Input.GetButton("Jump"))
        {
            rb.AddForce(Physics.gravity * (lowJumpMultiplier - 1f), ForceMode.Acceleration);
        }

        if (rb.linearVelocity.y < maxFallSpeed)
        {
            rb.linearVelocity = new Vector3(
                rb.linearVelocity.x,
                maxFallSpeed,
                rb.linearVelocity.z
            );
        }
    }


    // ---------------- Visual alignment (ground + wall) ----------------

    private void AlignModelToGroundAndTilt()
    {
        // Get movement input for tilt
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // While dashing / side dashing, don't add extra tilt from input
        // so the dash flip/roll is clean.
        if (isDashing || isSideDashing)
        {
            h = 0f;
            v = 0f;
        }

        bool isMoving = Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f;

        float targetForwardTilt = isMoving ? -v * tiltForwardAmount : 0f;
        float targetSideTilt = isMoving ? -h * tiltSideAmount : 0f;
        Quaternion moveTilt = Quaternion.Euler(targetForwardTilt, 0f, targetSideTilt);

        // WALL RUN: wheels pressed against the wall
        if (isWallRunning && wallRunNormal != Vector3.zero)
        {
            Vector3 upDir = wallRunNormal;
            Vector3 forwardDir = wallRunTangent.sqrMagnitude > 0.001f
                ? wallRunTangent
                : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

            Quaternion targetRot = Quaternion.LookRotation(forwardDir, upDir);

            cartModel.rotation = Quaternion.Slerp(
                cartModel.rotation,
                targetRot,
                Time.deltaTime * groundAlignSpeed
            );
        }
        else
        {
            // GROUND ALIGN
            if (Physics.Raycast(rayOrigin.position, Vector3.down, out RaycastHit hit, rayLength))
            {
                lastGroundNormal = hit.normal;

                Quaternion groundTilt =
                    Quaternion.FromToRotation(cartModel.up, lastGroundNormal) * cartModel.rotation;

                cartModel.rotation = Quaternion.Slerp(
                    cartModel.rotation,
                    groundTilt,
                    Time.deltaTime * groundAlignSpeed
                );

                cartModel.rotation = Quaternion.Euler(
                    cartModel.rotation.eulerAngles.x,
                    rb.rotation.eulerAngles.y,
                    cartModel.rotation.eulerAngles.z
                );
            }
            else
            {
                cartModel.rotation = Quaternion.Slerp(
                    cartModel.rotation,
                    rb.rotation,
                    Time.deltaTime * 2f
                );
            }
        }

        // Apply visual tilt relative to ground alignment
        cartModel.localRotation *= Quaternion.Lerp(
            Quaternion.identity,
            moveTilt,
            Time.deltaTime * tiltSpeed
        );

        // Dash flip/roll is applied *after* any ground align + tilt,
        // exactly like in your original PlayerController2.
        ApplyDashFlipRoll();
    }



    // -------------- Orientation update (for wall run lean) --------------
    private void UpdateOrientation()
    {
        if (!isWallRunning)
            return;

        Vector3 up = Vector3.up;
        Vector3 desiredForward = Vector3.ProjectOnPlane(wallRunTangent, up).normalized;
        if (desiredForward.sqrMagnitude < 0.0001f)
            return;

        Quaternion currentRot = transform.rotation;
        Quaternion targetRot = Quaternion.LookRotation(desiredForward, up);

        transform.rotation = Quaternion.Slerp(
            currentRot,
            targetRot,
            Time.deltaTime * wallRunFaceTurnLerp
        );
    }

    // ---------------- Ground Check ----------------

    private bool CheckGrounded()
    {
        float rayLen = 1.0f;
        RaycastHit hit;

        if (Physics.Raycast(feetTransform.position, Vector3.down, out hit, rayLen))
        {
            Debug.DrawLine(feetTransform.position, hit.point, Color.green);
            rb.linearDamping = linearDrag;
            lastGroundNormal = hit.normal;
            return true;
        }

        rb.linearDamping = 0f;
        return false;
    }

    private bool HighEnoughForWallRun()
    {
        Vector3 origin = transform.position + Vector3.up;
        float downDist = wallRunMinHeight + 1.0f;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, downDist, ~0,
                QueryTriggerInteraction.Ignore))
        {
            return hit.distance > wallRunMinHeight;
        }

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
            if (Physics.SphereCast(origin, radius * 0.5f, dir, out RaycastHit hit, dist,
                    wallLayers, QueryTriggerInteraction.Ignore))
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

    #region Set Variables (Upgrades)

    public void addMaxSpeed(float amount)
    {
        maxMoveSpeed += amount;
    }

    public void addAcceleration(float amount)
    {
        acceleration += amount;
    }

    public void addJumpForce(float amount)
    {
        jumpForce += amount;
    }

    public void addWallJumpAwayImpulse(float awayAmount)
    {
        wallJumpAwayImpulse += awayAmount;
    }
    public void addWallJumpUpImpulse(float upAmount)
    {
        wallJumpUpImpulse += upAmount;
    }

    public void addWallRunDuration(float amount)
    {
        wallRunDuration += amount;
    }
    public void addWallRunSpeed(float amount)
    {
        wallRunSpeed += amount;
    }

    #endregion
}
