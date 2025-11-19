using UnityEngine;

//attempt at a physics based player controller

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

    public float CameraRotationSpeed = 100f;
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
    public float groundAlignSpeed = 8f;

    private Rigidbody rb;
    private bool isGrounded;
    private Vector3 lastGroundNormal = Vector3.up;
    private float _turnSmoothVelocity;

    // Wall running
    [Header("Wall Run Settings")]
    public LayerMask wallLayers = ~0;
    public float wallCheckDistance = 1.0f;
    public float minWallUpDot = 0.2f;           // how vertical the surface must be
    public float wallRunSpeed = 10f;
    public float wallRunGravityMultiplier = 0.3f;
    public float wallStickForce = 20f;
    public float maxWallRunTime = 3f;
    public float wallJumpForce = 10f;
    public float wallJumpUpMultiplier = 1.2f;

    private bool isWallRunning = false;
    private Vector3 currentWallNormal = Vector3.zero;
    private Vector3 currentWallForward = Vector3.zero;
    private float wallRunTimer = 0f;

    void Start()
    {
        Cursor.visible = false; // Hides the cursor
        Cursor.lockState = CursorLockMode.Locked; // Locks cursor to the center of the screen
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        isGrounded = CheckGrounded();

        if (Input.GetButtonDown("Jump"))
        {
            if (isWallRunning)
                WallJump();
            else if (isGrounded)
                Jump();
        }
    }

    private void FixedUpdate()
    {
        HandleWallRun();
        Move();
        ApplyCustomGravity();
        AlignModelToGroundAndTilt();
    }

    private void Move()
    {
        if (isWallRunning)
        {
            WallRunMove();
            return;
        }

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // Use ground normal for proper slope handling
        Vector3 up = (lastGroundNormal == Vector3.zero) ? Vector3.up : lastGroundNormal;

        // Steer around ground up (decoupled from camera)
        float yawDelta = h * CameraRotationSpeed * Time.fixedDeltaTime;
        transform.rotation = Quaternion.AngleAxis(yawDelta, up) * transform.rotation;

        // Planar basis relative to ground
        Vector3 forwardFlat = Vector3.ProjectOnPlane(transform.forward, up).normalized;
        Vector3 planarVel = Vector3.ProjectOnPlane(rb.linearVelocity, up);
        float planarSpeed = planarVel.magnitude;

        // Determine current physical motion direction (1 = forward, -1 = backward, 0 = stopped)
        float motionSign = (planarVel.sqrMagnitude > 0.001f)
            ? Mathf.Sign(Vector3.Dot(planarVel, forwardFlat))
            : 0f;

        // Keep forward speed floor at actual forward motion (preserves momentum)
        if (motionSign >= 0f && currentMoveSpeed < planarSpeed)
            currentMoveSpeed = planarSpeed;

        // If currently moving backwards in physics and reverse speed is smaller, keep reverse speed floor
        if (motionSign < 0f && currentReverseSpeed < planarSpeed)
            currentReverseSpeed = planarSpeed;

        bool pressingForward = v > 0.01f;
        bool pressingBackward = v < -0.01f;

        // input logic
        if (pressingForward)
        {
            // If we are physically moving backward, treat 'W' as strong brake
            if (motionSign < 0f)
            {
                // Brake in reverse direction until we stop
                currentReverseSpeed = Mathf.MoveTowards(
                    currentReverseSpeed,
                    0f,
                    manualDeceleration * Time.deltaTime
                );

                // Stop forward speed while braking
                currentMoveSpeed = 0f;
            }
            else
            {
                // Start forward speed bump if needed
                if (currentMoveSpeed < startMoveSpeed)
                    currentMoveSpeed = startMoveSpeed;

                // Accelerate forward
                currentMoveSpeed = Mathf.MoveTowards(
                    currentMoveSpeed,
                    maxMoveSpeed,
                    acceleration * Time.deltaTime
                );

                // While accelerating forward, reset reverse speed
                currentReverseSpeed = 0f;
            }
        }
        else if (pressingBackward)
        {
            if (currentMoveSpeed > 0.001f)
            {
                // BRAKE: reduce forward speed only (no reverse force yet)
                currentMoveSpeed = Mathf.MoveTowards(
                    currentMoveSpeed,
                    0f,
                    manualDeceleration * Time.deltaTime
                );

                // ensure reverse speed is zero while braking
                currentReverseSpeed = 0f;
            }
            else
            {
                // Start reversing.
                currentReverseSpeed = Mathf.MoveTowards(
                    currentReverseSpeed,
                    backwardMaxMoveSpeed,
                    backwardAcceleration * Time.deltaTime
                );

                // make sure forward remains zero
                currentMoveSpeed = 0f;
            }
        }
        else
        {
            // natural slowdown when no input
            if (currentMoveSpeed > 0.001f)
            {
                currentMoveSpeed = Mathf.MoveTowards(
                    currentMoveSpeed,
                    0f,
                    deceleration * Time.deltaTime
                );
            }

            if (currentReverseSpeed > 0.001f)
            {
                currentReverseSpeed = Mathf.MoveTowards(
                    currentReverseSpeed,
                    0f,
                    backwardDeceleration * Time.deltaTime
                );
            }
        }

        // Build target velocity along forwardFlat using whichever "mode" is active
        Vector3 targetVelocity;
        if (currentMoveSpeed > 0.001f)
        {
            targetVelocity = forwardFlat * currentMoveSpeed; // forward
        }
        else if (currentReverseSpeed > 0.001f)
        {
            targetVelocity = -forwardFlat * currentReverseSpeed; // reverse
        }
        else
        {
            targetVelocity = Vector3.zero;
        }

        // force application
        Vector3 velocityChange = targetVelocity - planarVel;
        rb.AddForce(velocityChange, ForceMode.VelocityChange);
    }

    #region WallRunning

    private void HandleWallRun()
    {
        // Stop wall run if grounded
        if (isGrounded)
        {
            StopWallRun();
            return;
        }

        float v = Input.GetAxis("Vertical");

        // Need some forward input to start/keep wallrunning
        if (v <= 0.1f)
        {
            StopWallRun();
            return;
        }

        RaycastHit hit;
        Vector3 origin = feetTransform.position;
        bool foundWall = false;
        Vector3 wallNormal = Vector3.zero;

        // check left
        if (Physics.Raycast(origin, -transform.right, out hit, wallCheckDistance, wallLayers))
        {
            wallNormal = hit.normal;
            foundWall = true;
        }
        // check right
        else if (Physics.Raycast(origin, transform.right, out hit, wallCheckDistance, wallLayers))
        {
            wallNormal = hit.normal;
            foundWall = true;
        }

        if (foundWall && Mathf.Abs(Vector3.Dot(wallNormal, Vector3.up)) < minWallUpDot)
        {
            StartWallRun(wallNormal);
            wallRunTimer += Time.fixedDeltaTime;
            if (wallRunTimer > maxWallRunTime)
                StopWallRun();
        }
        else
        {
            StopWallRun();
        }
    }

    private void StartWallRun(Vector3 wallNormal)
    {
        isWallRunning = true;
        currentWallNormal = wallNormal.normalized;

        // direction along the wall
        Vector3 alongWall = Vector3.Cross(currentWallNormal, Vector3.up);
        if (alongWall.sqrMagnitude < 0.001f)
            alongWall = Vector3.Cross(currentWallNormal, transform.up);

        if (Vector3.Dot(alongWall, transform.forward) < 0f)
            alongWall = -alongWall;

        currentWallForward = alongWall.normalized;

        // Project velocity onto the wall and keep some speed
        Vector3 vel = rb.linearVelocity;
        Vector3 velAlongWall = Vector3.ProjectOnPlane(vel, currentWallNormal);

        if (velAlongWall.magnitude < wallRunSpeed)
            velAlongWall = velAlongWall.normalized * wallRunSpeed;

        rb.linearVelocity = new Vector3(
            velAlongWall.x,
            Mathf.Max(vel.y, 0f),
            velAlongWall.z
        );
    }

    private void StopWallRun()
    {
        if (!isWallRunning)
            return;

        isWallRunning = false;
        wallRunTimer = 0f;
        currentWallNormal = Vector3.zero;
        currentWallForward = Vector3.zero;
    }

    private void WallRunMove()
    {
        if (!isWallRunning)
            return;

        float v = Mathf.Max(0f, Input.GetAxis("Vertical"));

        Vector3 desiredAlongWall = currentWallForward * wallRunSpeed * v;
        Vector3 vel = rb.linearVelocity;
        Vector3 vertical = Vector3.Project(vel, Vector3.up);
        Vector3 horizontal = Vector3.ProjectOnPlane(vel, Vector3.up);

        horizontal = Vector3.Lerp(horizontal, desiredAlongWall, 0.1f);
        rb.linearVelocity = horizontal + vertical;

        // Stick a bit to the wall
        rb.AddForce(-currentWallNormal * wallStickForce, ForceMode.Acceleration);
    }

    private void WallJump()
    {
        if (!isWallRunning || currentWallNormal == Vector3.zero)
            return;

        // jump away from wall and upwards (Lucio-style)
        Vector3 jumpDir = (currentWallNormal + Vector3.up * wallJumpUpMultiplier).normalized;

        Vector3 vel = rb.linearVelocity;
        vel.y = 0f;
        rb.linearVelocity = vel;

        rb.AddForce(jumpDir * wallJumpForce, ForceMode.VelocityChange);

        StopWallRun();
    }

    #endregion

    private void Jump()
    {
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    private bool CheckGrounded()
    {
        float rayLength = 1.0f; // Adjust to match character height
        RaycastHit hit;

        // Cast a ray straight down from player position
        if (Physics.Raycast(feetTransform.transform.position, Vector3.down, out hit, rayLength))
        {
            Debug.DrawLine(transform.position, hit.point, Color.green);

            rb.linearDamping = linearDrag; // Apply drag when grounded
            return true;
        }

        rb.linearDamping = 0f; // No drag when in the air
        return false;
    }

    private void ApplyCustomGravity()
    {
        if (isWallRunning)
        {
            // Soften gravity while wall running (wallRunGravityMultiplier < 1 for lighter feel)
            rb.AddForce(Physics.gravity * (wallRunGravityMultiplier - 1f), ForceMode.Acceleration);

            if (rb.linearVelocity.y < maxFallSpeed)
            {
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, maxFallSpeed, rb.linearVelocity.z);
            }
            return;
        }

        // Only modify gravity when falling or going up
        if (rb.linearVelocity.y < 0)
        {
            // Falling – add extra gravity
            rb.AddForce(Physics.gravity * (fallMultiplier - 1f), ForceMode.Acceleration);
        }
        else if (rb.linearVelocity.y > 0 && !Input.GetButton("Jump"))
        {
            // If jump released early, apply low jump gravity for shorter hops
            rb.AddForce(Physics.gravity * (lowJumpMultiplier - 1f), ForceMode.Acceleration);
        }

        // Clamp fall speed
        if (rb.linearVelocity.y < maxFallSpeed)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, maxFallSpeed, rb.linearVelocity.z);
        }
    }

    private void AlignModelToGroundAndTilt()
    {
        // Get movement input
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        bool isMoving = Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f;

        // Calculate tilt rotation from input
        float targetForwardTilt = isMoving ? -v * tiltForwardAmount : 0f;
        float targetSideTilt = isMoving ? -h * tiltSideAmount : 0f;
        Quaternion moveTilt = Quaternion.Euler(targetForwardTilt, 0f, targetSideTilt);

        // WALL RUN ALIGNMENT (wheels on the wall)
        if (isWallRunning && currentWallNormal != Vector3.zero)
        {
            // local -up (wheels direction) should point into the wall,
            // so local up points away from the wall
            Vector3 upDir = -currentWallNormal;
            Vector3 forwardDir = currentWallForward.sqrMagnitude > 0.001f
                ? currentWallForward
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
            // GROUND ALIGNMENT
            if (Physics.Raycast(rayOrigin.position, Vector3.down, out RaycastHit hit, rayLength))
            {
                lastGroundNormal = hit.normal;

                // align model up to ground normal
                Quaternion groundTilt =
                    Quaternion.FromToRotation(cartModel.up, lastGroundNormal) * cartModel.rotation;

                cartModel.rotation = Quaternion.Slerp(
                    cartModel.rotation,
                    groundTilt,
                    Time.deltaTime * groundAlignSpeed
                );

                // keep yaw matched to rigidbody (so it faces movement/camera)
                Vector3 euler = cartModel.rotation.eulerAngles;
                cartModel.rotation = Quaternion.Euler(
                    euler.x,
                    rb.rotation.eulerAngles.y,
                    euler.z
                );
            }
            else
            {
                // No ground detected – ease back upright following rb
                cartModel.rotation = Quaternion.Slerp(
                    cartModel.rotation,
                    rb.rotation,
                    Time.deltaTime * 2f
                );
            }
        }

        // Apply visual input-based tilt on top
        cartModel.localRotation *= Quaternion.Lerp(
            Quaternion.identity,
            moveTilt,
            Time.deltaTime * tiltSpeed
        );
    }
}
