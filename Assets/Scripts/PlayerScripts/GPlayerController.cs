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

    public float rotationSpeed = 100f;
    public float linearDrag = 5f;

    [Header("Jump and Gravity Settings")]
    public float jumpForce = 5f;
    public float fallMultiplier = 2f;
    public float lowJumpMultiplier = 1.5f;
    public float maxFallSpeed = -20f;

    [Header("Ground Check Settings")]
    public Transform feetTransform;

    [Header("Visual Tilt Settings")]
    public Transform cartModel;
    //private Vector3 initialModelLocalPos;
    public Transform rayOrigin;
    public float rayLength = 1.5f;
    public float tiltForwardAmount = 10f;
    public float tiltSideAmount = 10f;
    public float tiltSpeed = 5f;
    public float airTiltAmount = 5f;
    public float groundAlignSpeed = 8f;
    public float maxVisualRoll = 25f;

    private Rigidbody rb;
    private bool isGrounded;
    private Vector3 lastGroundNormal = Vector3.up;
    private float _turnSmoothVelocity;

    void Start()
    {
        Cursor.visible = false; // Hides the cursor
        Cursor.lockState = CursorLockMode.Locked; // Locks it to the center
        rb = GetComponent<Rigidbody>();
    }


    void Update()
    {
        isGrounded = CheckGrounded();

        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            Jump();
        }

        // UI speed update for XP system & Bar
        float normalized = currentMoveSpeed / maxMoveSpeed; // 1 at top speed
        if (ExperienceManager.Instance != null)
            ExperienceManager.Instance.SetSpeedMultiplier(1f + normalized * 0.20f); // +20% XP gain
    }

    private void FixedUpdate()
    {
        Move();
        ApplyCustomGravity();
        AlignModelToGroundAndTilt();
        if (!isGrounded)
        {
            Quaternion rot = rb.rotation;
            rot.eulerAngles = new Vector3(0f, rot.eulerAngles.y, 0f);
            rb.MoveRotation(rot);
        }
    }

    private void Move()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // Determine steering axis
        Vector3 up = isGrounded ? lastGroundNormal : Vector3.up;

        // Only rotate if grounded OR using world upright in air
        float yawDelta = h * rotationSpeed * Time.fixedDeltaTime;
        transform.rotation = Quaternion.AngleAxis(yawDelta, up) * transform.rotation;

        // Planar basis relative to ground
        Vector3 forwardFlat = Vector3.ProjectOnPlane(transform.forward, up).normalized;
        Vector3 planarVel = Vector3.ProjectOnPlane(rb.linearVelocity, up);
        float planarSpeed = planarVel.magnitude;

        // Determine current physical motion direction (1 = forward, -1 = backward, 0 = stopped)
        float motionSign = (planarVel.sqrMagnitude > 0.001f) ? Mathf.Sign(Vector3.Dot(planarVel, forwardFlat)) : 0f;

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
            // Start forward speed bump if needed
            if (currentMoveSpeed < startMoveSpeed)
                currentMoveSpeed = startMoveSpeed;

            // Accelerate forward
            currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, maxMoveSpeed, acceleration * Time.deltaTime);

            // While accelerating forward, reset reverse speed
            currentReverseSpeed = 0f;
        }
        else if (pressingBackward)
        {
            if (currentMoveSpeed > 0.001f)
            {
                // BRAKE: reduce forward speed only (no reverse force yet)
                currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, 0f, manualDeceleration * Time.deltaTime);

                // ensure reverse speed is zero while braking
                currentReverseSpeed = 0f;
            }
            else
            {
                // Start reversing.
                currentReverseSpeed = Mathf.MoveTowards(currentReverseSpeed, backwardMaxMoveSpeed, backwardAcceleration * Time.deltaTime);

                // make sure forward remains zero
                currentMoveSpeed = 0f;
            }
        }
        else
        {
            // No input: natural slowdown for both directions
            currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, 0f, deceleration * Time.deltaTime);
            currentReverseSpeed = Mathf.MoveTowards(currentReverseSpeed, 0f, backwardDeceleration * Time.deltaTime);
        }

        // Movement application
        // Choose which speed is active (prefer forward if >0, otherwise reverse)
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
    public void BlockForwardMovement()
    {
        currentMoveSpeed = 0f;
    }

    public void BlockBackwardMovement()
    {
        currentReverseSpeed = 0f;
    }

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

        // No ground hit
        rb.linearDamping = 0f; // No drag when in air
        return false;
    }

    private void ApplyCustomGravity()
    {
        if (isGrounded) return; // do not modify grounded movement

        // Strong gravity while airborne
        rb.AddForce(Physics.gravity * (fallMultiplier - 1f), ForceMode.Acceleration);

        // Short hop: apply extra gravity when button released early
        if (rb.linearVelocity.y > 0 && !Input.GetButton("Jump"))
        {
            rb.AddForce(Physics.gravity * lowJumpMultiplier, ForceMode.Acceleration);
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

        // Calculate tilt rotation
        float targetForwardTilt = isMoving ? -v * tiltForwardAmount : 0f;
        float targetSideTilt = isMoving ? -h * tiltSideAmount : 0f;

        Quaternion moveTilt = Quaternion.Euler(targetForwardTilt, 0f, targetSideTilt);

        // Raycast to detect ground
        if (Physics.Raycast(rayOrigin.position, Vector3.down, out RaycastHit hit, rayLength))
        {
            lastGroundNormal = hit.normal; 
            
            // Calculate rotation that makes model's up match ground normal
            Quaternion groundTilt = Quaternion.FromToRotation(cartModel.up,lastGroundNormal) * cartModel.rotation; 
            
            // Smoothly interpolate to match the ground
            cartModel.rotation = Quaternion.Slerp(cartModel.rotation, groundTilt, Time.deltaTime * groundAlignSpeed); 
            
            // Align the cartModel horizontally with the camera
            cartModel.rotation = Quaternion.Euler(cartModel.rotation.eulerAngles.x, rb.rotation.eulerAngles.y, cartModel.rotation.eulerAngles.z); 
        } 
        else 
        { 
            // No ground detected — return to upright
            cartModel.rotation = Quaternion.Slerp(cartModel.rotation, rb.rotation, Time.deltaTime * 2f); 
        } 
        
        // Apply visual tilt relative to ground alignment
        cartModel.localRotation *= Quaternion.Lerp(Quaternion.identity, moveTilt, Time.deltaTime * tiltSpeed);
        
    }


    //private void AlignModelToGroundAndTilt()
    //{
    //    float h = Input.GetAxis("Horizontal");
    //    float v = Input.GetAxis("Vertical");
    //    bool isMoving = Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f;

    //    // ----- 1. Detect Ground -----
    //    Quaternion groundRotation;

    //    if (Physics.Raycast(rayOrigin.position, Vector3.down, out RaycastHit hit, rayLength))
    //    {
    //        // We are grounded
    //        lastGroundNormal = hit.normal;

    //        // Align model's UP with ground normal
    //        groundRotation = Quaternion.FromToRotation(Vector3.up, lastGroundNormal)
    //                        * Quaternion.Euler(0f, rb.rotation.eulerAngles.y, 0f);
    //    }
    //    else
    //    {
    //        // Use upright rotation while airborne
    //        groundRotation = Quaternion.Euler(0f, rb.rotation.eulerAngles.y, 0f);
    //    }

    //    // ----- 2. Calculate movement tilt (does NOT accumulate) -----
    //    float forwardTilt = 0f;
    //    float sideTilt = 0f;

    //    if (isMoving)
    //    {
    //        forwardTilt = -v * tiltForwardAmount;
    //        sideTilt = -h * tiltSideAmount;
    //    }
    //    else if (!isGrounded)
    //    {
    //        // Air tilt for jumps
    //        forwardTilt = -airTiltAmount;
    //    }

    //    Quaternion tiltRotation = Quaternion.Euler(forwardTilt, 0f, sideTilt);

    //    // ----- 3. Combine cleanly (NO *=) -----
    //    Quaternion targetRotation = groundRotation * tiltRotation;

    //    // ----- 4. Smooth -----
    //    cartModel.rotation = Quaternion.Slerp(
    //        cartModel.rotation,
    //        targetRotation,
    //        Time.deltaTime * groundAlignSpeed
    //    );
    //}
}

