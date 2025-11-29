using UnityEngine;

// Rigidbody-based cart controller + wall run / wall jump state machine
[RequireComponent(typeof(Rigidbody))]
public class GPlayerCont2 : MonoBehaviour
{
    [Header("Movement Settings")]
    public float currentMoveSpeed = 1f;
    public float startMoveSpeed = 5f;
    public float maxMoveSpeed = 15f;
    public float acceleration = 20f;
    public float deceleration = 25f;

    private float currentReverseSpeed = 0f;
    public float manualDeceleration = 30f;
    public float backwardMaxMoveSpeed = 10f;
    public float backwardAcceleration = 15f;
    public float backwardDeceleration = 20f;

    [Header("Custom Gravity Settings")]
    public float fallMultiplier = 2f;
    public float lowJumpMultiplier = 1.5f;
    public float maxFallSpeed = -20f;

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

    [Header("Wall Surface")]
    public LayerMask wallLayers = ~0;
    public float wallCheckRadius = 0.45f;
    public float wallCheckDistance = 0.6f;

    [Header("Wall Jump")]
    public float wallJumpUpImpulse = 6.5f;
    public float wallJumpAwayImpulse = 7.5f;
    public float wallStickMaxSpeed = 3.5f;
    public float wallJumpCooldown = 0.15f;

    [Header("Wall Run")]
    public bool wallRunEnabled = true;
    public float wallRunSpeed = 10f;
    public float wallRunDuration = 1.4f;
    public float wallRunGravityScale = 0.2f;
    public float wallRunMinHeight = 1.1f;
    public float wallRunMinForwardDot = 0.2f;
    public float wallRunCooldown = 0.35f;
    public float wallRunStick = 2.0f;

    [Header("Wall Run Facing / Lean")]
    public float wallRunFaceTurnLerp = 12f;
    public float wallRunLeanDegrees = 15f;

    [Header("Dash")]
    public KeyCode dashKey = KeyCode.LeftShift;
    public float dashSpeed = 20f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1.0f;
    public float dashSpeedBoostMultiplier = 1.4f;
    public float dashBoostDuration = 1.0f;

    [Header("Dash Jump-Style Feel")]
    public float dashHopVerticalSpeed = 4f;
    public float dashInitialBurstMultiplier = 1.5f;

    [Header("Jump Timing")]
    public float coyoteTime = 0.12f;
    public float jumpBuffer = 0.12f;

    public enum CharState { Grounded, Airborne, WallRunning }
    public CharState CurrentState => state;
    private CharState state = CharState.Grounded;

    private Rigidbody rb;
    private bool isGrounded;
    private Vector3 lastGroundNormal = Vector3.up;

    // wall run state
    private bool isWallRunning = false;
    private Vector3 wallRunNormal = Vector3.zero;
    private Vector3 wallRunTangent = Vector3.zero;
    private float wallRunEndTime;
    private float nextWallRunReadyTime;
    private float wallJumpLockUntil;

    // Dash state
    private bool isDashing = false;
    private bool dashJustStarted = false;
    private Vector3 dashDirection = Vector3.zero;
    private float dashEndTime = 0f;
    private float dashBoostEndTime = 0f;
    private float nextDashAllowedTime = 0f;

    private float lastGroundedTime;
    private float lastJumpPressedTime;

    private void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // ground check each frame, track time
        isGrounded = CheckGrounded();
        if (isGrounded)
            lastGroundedTime = Time.time;

        // buffer jump press for FixedUpdate
        if (Input.GetButtonDown("Jump"))
            lastJumpPressedTime = Time.time;

