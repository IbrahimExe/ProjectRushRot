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
    //private Transform cam;
    private bool isGrounded;
    private Vector3 lastGroundNormal = Vector3.up;
    private float _turnSmoothVelocity;

    void Start()
    {
        Cursor.visible = false; // Hides the cursor
        Cursor.lockState = CursorLockMode.Locked; // Locks it to the center
        rb = GetComponent<Rigidbody>();
        //cam = Camera.main.transform;
    }


    void Update()
    {
        isGrounded = CheckGrounded();

        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            Jump();
        }
    }

    private void FixedUpdate()
    {
        Move();
        ApplyCustomGravity();
        AlignModelToGroundAndTilt();
    }

    private void Move()
    {
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

    //private void Move()
    //{
    //    float h = Input.GetAxis("Horizontal");
    //    float v = Input.GetAxis("Vertical");

    //    // Use ground normal for proper slope handling
    //    Vector3 up = (lastGroundNormal == Vector3.zero) ? Vector3.up : lastGroundNormal;

    //    // Steer around ground up (decoupled from camera)
    //    float yawDelta = h * CameraRotationSpeed * Time.fixedDeltaTime;
    //    transform.rotation = Quaternion.AngleAxis(yawDelta, up) * transform.rotation;

    //    // Planar basis relative to ground
    //    Vector3 forwardFlat = Vector3.ProjectOnPlane(transform.forward, up).normalized;
    //    Vector3 planarVel = Vector3.ProjectOnPlane(rb.linearVelocity, up);
    //    float planarSpeed = planarVel.magnitude;

    //    // Maintain speed floor at actual motion (preserves momentum)
    //    if (currentMoveSpeed < planarSpeed)
    //        currentMoveSpeed = planarSpeed;

    //    bool pressingForward = v > 0.01f;
    //    bool pressingBackward = v < -0.01f;

    //    if (pressingForward)
    //    {
    //        // Start speed floor
    //        if (currentMoveSpeed < startMoveSpeed)
    //            currentMoveSpeed = startMoveSpeed;

    //        // Accelerate toward max
    //        currentMoveSpeed = Mathf.MoveTowards(
    //            currentMoveSpeed,
    //            maxMoveSpeed,
    //            acceleration * Time.deltaTime
    //        );
    //    }
    //    else if (pressingBackward)
    //    {
    //        // Apply braking instead of reversing direction
    //        currentMoveSpeed = Mathf.MoveTowards(
    //            currentMoveSpeed,
    //            0f,
    //            deceleration * 2f * Time.deltaTime // a bit stronger brake
    //        );
    //    }
    //    else
    //    {
    //        // Natural slowdown when no input
    //        currentMoveSpeed = Mathf.MoveTowards(
    //            currentMoveSpeed,
    //            0f,
    //            deceleration * Time.deltaTime
    //        );
    //    }

    //    // Target planar velocity (forward or reverse)
    //    Vector3 moveDir = forwardFlat * Mathf.Sign(v); // Reverse if v < 0


    //    Vector3 targetVelocity = moveDir * currentMoveSpeed;

    //    // Change ONLY planar velocity (vertical stays fully physics-driven)
    //    Vector3 velocityChange = targetVelocity - planarVel;
    //    rb.AddForce(velocityChange, ForceMode.VelocityChange);
    //}

    //private void Move()
    //    {
    //        // Input
    //        float h = Input.GetAxis("Horizontal");
    //        float v = Input.GetAxis("Vertical");
    //        Vector3 inputDir = new Vector3(h, 0f, v).normalized;

    //        bool isMoving = inputDir.magnitude >= 0.1f;

    //        // Horizontal velocity (ignore vertical)
    //        Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
    //        float currentVelocityMag = horizontalVel.magnitude;

    //        if (isMoving)
    //        {
    //            // Camera-relative movement
    //            float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + cam.eulerAngles.y;
    //            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _turnSmoothVelocity, 1f / rotationSpeed);
    //            transform.rotation = Quaternion.Euler(0f, angle, 0f);

    //            // Keep current speed from dropping below the actual physical velocity
    //            if (currentMoveSpeed < currentVelocityMag)
    //                currentMoveSpeed = currentVelocityMag;

    //            // Set initial move speed if starting from rest
    //            if (currentMoveSpeed < startMoveSpeed)
    //                currentMoveSpeed = startMoveSpeed;


    //            // Gradually accelerate toward max speed
    //            currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, maxMoveSpeed, acceleration * Time.deltaTime);

    //            // Apply movement
    //            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;


    //            Vector3 targetVelocity = moveDir.normalized * currentMoveSpeed;
    //            Vector3 velocityChange = targetVelocity - horizontalVel;

    //            rb.AddForce(velocityChange, ForceMode.VelocityChange);
    //        }
    //        else
    //        {
    //            // Gradually decelerate when no input
    //            currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, 0f, deceleration * Time.deltaTime);
    //        }

    //    }

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
        // Only modify gravity when falling or going up
        if (rb.linearVelocity.y < 0)
        {
            // Falling — add extra gravity
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
        //  Get movement input
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        bool isMoving = Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f;

        //  Calculate tilt rotation
        float targetForwardTilt = isMoving ? -v * tiltForwardAmount : 0f;
        float targetSideTilt = isMoving ? -h * tiltSideAmount : 0f;
        Quaternion moveTilt = Quaternion.Euler(targetForwardTilt, 0f, targetSideTilt);

        //  Raycast to detect ground
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

        //  Apply visual tilt relative to ground alignment
        cartModel.localRotation *= Quaternion.Lerp(Quaternion.identity, moveTilt, Time.deltaTime * tiltSpeed);
    }

}
