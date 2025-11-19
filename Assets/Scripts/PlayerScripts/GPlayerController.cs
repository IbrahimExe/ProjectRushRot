using UnityEngine;

// Rigidbody-based cart controller + wall run / wall jump state machine
[RequireComponent(typeof(Rigidbody))]
public class GPlayerController : MonoBehaviour
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

    public float rotationSpeed = 100f;
    public float linearDrag = 5f;
    public float jumpForce = 5f;

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
    public float groundAlignSpeed = 8f;
    public float maxVisualRoll = 25f;

    // -------- Wall / state --------

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

    private float lastGroundedTime;
    private float lastJumpPressedTime = -999f;

    void Start()
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
    }

    private void FixedUpdate()
    {
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
        // When wall running, we override movement with wall-run motion
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
                maxMoveSpeed,
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

    public void BlockForwardMovement() => currentMoveSpeed = 0f;
    public void BlockBackwardMovement() => currentReverseSpeed = 0f;

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

        // ----- START conditions -----
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
            }
        }

        // ----- SUSTAIN / EXIT -----
        if (isWallRunning)
        {
            bool timeUp = Time.time >= wallRunEndTime;
            bool lostWall = !TryGetWall(out Vector3 n2) || Vector3.Dot(n2, wallRunNormal) < 0.7f;

            if (timeUp || lostWall)
            {
                StopWallRun(false);
            }
            else
            {
                // keep state
                wallRunNormal = n2;
                state = CharState.WallRunning;
            }
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
        // Move along wall tangent, similar to PlayerMotorAdvanced
        float v = Mathf.Max(0f, Input.GetAxis("Vertical"));

        Vector3 up = Vector3.up;
        Vector3 horizVel = Vector3.ProjectOnPlane(rb.linearVelocity, up);
        Vector3 vertical = Vector3.Project(rb.linearVelocity, up);

        float targetSpeed = wallRunSpeed;
        Vector3 tangential = Vector3.Project(horizVel, wallRunTangent);
        float add = targetSpeed - tangential.magnitude;

        if (add > 0f)
        {
            float step = Mathf.Min(acceleration * Time.deltaTime, add);
            tangential += wallRunTangent * step;
        }

        // small stick force into wall for stability
        Vector3 stick = -wallRunNormal * wallRunStick;
        horizVel = tangential + stick * Time.deltaTime;

        rb.linearVelocity = horizVel + vertical;

        // limit downward slide
        if (rb.linearVelocity.y < -wallStickMaxSpeed)
        {
            rb.linearVelocity = new Vector3(
                rb.linearVelocity.x,
                -wallStickMaxSpeed,
                rb.linearVelocity.z
            );
        }
    }

    // ---------------- Jumping / gravity ----------------

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
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

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

    private void ApplyCustomGravity()
    {
        // softer gravity when wall running
        if (isWallRunning)
        {
            rb.AddForce(Physics.gravity * wallRunGravityScale, ForceMode.Acceleration);

            if (rb.linearVelocity.y < -wallStickMaxSpeed)
            {
                rb.linearVelocity = new Vector3(
                    rb.linearVelocity.x,
                    -wallStickMaxSpeed,
                    rb.linearVelocity.z
                );
            }
            return;
        }

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
            // Flip: make cartModel.up follow the wall normal so wheels face the wall
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

        // apply visual tilt
        cartModel.localRotation *= Quaternion.Lerp(
            Quaternion.identity,
            moveTilt,
            Time.deltaTime * tiltSpeed
        );
    }

    // ---------------- Orientation during wall run ----------------

    private void UpdateOrientation()
    {
        if (state != CharState.WallRunning || wallRunTangent.sqrMagnitude < 0.001f)
            return;

        Vector3 faceDir = wallRunTangent;
        Quaternion face = Quaternion.LookRotation(faceDir, Vector3.up);

        float side = Mathf.Sign(Vector3.Dot(
            Vector3.Cross(Vector3.up, wallRunTangent),
            wallRunNormal
        ));

        float roll = wallRunLeanDegrees * side;
        Quaternion lean = Quaternion.AngleAxis(roll, faceDir);

        Quaternion target = lean * face;
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            target,
            wallRunFaceTurnLerp * Time.deltaTime
        );
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

        Vector3 origin = transform.position + Vector3.up;
        float radius = wallCheckRadius;
        float dist = wallCheckDistance;

        const int rays = 12;
        for (int i = 0; i < rays; i++)
        {
            float ang = (Mathf.PI * 2f / rays) * i;
            Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));

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