        // handle dash input (direction & key)
        HandleDashInput();
    }

    private void FixedUpdate()
    {
        HandleDashMovement(); // dash physics

        HandleWallRun();      // state machine: start/stop wall run
        HandleJump();         // uses state + jump buffer / coyote

        Move();               // momentum logic (+ wall-run branch)
        ApplyCustomGravity(); // gravity with special wall-run behaviour
        AlignModelToGroundAndTilt(); // ground OR wall alignment + tilt
        UpdateOrientation();  // character rotation during wall-run

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
        // When wall running, override movement with wall-run motion
        if (isWallRunning)
        {
            WallRunMove();
            return;
        }

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // steering axis
        Vector3 up = isGrounded ? lastGroundNormal : Vector3.up;

        // rotate by input
        float yawDelta = h * 120f * Time.fixedDeltaTime;
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
        // During the post-dash window, slightly higher max speed.
        if (Time.time < dashBoostEndTime)
            return maxMoveSpeed * dashSpeedBoostMultiplier;

        return maxMoveSpeed;
    }

    public void BlockForwardMovement() => currentMoveSpeed = 0f;
    public void BlockBackwardMovement() => currentReverseSpeed = 0f;


    private void HandleDashInput()
    {
        // don't start a dash while wall-running or on cooldown
        if (isWallRunning)
            return;

        if (Time.time < nextDashAllowedTime)
            return;

        // only allow dash when grounded (Sleeping Dogs style)
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

        Vector3 up = isGrounded ? lastGroundNormal : Vector3.up;

        // Convert local input (WASD) to world space, then project onto movement plane
        Vector3 worldDir = transform.TransformDirection(inputDir.normalized);
        dashDirection = Vector3.ProjectOnPlane(worldDir, up).normalized;

        if (dashDirection.sqrMagnitude < 0.01f)
            return;

        isDashing = true;
        dashJustStarted = true;
        dashEndTime = Time.time + dashDuration;
        dashBoostEndTime = dashEndTime + dashBoostDuration;
        nextDashAllowedTime = Time.time + dashCooldown;
    }

    private void HandleDashMovement()
    {
        if (!isDashing)
            return;

        // FIRST PHYSICS FRAME OF DASH: strong burst + small hop
        if (dashJustStarted)
        {
            dashJustStarted = false;

            // stronger horizontal burst
            float burstSpeed = dashSpeed * dashInitialBurstMultiplier;

            // combine planar dash with upward hop
            Vector3 newVel = dashDirection * burstSpeed;
            newVel.y = Mathf.Max(newVel.y, dashHopVerticalSpeed);

            rb.linearVelocity = newVel;
        }
        else
        {
            // DURING DASH WINDOW: keep planar speed close to dashSpeed
            Vector3 up = isGrounded ? lastGroundNormal : Vector3.up;

            // only adjust planar component, leave vertical to gravity/jump
            Vector3 planarVel = Vector3.ProjectOnPlane(rb.linearVelocity, up);
            Vector3 desiredVel = dashDirection * dashSpeed;

            Vector3 velDiff = desiredVel - planarVel;

            // use Acceleration so it's a push, not a teleport
            rb.AddForce(velDiff, ForceMode.Acceleration);
        }

        // end of dash window
        if (Time.time >= dashEndTime)
        {
            isDashing = false;
        }
    }

    // ---------------- Wall run state machine ----------------

    private void HandleWallRun()
    {
        if (!wallRunEnabled)
        {
            StopWallRun(isGrounded);
            return;
        }

        float v = Input.GetAxis("Vertical");

        // if grounded or cooldown, stop wall run
        if (isGrounded || Time.time < nextWallRunReadyTime)
        {
            StopWallRun(isGrounded);
            return;
        }

        Vector3 up = Vector3.up;
        Vector3 forwardFlat = Vector3.ProjectOnPlane(transform.forward, up).normalized;
        Vector3 planarVel = Vector3.ProjectOnPlane(rb.linearVelocity, up);

        // "intent" direction (forward input)
        Vector3 wishDir = forwardFlat * Mathf.Max(0f, v);
        if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();

        // find a suitable wall
        if (!TryGetWall(out Vector3 n))
        {
            StopWallRun(isGrounded);
            return;
        }

        // choose tangent along the wall (perpendicular to normal, mostly horizontal)
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

        if (isWallRunning && Time.time > wallRunEndTime)
        {
            StopWallRun(isGrounded);
        }

        // if not wallrunning, but not grounded -> airborne state
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
        // Move along wall tangent
        float v = Mathf.Max(0f, Input.GetAxis("Vertical"));

        Vector3 up = Vector3.up;
        Vector3 t = wallRunTangent;
        t = Vector3.ProjectOnPlane(t, up).normalized;

        Vector3 desiredVel = t * (wallRunSpeed * v);
        Vector3 planarVel = Vector3.ProjectOnPlane(rb.linearVelocity, up);

        Vector3 velChange = desiredVel - planarVel;
        rb.AddForce(velChange, ForceMode.VelocityChange);

        //  clamp vertical speed to avoid rocketing upwards/downwards on the wall
        Vector3 vel = rb.linearVelocity;
        if (vel.y > wallStickMaxSpeed)
            vel.y = wallStickMaxSpeed;
        rb.linearVelocity = vel;
    }

    private void HandleJump()
    {
        bool wantsJump = (Time.time - lastJumpPressedTime) <= jumpBuffer;
        if (!wantsJump)
            return;

        bool canCoyote = (Time.time - lastGroundedTime) <= coyoteTime;

        if (isWallRunning && Time.time >= wallJumpLockUntil)
        {
            // wall jump: away from wall + up
            StopWallRun(false);

            Vector3 vel = rb.linearVelocity;
            vel.y = 0f;
            rb.linearVelocity = vel;

            Vector3 jumpDir = wallRunNormal.normalized * wallJumpAwayImpulse
                              + Vector3.up * wallJumpUpImpulse;

            rb.AddForce(jumpDir, ForceMode.VelocityChange);

            wallJumpLockUntil = Time.time + wallJumpCooldown;
            lastJumpPressedTime = -999f;
        }
        else if (isGrounded || canCoyote)
        {
            Jump();
            lastJumpPressedTime = -999f;
        }
    }

    private void Jump()
    {
        state = CharState.Airborne;

        Vector3 vel = rb.linearVelocity;
        vel.y = 0f;
        rb.linearVelocity = vel;

        rb.AddForce(Vector3.up * 7f, ForceMode.VelocityChange);
    }

    private void ApplyCustomGravity()
    {
        // if wallrunning, apply reduced gravity
        if (isWallRunning)
        {
            rb.AddForce(Physics.gravity * wallRunGravityScale, ForceMode.Acceleration);
            return;
        }

        // normal gravity
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
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
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
                Time.deltaTime * 8f
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
                    Time.deltaTime * 8f
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

        // apply visual tilt
        cartModel.localRotation *= Quaternion.Lerp(
            Quaternion.identity,
            moveTilt,
            Time.deltaTime * tiltSpeed
        );
    }

    // -------------- Orientation update (for wall run lean) --------------
    private void UpdateOrientation()
    {
        if (!isWallRunning)
            return;

        Vector3 up = Vector3.up;
        Vector3 forwardFlat = Vector3.ProjectOnPlane(transform.forward, up).normalized;

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
        if (Physics.Raycast(feetTransform.position + Vector3.up * 0.1f, Vector3.down,
                out RaycastHit hit, 0.3f))
        {
            lastGroundNormal = hit.normal;
            return true;
        }
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
}
